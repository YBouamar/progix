using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

public record PointControleInput(string? Code, string? Resultat, string? Observation);
public record AutoControleInput(List<PointControleInput>? Points);

/// <summary>Fiche d'auto-contrôle du plancher, à la réception de dalle (étape 6).</summary>
public static class AutoControleEndpoints
{
    public const int Etape = 6;
    private static readonly string[] Resultats = [FicheAutoControle.Conforme, FicheAutoControle.NonConforme, FicheAutoControle.NonApplicable];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/projets/{id:guid}/dalles/{did:guid}/auto-controle").WithTags("Suivi de chantier").RequireAuthorization();

        g.MapGet("/", async (Guid id, Guid did, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var (projet, dalle) = await Charger(db, id, did);
            return projet is null || dalle is null ? Results.NotFound() : Results.Ok(await Vue(db, projet, dalle, user));
        });

        g.MapPut("/", async (Guid id, Guid did, AutoControleInput input, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var (projet, dalle) = await Charger(db, id, did);
            if (projet is null || dalle is null) return Results.NotFound();
            if (!PeutSaisir(user)) return Results.Forbid();
            if (Verrou(projet, dalle) is { } verrou) return ProjetsEndpoints.Conflit(verrou);
            var points = (input.Points ?? []).Where(p => FicheAutoControle.Modele.Any(m => m.Code == p.Code)).ToList();
            if (points.Any(p => !string.IsNullOrEmpty(p.Resultat) && !Resultats.Contains(p.Resultat))) return ProjetsEndpoints.Conflit("Résultat de contrôle inconnu.");
            var f = await db.FichesAutoControle.FirstOrDefaultAsync(x => x.DalleId == did);
            if (f is null) db.FichesAutoControle.Add(f = new FicheAutoControle { DalleId = did });
            f.Points = FicheAutoControle.Modele.Select(m =>
            {
                var p = points.FirstOrDefault(x => x.Code == m.Code);
                return new PointControle { Code = m.Code, Resultat = p?.Resultat ?? "", Observation = string.IsNullOrWhiteSpace(p?.Observation) ? null : p!.Observation!.Trim() };
            }).ToList();
            f.SaisiPar = user.UserName();
            f.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(await Vue(db, projet, dalle, user));
        });
    }

    /// <summary>Tous les points doivent être renseignés avant de valider l'étape. Un point non conforme ne bloque pas : il est signalé.</summary>
    public static async Task<string?> RefusAsync(ProjetsDbContext db, Guid dalleId)
    {
        var f = await db.FichesAutoControle.AsNoTracking().FirstOrDefaultAsync(x => x.DalleId == dalleId);
        var manquants = FicheAutoControle.Modele.Count(m => string.IsNullOrEmpty(f?.Points.FirstOrDefault(p => p.Code == m.Code)?.Resultat));
        return manquants == 0 ? null : $"Fiche d'auto-contrôle incomplète : {manquants} point(s) restent à noter (conforme, non conforme ou non applicable).";
    }

    /// <summary>La fiche est remplie par le chantier ; le directeur et le chef de projet, qui contrôlent toutes les étapes, peuvent aussi la saisir.</summary>
    private static bool PeutSaisir(ClaimsPrincipal u) =>
        u.IsInRole(Roles.ChefChantier) || u.IsInRole(Roles.ResponsableChantier) || u.IsInRole(Roles.Directeur) || u.IsInRole(Roles.ChefProjet);

    private static async Task<(Projet?, Dalle?)> Charger(ProjetsDbContext db, Guid id, Guid did) =>
        (await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id), await db.Dalles.AsNoTracking().FirstOrDefaultAsync(d => d.Id == did && d.ProjetId == id));

    private static string? Verrou(Projet projet, Dalle dalle) =>
        projet.Supprime ? "Un projet supprimé est en lecture seule."
        : projet.Bloque ? "Projet bloqué : les étapes de chantier sont suspendues."
        : dalle.EtapesFaites >= Etape ? "La réception de dalle est validée : la fiche est figée."
        : dalle.EtapesFaites < Etape - 1 ? "La réception de dalle n'est pas encore en cours." : null;

    private static async Task<object> Vue(ProjetsDbContext db, Projet projet, Dalle dalle, ClaimsPrincipal user)
    {
        var f = await db.FichesAutoControle.AsNoTracking().FirstOrDefaultAsync(x => x.DalleId == dalle.Id);
        var verrou = Verrou(projet, dalle);
        return new
        {
            modifiable = verrou is null && PeutSaisir(user), verrou, saisiPar = f?.SaisiPar, saisiLe = f?.UpdatedAt ?? f?.CreatedAt,
            nonConformes = f?.Points.Count(p => p.Resultat == FicheAutoControle.NonConforme) ?? 0,
            points = FicheAutoControle.Modele.Select(m =>
            {
                var p = f?.Points.FirstOrDefault(x => x.Code == m.Code);
                return new { code = m.Code, section = m.Section, rubrique = m.Rubrique, libelle = m.Libelle, resultat = p?.Resultat ?? "", observation = p?.Observation };
            }),
        };
    }
}
