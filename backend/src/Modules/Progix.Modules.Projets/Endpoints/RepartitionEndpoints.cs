using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

/// <summary>Tableaux de répartition : projets par ingénieur, dalles par dessinateur. Les affectations passent par les points d'accès de l'équipe.</summary>
public static class RepartitionEndpoints
{
    private static bool Voit(ClaimsPrincipal u, string chef) => u.IsInRole(Roles.Directeur) || u.IsInRole(Roles.ChefProjet) || u.IsInRole(chef);

    /// <summary>Avis de conformité d'une dalle : directeur, chef de projet, responsable administratif.</summary>
    public static readonly string[] RolesConformite = [Roles.Directeur, Roles.ChefProjet, Roles.ResponsableAdministratif];

    public record ConformiteInput(bool? Conforme, string? Remarque);
    public record EtudeInput(DateOnly? Date);

    public static void Map(IEndpointRouteBuilder app)
    {
        // Avis de conformité d'une dalle réalisée. Non conforme : la remarque est obligatoire.
        app.MapPut("/api/projets/{id:guid}/dalles/{did:guid}/conformite", async (Guid id, Guid did, ConformiteInput input, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            if (!RolesConformite.Any(user.IsInRole)) return Results.Forbid();
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            var d = await db.Dalles.FirstOrDefaultAsync(x => x.Id == did && x.ProjetId == id);
            if (projet is null || d is null) return Results.NotFound();
            if (projet.Supprime) return ProjetsEndpoints.Conflit("Un projet supprimé est en lecture seule.");
            if (d.EtapesFaites < Etapes.Total) return ProjetsEndpoints.Conflit("L'avis de conformité se donne une fois les 11 étapes de la dalle faites.");
            if (input.Conforme == false && string.IsNullOrWhiteSpace(input.Remarque)) return ProjetsEndpoints.Conflit("Indiquez pourquoi la dalle n'est pas conforme.");
            d.Conforme = input.Conforme;
            d.ConformiteRemarque = input.Conforme == false ? input.Remarque!.Trim() : null;
            d.ConformitePar = input.Conforme is null ? null : user.UserName();
            d.ConformiteLe = input.Conforme is null ? null : DateTime.UtcNow;
            d.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = d.Id, conforme = d.Conforme, remarque = d.ConformiteRemarque, par = d.ConformitePar, le = d.ConformiteLe });
        }).WithTags("Suivi de chantier").RequireAuthorization();

        // Date d'étude planifiée d'une dalle : par ceux qui répartissent les dessinateurs.
        app.MapPut("/api/projets/{id:guid}/dalles/{did:guid}/etude", async (Guid id, Guid did, EtudeInput input, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            if (!Voit(user, Roles.DessinateurChef)) return Results.Forbid();
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            var d = await db.Dalles.FirstOrDefaultAsync(x => x.Id == did && x.ProjetId == id);
            if (projet is null || d is null) return Results.NotFound();
            if (projet.Supprime) return ProjetsEndpoints.Conflit("Un projet supprimé est en lecture seule.");
            d.EtudePlanifiee = input.Date; d.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = d.Id, etudePlanifiee = d.EtudePlanifiee });
        }).WithTags("Répartition").RequireAuthorization();

        var g = app.MapGroup("/api/repartition").WithTags("Répartition").RequireAuthorization();

        // Projets en cours et leurs ingénieurs.
        g.MapGet("/ingenieurs", async (ProjetsDbContext db, IUtilisateursLookup users, ClaimsPrincipal user) =>
        {
            if (!Voit(user, Roles.IngenieurChef)) return Results.Forbid();
            var projets = await db.Projets.AsNoTracking().Where(p => !p.Supprime && p.Statut != StatutsProjet.Termine).OrderByDescending(p => p.DateCreation).ToListAsync();
            var ids = projets.Select(p => p.Id).ToList();
            var affectations = await db.Affectations.AsNoTracking().Where(a => ids.Contains(a.ProjetId) && a.Fonction == Fonctions.Ingenieur && a.Fin == null).ToListAsync();
            var dalles = await db.Dalles.AsNoTracking().Where(d => ids.Contains(d.ProjetId)).Select(d => new { d.ProjetId, d.EtapesFaites }).ToListAsync();
            return Results.Ok(new
            {
                personnes = (await users.ParRolesAsync([Roles.Ingenieur, Roles.IngenieurChef])).OrderBy(u => u.Nom).Select(u => new { id = u.Id, nom = u.Nom }),
                projets = projets.Select(p => new
                {
                    id = p.Id, code = p.Code, libelle = p.Libelle, statut = p.Statut, statutLabel = StatutsProjet.Labels.GetValueOrDefault(p.Statut, p.Statut), bloque = p.Bloque,
                    dalles = dalles.Count(d => d.ProjetId == p.Id), realisees = dalles.Count(d => d.ProjetId == p.Id && d.EtapesFaites >= Etapes.Total),
                    ingenieurs = affectations.Where(a => a.ProjetId == p.Id).Select(a => new { affectationId = a.Id, userId = a.UserId }),
                }),
            });
        });

        // Dalles dont l'étude n'est pas encore validée par le BCT, et leur dessinateur.
        g.MapGet("/dessinateurs", async (ProjetsDbContext db, IUtilisateursLookup users, ClaimsPrincipal user) =>
        {
            if (!Voit(user, Roles.DessinateurChef)) return Results.Forbid();
            var rows = await (from d in db.Dalles.AsNoTracking() join p in db.Projets.AsNoTracking() on d.ProjetId equals p.Id
                              where !p.Supprime && d.EtapesFaites < Etapes.ValidationBct
                              orderby p.Code, d.Numero select new { d, p }).ToListAsync();
            var jour = DateOnly.FromDateTime(DateTime.UtcNow);
            // Tri par date d'étude planifiée, les dalles sans date en dernier.
            rows = rows.OrderBy(r => r.d.EtudePlanifiee is null).ThenBy(r => r.d.EtudePlanifiee).ThenBy(r => r.p.Code).ThenBy(r => r.d.Numero).ToList();
            return Results.Ok(new
            {
                personnes = (await users.ParRolesAsync([Roles.Dessinateur, Roles.DessinateurChef])).OrderBy(u => u.Nom).Select(u => new { id = u.Id, nom = u.Nom }),
                dalles = rows.Select(r => new
                {
                    id = r.d.Id, projetId = r.p.Id, reference = $"{r.p.Code}-{r.d.Suffixe}", projet = r.p.Libelle, bloque = r.p.Bloque,
                    emplacement = string.Join(" · ", new[] { r.d.Bloc, r.d.Immeuble, Dalle.NiveauLabel(r.d.Niveau) }.Where(x => !string.IsNullOrWhiteSpace(x))),
                    dessinateurId = r.d.DessinateurId, etudePlanifiee = r.d.EtudePlanifiee,
                    enRetard = r.d.EtudePlanifiee is { } prevue && prevue < jour && r.d.EtapesFaites < 1,
                    // État de l'étude : à lancer sans dessinateur, en cours une fois affectée, terminée quand les plans d'exécution sont faits.
                    etat = r.d.EtapesFaites >= 1 ? "terminee" : r.d.DessinateurId is null ? "lancer" : "cours",
                }),
            });
        });
    }
}
