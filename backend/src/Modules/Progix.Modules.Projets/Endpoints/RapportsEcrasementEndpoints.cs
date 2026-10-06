using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

public record RapportEcrasementInput(decimal? Valeur, bool? Conforme, string? Remarque);

/// <summary>Rapports d'écrasement d'une dalle (étape 8) : valeur, conformité, fichier du laboratoire, et historique des rapports non conformes.</summary>
public static class RapportsEcrasementEndpoints
{
    public const int Etape = FichiersRapport.NumeroEtape;

    private static bool PeutSaisir(ClaimsPrincipal u) =>
        u.IsInRole(Roles.Directeur) || u.IsInRole(Roles.ChefProjet) || Etapes.Get(Etape).Roles.Any(u.IsInRole);

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/projets/{id:guid}/dalles/{did:guid}/rapports-ecrasement").WithTags("Suivi de chantier").RequireAuthorization();

        g.MapGet("/", async (Guid id, Guid did, ProjetsDbContext db, IFichiersLookup fichiers, ClaimsPrincipal user) =>
        {
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            var dalle = await db.Dalles.AsNoTracking().FirstOrDefaultAsync(d => d.Id == did && d.ProjetId == id);
            return projet is null || dalle is null ? Results.NotFound() : Results.Ok(await Vue(db, fichiers, projet, dalle, user));
        });

        g.MapPost("/", async (Guid id, Guid did, RapportEcrasementInput input, ProjetsDbContext db, IFichiersLookup fichiers, ClaimsPrincipal user) =>
        {
            if (!PeutSaisir(user)) return Results.Forbid();
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            var dalle = await db.Dalles.AsNoTracking().FirstOrDefaultAsync(d => d.Id == did && d.ProjetId == id);
            if (projet is null || dalle is null) return Results.NotFound();
            if (Verrou(projet, dalle) is { } verrou) return ProjetsEndpoints.Conflit(verrou);
            if (input.Valeur is not > 0) return ProjetsEndpoints.Conflit("Saisissez la valeur du rapport, en MPa : un nombre supérieur à 0.");
            if (input.Valeur > 200) return ProjetsEndpoints.Conflit("La valeur saisie dépasse 200 MPa : vérifiez-la.");
            if (input.Conforme is null) return ProjetsEndpoints.Conflit("Indiquez si le rapport est conforme ou non conforme.");

            await ReprendreAsync(db, fichiers, did);
            var dernier = await db.RapportsEcrasement.Where(r => r.DalleId == did).OrderByDescending(r => r.Numero).FirstOrDefaultAsync();
            if (dernier is not null && await fichiers.NombreAsync(FichiersRapport.Nom, dernier.Id) == 0)
                return ProjetsEndpoints.Conflit($"Déposez d'abord le fichier du rapport n° {dernier.Numero} : il reste dans l'historique.");
            db.RapportsEcrasement.Add(new RapportEcrasement
            {
                DalleId = did, Numero = (dernier?.Numero ?? 0) + 1, Valeur = Math.Round(input.Valeur.Value, 2), Conforme = input.Conforme.Value,
                Remarque = string.IsNullOrWhiteSpace(input.Remarque) ? null : input.Remarque.Trim(), SaisiPar = user.UserName(),
            });
            await db.SaveChangesAsync();
            return Results.Ok(await Vue(db, fichiers, projet, dalle, user));
        });
    }

    private static string? Verrou(Projet projet, Dalle dalle) =>
        projet.Supprime ? "Un projet supprimé est en lecture seule."
        : projet.Bloque ? "Projet bloqué : les étapes de chantier sont suspendues."
        : dalle.EtapesFaites >= Etape ? "Le rapport d'écrasement est validé : il est figé."
        : dalle.EtapesFaites < Etape - 1 ? "L'étape du rapport d'écrasement n'est pas encore en cours." : null;

    /// <summary>Les fichiers déposés avant la saisie de la valeur étaient rattachés à la dalle : ils deviennent son rapport n° 1.</summary>
    public static async Task ReprendreAsync(ProjetsDbContext db, IFichiersLookup fichiers, Guid dalleId)
    {
        if (await db.RapportsEcrasement.AnyAsync(r => r.DalleId == dalleId) || await fichiers.NombreAsync(FichiersRapport.Nom, dalleId) == 0) return;
        db.RapportsEcrasement.Add(new RapportEcrasement { Id = dalleId, DalleId = dalleId, Numero = 1, Conforme = true, Remarque = "Rapport déposé avant la saisie de la valeur.", SaisiPar = "Reprise" });
        await db.SaveChangesAsync();
    }

    /// <summary>Ce qui empêche de valider l'étape, ou null : il faut un dernier rapport conforme, avec son fichier.</summary>
    public static async Task<string?> RefusAsync(ProjetsDbContext db, IFichiersLookup fichiers, Guid dalleId)
    {
        await ReprendreAsync(db, fichiers, dalleId);
        var dernier = await db.RapportsEcrasement.AsNoTracking().Where(r => r.DalleId == dalleId).OrderByDescending(r => r.Numero).FirstOrDefaultAsync();
        if (dernier is null) return "Le rapport d'écrasement est obligatoire : saisissez sa valeur et déposez le fichier avant de valider l'étape.";
        if (await fichiers.NombreAsync(FichiersRapport.Nom, dernier.Id) == 0) return $"Déposez le fichier du rapport d'écrasement n° {dernier.Numero} avant de valider l'étape.";
        if (!dernier.Conforme) return $"Le rapport d'écrasement n° {dernier.Numero} est non conforme : l'étape reste bloquée jusqu'à un nouveau rapport conforme.";
        return null;
    }

    private static async Task<object> Vue(ProjetsDbContext db, IFichiersLookup fichiers, Projet projet, Dalle dalle, ClaimsPrincipal user)
    {
        await ReprendreAsync(db, fichiers, dalle.Id);
        var rapports = await db.RapportsEcrasement.AsNoTracking().Where(r => r.DalleId == dalle.Id).OrderByDescending(r => r.Numero).ToListAsync();
        var verrou = Verrou(projet, dalle);
        var dernierSansFichier = rapports.Count > 0 && await fichiers.NombreAsync(FichiersRapport.Nom, rapports[0].Id) == 0;
        return new
        {
            verrou, fige = dalle.EtapesFaites >= Etape, dernierSansFichier,
            peutCreer = verrou is null && PeutSaisir(user) && !dernierSansFichier,
            rapports = rapports.Select((r, i) => new
            {
                id = r.Id, numero = r.Numero, valeur = r.Valeur, conforme = r.Conforme, remarque = r.Remarque, saisiPar = r.SaisiPar, date = r.CreatedAt, dernier = i == 0,
            }),
        };
    }
}
