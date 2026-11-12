using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

/// <summary>
/// Chiffres du menu : dalles qui restent à traiter dans Étude, Exécution, Chantier et Logistique.
/// L'ingénieur, le dessinateur et le chef de chantier comptent ce qui est à leur nom ; les autres fonctions, le total.
/// </summary>
public static class ATraiterEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        MapATraiter(app);

        // Notifications du navigateur : avis de changement d'étape survenus depuis une date, pour la personne connectée.
        // Rien n'est stocké : les avis se déduisent de l'étape en cours de chaque dalle. Fenêtre limitée à sept jours.
        app.MapGet("/api/notifications/etapes", async (DateTime? depuis, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var maintenant = DateTime.UtcNow;
            var plancher = maintenant.AddDays(-7);
            var apres = depuis is DateTime dt && dt.ToUniversalTime() > plancher ? dt.ToUniversalTime() : plancher;
            var moi = user.UserId();
            var nom = user.UserName();
            Func<string, bool> aRole = user.IsInRole;

            var dalles = from d in db.Dalles.AsNoTracking()
                         join p in db.Projets.AsNoTracking() on d.ProjetId equals p.Id
                         where !p.Supprime && !p.Bloque && d.EtapesFaites < Etapes.Total
                         select new { d, p };
            // Ingénieur, dessinateur et chef de chantier : seulement les projets ou les dalles où ils sont affectés.
            if (NotificationsEtapes.RolesAffectes.Any(user.IsInRole) && !Policies.RolesDirection.Any(user.IsInRole))
            {
                var etudes = db.AffectationsEtude.AsNoTracking().Where(a => a.Fin == null && a.UserId == moi);
                var executions = db.AffectationsExecution.AsNoTracking().Where(a => a.Fin == null && a.UserId == moi);
                var projets = db.Affectations.AsNoTracking().Where(a => a.Fin == null && a.UserId == moi);
                dalles = dalles.Where(x => projets.Any(a => a.ProjetId == x.p.Id) || etudes.Any(a => a.DalleId == x.d.Id) || executions.Any(a => a.DalleId == x.d.Id));
            }
            var liste = await dalles.ToListAsync();
            var ids = liste.Select(x => x.d.Id).ToList();
            var etapes = await db.Etapes.AsNoTracking().Where(e => ids.Contains(e.DalleId)).ToListAsync();
            var traces = await db.EtapeTraces.AsNoTracking().Where(t => ids.Contains(t.DalleId) && t.CreatedAt > plancher).ToListAsync();

            var items = new List<(DateTime Le, object Vue)>();
            foreach (var x in liste)
            {
                var n = x.d.EtapesFaites + 1;
                var etape = etapes.FirstOrDefault(e => e.DalleId == x.d.Id && e.Numero == n);
                // Début de l'étape en cours : fin de la précédente, création de la dalle, ou déverrouillage plus récent.
                var debut = n == 1 ? (x.d.CreatedAt, (string?)null) : NotificationsEtapes.DerniereAction(etapes.FirstOrDefault(e => e.DalleId == x.d.Id && e.Numero == n - 1)) ?? (x.d.CreatedAt, null);
                var trace = traces.Where(t => t.DalleId == x.d.Id && t.Numero == n).OrderByDescending(t => t.CreatedAt).FirstOrDefault();
                var rouverte = trace is not null && trace.CreatedAt > debut.Item1;
                if (rouverte) debut = (trace!.CreatedAt, trace.Par);
                foreach (var a in NotificationsEtapes.Pour(n, etape, debut, rouverte, aRole))
                {
                    // Pas d'avis pour sa propre action, ni pour ce qui est plus ancien que la date demandée.
                    if (a.Le <= apres || a.Par == nom) continue;
                    var reference = $"{x.p.Code}-{x.d.Suffixe}";
                    items.Add((a.Le, new
                    {
                        id = $"{x.d.Id:N}-{a.Numero}-{a.Genre}-{a.Le.Ticks}", genre = a.Genre, le = a.Le, titre = $"{reference} · {x.p.Libelle}", texte = a.Texte,
                        par = a.Par, lien = $"/projets/{x.p.Id}", projetId = x.p.Id, dalleId = x.d.Id, etape = a.Numero,
                    }));
                }
            }
            // Affectations reçues : étude ou exécution d'une dalle, équipe d'un projet. On n'est pas prévenu de s'être affecté soi-même.
            var mesEtudes = await (from a in db.AffectationsEtude.AsNoTracking() join d in db.Dalles.AsNoTracking() on a.DalleId equals d.Id join p in db.Projets.AsNoTracking() on d.ProjetId equals p.Id
                                   where a.UserId == moi && a.Fin == null && a.CreatedAt > apres && !p.Supprime select new { a.Id, a.CreatedAt, a.AffectePar, d, p }).ToListAsync();
            var mesExecutions = await (from a in db.AffectationsExecution.AsNoTracking() join d in db.Dalles.AsNoTracking() on a.DalleId equals d.Id join p in db.Projets.AsNoTracking() on d.ProjetId equals p.Id
                                       where a.UserId == moi && a.Fin == null && a.CreatedAt > apres && !p.Supprime select new { a.Id, a.CreatedAt, a.AffectePar, d, p }).ToListAsync();
            var mesProjets = await (from a in db.Affectations.AsNoTracking() join p in db.Projets.AsNoTracking() on a.ProjetId equals p.Id
                                    where a.UserId == moi && a.Fin == null && a.CreatedAt > apres && !p.Supprime select new { a.Id, a.CreatedAt, a.Fonction, p }).ToListAsync();
            object Affectation(Guid id, DateTime le, string titre, string texte, string? par, string lien) =>
                new { id = $"{id:N}-affectation", genre = NotificationsEtapes.Affecte, le, titre, texte, par, lien };
            items.AddRange(mesEtudes.Where(x => x.AffectePar != nom).Select(x => (x.CreatedAt,
                Affectation(x.Id, x.CreatedAt, $"{x.p.Code}-{x.d.Suffixe} · {x.p.Libelle}", "Vous êtes affecté à l'étude de cette dalle.", x.AffectePar, "/etudes"))));
            items.AddRange(mesExecutions.Where(x => x.AffectePar != nom).Select(x => (x.CreatedAt,
                Affectation(x.Id, x.CreatedAt, $"{x.p.Code}-{x.d.Suffixe} · {x.p.Libelle}", "Vous êtes affecté à l'exécution de cette dalle.", x.AffectePar, "/execution"))));
            items.AddRange(mesProjets.Select(x => (x.CreatedAt,
                Affectation(x.Id, x.CreatedAt, $"{x.p.Code} · {x.p.Libelle}", x.Fonction == Fonctions.ChefChantier ? "Vous êtes affecté à ce projet comme chef de chantier." : "Vous êtes affecté à ce projet.", null, $"/projets/{x.p.Id}"))));

            return Results.Ok(new { maintenant, items = items.OrderByDescending(i => i.Le).Take(50).Select(i => i.Vue) });
        }).WithTags("Tableau de bord").RequireAuthorization();
    }

    private static void MapATraiter(IEndpointRouteBuilder app) =>
        app.MapGet("/api/a-traiter", async (ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var moi = user.UserId();
            var dalles = from d in db.Dalles.AsNoTracking() join p in db.Projets.AsNoTracking() on d.ProjetId equals p.Id where !p.Supprime select d;
            var r = new Dictionary<string, int>();

            // Étude : études non terminées ; pour un ingénieur, celles où il est affecté.
            if (EtudeDalle.RolesVue.Any(user.IsInRole))
            {
                var actives = db.AffectationsEtude.AsNoTracking().Where(a => a.Fin == null);
                var ouvertes = dalles.Where(d => d.EtudeTermineeLe == null);
                r[Habilitations.Etude] = user.IsInRole(Roles.Ingenieur)
                    ? await ouvertes.CountAsync(d => actives.Any(a => a.DalleId == d.Id && a.UserId == moi))
                    : await ouvertes.CountAsync();
            }

            // Exécution : dalles sans dossier ou au dossier en cours ; pour un dessinateur, celles où il est affecté.
            if (ExecutionDalle.RolesVue.Any(user.IsInRole))
            {
                var actives = db.AffectationsExecution.AsNoTracking().Where(a => a.Fin == null);
                var derniers = db.VersionsPlans.AsNoTracking().Where(v => !db.VersionsPlans.Any(w => w.DalleId == v.DalleId && w.Lettre > v.Lettre));
                var ouvertes = dalles.Select(d => new { d.Id, Dossier = derniers.Where(v => v.DalleId == d.Id).Select(v => v.Etat).FirstOrDefault() })
                    .Where(x => x.Dossier == null || x.Dossier == ExecutionDalle.EnCours);
                r[Habilitations.Execution] = user.IsInRole(Roles.Dessinateur)
                    ? await ouvertes.CountAsync(x => actives.Any(a => a.DalleId == x.Id && a.UserId == moi))
                    : await ouvertes.CountAsync();
            }

            // Chantier : dalles dont les étapes ne sont pas toutes faites ; pour un chef de chantier, celles de ses projets.
            if (ChantierDalle.RolesVue.Any(user.IsInRole))
            {
                var chefs = db.Affectations.AsNoTracking().Where(a => a.Fonction == Fonctions.ChefChantier && a.Fin == null);
                var ouvertes = dalles.Where(d => d.EtapesFaites < Etapes.Total);
                r[Habilitations.Chantier] = ChantierDalle.RolesAffectation.Any(user.IsInRole)
                    ? await ouvertes.CountAsync()
                    : await ouvertes.CountAsync(d => chefs.Any(a => a.ProjetId == d.ProjetId && a.UserId == moi));
            }

            // Logistique : commandes usine à créer.
            if (Policies.RolesLogistique.Any(user.IsInRole))
                r[Habilitations.Logistique] = await LogistiqueEndpoints.DallesACommander(db).CountAsync();

            return Results.Ok(r);
        }).WithTags("Tableau de bord").RequireAuthorization();
}
