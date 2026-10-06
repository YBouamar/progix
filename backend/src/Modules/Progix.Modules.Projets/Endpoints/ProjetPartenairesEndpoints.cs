using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

public record AffectationPartenaireInput(Guid? PartenaireId);

public static class ProjetPartenairesEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/projets/{id:guid}/partenaires").WithTags("Projets").RequireAuthorization();

        g.MapGet("/", async (Guid id, ProjetsDbContext db, IPartenairesLookup partenaires) => Results.Ok(await Liste(id, db, partenaires)));

        // Affecte, change ou retire (partenaireId nul) le partenaire d'un rôle.
        g.MapPut("/{role}", async (Guid id, string role, AffectationPartenaireInput input, ProjetsDbContext db, IPartenairesLookup partenaires) =>
        {
            if (RolesPartenaire.All.All(r => r.Code != role)) return Results.NotFound();
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (projet is null) return Results.NotFound();
            if (projet.Supprime) return ProjetsEndpoints.Conflit("Un projet supprimé est en lecture seule.");

            var lien = await db.ProjetPartenaires.FirstOrDefaultAsync(x => x.ProjetId == id && x.Role == role);
            if (input.PartenaireId is not Guid pid)
            {
                if (lien is not null) db.ProjetPartenaires.Remove(lien);
            }
            else
            {
                var fiches = await partenaires.FichesAsync([pid]);
                if (!fiches.TryGetValue(pid, out var fiche)) return ProjetsEndpoints.Conflit("Partenaire introuvable.");
                if (fiche.Type != role) return ProjetsEndpoints.Conflit("Le type de ce partenaire ne correspond pas au rôle.");
                if (lien is null) db.ProjetPartenaires.Add(new ProjetPartenaire { ProjetId = id, Role = role, PartenaireId = pid });
                else { lien.PartenaireId = pid; lien.UpdatedAt = DateTime.UtcNow; }
            }
            await db.SaveChangesAsync();
            return Results.Ok(await Liste(id, db, partenaires));
        }).RequireAuthorization(Policies.Commercial);
    }

    private static async Task<object> Liste(Guid id, ProjetsDbContext db, IPartenairesLookup partenaires)
    {
        var liens = await db.ProjetPartenaires.AsNoTracking().Where(x => x.ProjetId == id).ToListAsync();
        var fiches = await partenaires.FichesAsync(liens.Select(l => l.PartenaireId));
        return RolesPartenaire.All.Select(r =>
        {
            var lien = liens.FirstOrDefault(l => l.Role == r.Code);
            var fiche = lien is null ? null : fiches.GetValueOrDefault(lien.PartenaireId);
            return new { role = r.Code, label = r.Label, partenaireId = fiche?.Id, nom = fiche?.Nom, contact = fiche?.Contact };
        });
    }
}
