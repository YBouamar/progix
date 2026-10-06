using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

public record VersionInput(string? Commentaire);

/// <summary>Versions des plans d'exécution d'une dalle.</summary>
public static class VersionsEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/projets/{id:guid}/dalles/{did:guid}/versions").WithTags("Suivi de chantier").RequireAuthorization();

        g.MapGet("/", async (Guid id, Guid did, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            var dalle = await db.Dalles.AsNoTracking().FirstOrDefaultAsync(d => d.Id == did && d.ProjetId == id);
            return projet is null || dalle is null ? Results.NotFound() : Results.Ok(await Vue(db, projet, dalle, user));
        });

        g.MapPost("/", async (Guid id, Guid did, VersionInput input, ProjetsDbContext db, IFichiersLookup fichiers, ClaimsPrincipal user) =>
        {
            if (!FichiersPlans.Roles.Any(user.IsInRole)) return Results.Forbid();
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            var dalle = await db.Dalles.FirstOrDefaultAsync(d => d.Id == did && d.ProjetId == id);
            if (projet is null || dalle is null) return Results.NotFound();
            if (projet.Supprime) return ProjetsEndpoints.Conflit("Un projet supprimé est en lecture seule.");

            if (dalle.EtapesFaites >= Etapes.ValidationBct) return ProjetsEndpoints.Conflit(VersionPlan.Fige);

            var derniere = await db.VersionsPlans.Where(v => v.DalleId == did).OrderByDescending(v => v.Lettre).FirstOrDefaultAsync();
            char lettre;
            if (derniere is null) lettre = dalle.Version; // la première version garde la lettre de départ de la dalle
            else
            {
                if (await fichiers.NombreAsync(VersionPlan.Contexte, derniere.Id) == 0)
                    return ProjetsEndpoints.Conflit($"La version {derniere.Lettre} ne contient encore aucun fichier.");
                if (derniere.Lettre >= 'Z') return ProjetsEndpoints.Conflit("La dalle a atteint la dernière lettre de version.");
                lettre = (char)(derniere.Lettre + 1);
            }
            db.VersionsPlans.Add(new VersionPlan
            {
                DalleId = did, Lettre = lettre, CreePar = user.UserName(),
                Commentaire = string.IsNullOrWhiteSpace(input.Commentaire) ? null : input.Commentaire.Trim(),
            });
            dalle.Version = lettre;
            dalle.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(await Vue(db, projet, dalle, user));
        });
    }

    /// <summary>Ce qui manque pour valider l'étape « Plans d'exécution », ou null si la dernière version est complète.</summary>
    public static async Task<string?> ManqueAsync(ProjetsDbContext db, IFichiersLookup fichiers, Guid dalleId)
    {
        var derniere = await db.VersionsPlans.AsNoTracking().Where(v => v.DalleId == dalleId).OrderByDescending(v => v.Lettre).FirstOrDefaultAsync();
        if (derniere is null) return "Créez une version des plans d'exécution avant de valider l'étape.";
        var categories = await fichiers.CategoriesAsync(VersionPlan.Contexte, derniere.Id);
        var manque = new List<string>();
        if (!categories.Contains(VersionPlan.Plan)) manque.Add("le plan d'exécution");
        if (!categories.Contains(VersionPlan.Modele)) manque.Add("le modèle de calcul");
        return manque.Count == 0 ? null : $"Il manque {string.Join(" et ", manque)} dans la version {derniere.Lettre}.";
    }

    private static async Task<object> Vue(ProjetsDbContext db, Projet projet, Dalle dalle, ClaimsPrincipal user)
    {
        var versions = await db.VersionsPlans.AsNoTracking().Where(v => v.DalleId == dalle.Id).OrderByDescending(v => v.Lettre).ToListAsync();
        return new
        {
            dalle = DallesEndpoints.Dto(dalle, projet.Code),
            peutCreer = !projet.Supprime && dalle.EtapesFaites < Etapes.ValidationBct && FichiersPlans.Roles.Any(user.IsInRole),
            fige = dalle.EtapesFaites >= Etapes.ValidationBct,
            versions = versions.Select((v, i) => new
            {
                id = v.Id, lettre = v.Lettre.ToString(), commentaire = v.Commentaire, creePar = v.CreePar, date = v.CreatedAt, courante = i == 0,
            }),
        };
    }
}
