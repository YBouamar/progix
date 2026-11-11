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
    public static void Map(IEndpointRouteBuilder app) =>
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
