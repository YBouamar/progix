using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

public record AffectationInput(string Fonction, Guid UserId);
public record DessinateurInput(Guid? UserId);

public static class EquipeEndpoints
{
    // Qui peut être affecté, et qui peut affecter.
    private static readonly string[] RolesIngenieurs = [Roles.Ingenieur, Roles.IngenieurChef];
    private static readonly string[] RolesDessinateurs = [Roles.Dessinateur, Roles.DessinateurChef];
    private static readonly string[] RolesChefsChantier = [Roles.ChefChantier];

    private static bool PeutAffecter(ClaimsPrincipal u, string cible) =>
        u.IsInRole(Roles.Directeur) || u.IsInRole(Roles.ChefProjet) || u.IsInRole(cible);

    private static bool PeutIngenieurs(ClaimsPrincipal u) => PeutAffecter(u, Roles.IngenieurChef);
    private static bool PeutDessinateurs(ClaimsPrincipal u) => PeutAffecter(u, Roles.DessinateurChef);
    private static bool PeutChefsChantier(ClaimsPrincipal u) => PeutAffecter(u, Roles.ResponsableChantier);

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/projets/{id:guid}").WithTags("Équipe").RequireAuthorization();

        g.MapGet("/equipe", async (Guid id, ProjetsDbContext db, IUtilisateursLookup users, ClaimsPrincipal user) =>
            Results.Ok(await Equipe(id, db, users, user)));

        g.MapPost("/equipe", async (Guid id, AffectationInput input, ProjetsDbContext db, IUtilisateursLookup users, ClaimsPrincipal user) =>
        {
            var (roles, droit) = input.Fonction switch
            {
                Fonctions.Ingenieur => (RolesIngenieurs, PeutIngenieurs(user)),
                Fonctions.ChefChantier => (RolesChefsChantier, PeutChefsChantier(user)),
                _ => ([], false),
            };
            if (roles.Length == 0) return Results.NotFound();
            if (!droit) return Results.Forbid();
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (projet is null) return Results.NotFound();
            if (projet.Supprime) return ProjetsEndpoints.Conflit("Un projet supprimé est en lecture seule.");
            var personne = (await users.ParRolesAsync(roles)).FirstOrDefault(u => u.Id == input.UserId);
            if (personne is null) return ProjetsEndpoints.Conflit("Cette personne ne peut pas être affectée à cette fonction.");
            if (await db.Affectations.AnyAsync(a => a.ProjetId == id && a.Fonction == input.Fonction && a.UserId == input.UserId && a.Fin == null))
                return ProjetsEndpoints.Conflit($"{personne.Nom} est déjà sur ce projet.");
            db.Affectations.Add(new Affectation
            {
                ProjetId = id, Fonction = input.Fonction, UserId = personne.Id, UserNom = personne.Nom, Debut = DateOnly.FromDateTime(DateTime.UtcNow),
            });
            await db.SaveChangesAsync();
            return Results.Ok(await Equipe(id, db, users, user));
        });

        g.MapPost("/equipe/{aid:guid}/retirer", async (Guid id, Guid aid, ProjetsDbContext db, IUtilisateursLookup users, ClaimsPrincipal user) =>
        {
            var a = await db.Affectations.FirstOrDefaultAsync(x => x.Id == aid && x.ProjetId == id && x.Fin == null);
            if (a is null) return Results.NotFound();
            if (!(a.Fonction == Fonctions.Ingenieur ? PeutIngenieurs(user) : PeutChefsChantier(user))) return Results.Forbid();
            a.Fin = DateOnly.FromDateTime(DateTime.UtcNow);
            a.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(await Equipe(id, db, users, user));
        });

        g.MapPut("/dalles/{did:guid}/dessinateur", async (Guid id, Guid did, DessinateurInput input, ProjetsDbContext db, IUtilisateursLookup users, ClaimsPrincipal user) =>
        {
            if (!PeutDessinateurs(user)) return Results.Forbid();
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            var d = await db.Dalles.FirstOrDefaultAsync(x => x.Id == did && x.ProjetId == id);
            if (projet is null || d is null) return Results.NotFound();
            if (projet.Supprime) return ProjetsEndpoints.Conflit("Un projet supprimé est en lecture seule.");
            if (input.UserId is Guid uid)
            {
                var personne = (await users.ParRolesAsync(RolesDessinateurs)).FirstOrDefault(u => u.Id == uid);
                if (personne is null) return ProjetsEndpoints.Conflit("Cette personne n'est pas dessinateur.");
                d.DessinateurId = personne.Id;
                d.DessinateurNom = personne.Nom;
            }
            else
            {
                d.DessinateurId = null;
                d.DessinateurNom = null;
            }
            d.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(DallesEndpoints.Dto(d, projet.Code));
        });
    }

    private static async Task<object> Equipe(Guid id, ProjetsDbContext db, IUtilisateursLookup users, ClaimsPrincipal user)
    {
        var rows = await db.Affectations.AsNoTracking().Where(a => a.ProjetId == id).ToListAsync();
        object Liste(string fonction) => rows.Where(a => a.Fonction == fonction)
            .OrderBy(a => a.Fin != null).ThenByDescending(a => a.Debut).ThenByDescending(a => a.CreatedAt)
            .Select(a => new { id = a.Id, userId = a.UserId, nom = a.UserNom, debut = a.Debut, fin = a.Fin });
        async Task<object> Candidats(string[] roles) =>
            (await users.ParRolesAsync(roles)).Select(u => new { id = u.Id, nom = u.Nom });
        return new
        {
            ingenieurs = Liste(Fonctions.Ingenieur),
            chefsChantier = Liste(Fonctions.ChefChantier),
            candidats = new
            {
                ingenieurs = await Candidats(RolesIngenieurs),
                chefsChantier = await Candidats(RolesChefsChantier),
                dessinateurs = await Candidats(RolesDessinateurs),
            },
            droits = new { ingenieurs = PeutIngenieurs(user), chefsChantier = PeutChefsChantier(user), dessinateurs = PeutDessinateurs(user) },
        };
    }
}
