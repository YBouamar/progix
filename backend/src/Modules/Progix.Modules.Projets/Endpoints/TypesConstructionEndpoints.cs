using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

public record TypeConstructionInput(string? Code, string? Libelle, bool Actif, decimal? Forfait = null);
public record TypesConstructionInput(List<TypeConstructionInput>? Types);

/// <summary>Types de construction proposés à la création d'un projet : liste ordonnée, réglée par les fonctions qui ont l'écriture sur le module « Types de construction » (Paramétrage, ou boutons de la fiche du projet).</summary>
public static class TypesConstructionEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/parametres/types-construction").WithTags("Paramétrage").RequireAuthorization();
        g.MapGet("/", async (ProjetsDbContext db, IHabilitations habilitations, ClaimsPrincipal user) => Results.Ok(await Vue(db, await Modifiable(habilitations, user))));
        // L'écriture est réservée par les habilitations (module « Types de construction »), pour toute la route.
        g.MapPut("/", async (TypesConstructionInput input, ProjetsDbContext db, IHabilitations habilitations, ClaimsPrincipal user) =>
        {
            var liste = (input.Types ?? []).Select(t => t with { Libelle = t.Libelle?.Trim() }).ToList();
            if (liste.Any(t => string.IsNullOrEmpty(t.Libelle))) return ProjetsEndpoints.Conflit("Chaque type a un libellé.");
            if (liste.Any(t => t.Libelle!.Length > 60)) return ProjetsEndpoints.Conflit("Un libellé compte au plus 60 caractères.");
            if (liste.GroupBy(t => t.Libelle!.ToUpperInvariant()).FirstOrDefault(x => x.Count() > 1) is { } double_)
                return ProjetsEndpoints.Conflit($"Le type « {double_.First().Libelle} » figure deux fois.");
            if (liste.Any(t => t.Forfait is < 0 or > 100000)) return ProjetsEndpoints.Conflit("Le forfait d'un type est un nombre positif (DH/m²), ou vide pour suivre la grille.");
            if (!liste.Any(t => t.Actif)) return ProjetsEndpoints.Conflit("Au moins un type doit rester proposé.");
            var existants = await db.TypesConstruction.ToListAsync();
            if (liste.Any(t => t.Code is { } c && existants.All(e => e.Code != c))) return ProjetsEndpoints.Conflit("Un type de la liste n'existe plus : rechargez la page.");
            // Un type porté par un projet ne se supprime pas : il se désactive, et le projet garde son libellé.
            var retires = existants.Where(e => liste.All(t => t.Code != e.Code)).ToList();
            var codesRetires = retires.Select(r => r.Code).ToList();
            var utilises = await db.Projets.Where(p => codesRetires.Contains(p.TypeConstruction)).GroupBy(p => p.TypeConstruction).Select(x => new { x.Key, Nb = x.Count() }).ToListAsync();
            if (utilises.FirstOrDefault() is { } u)
                return ProjetsEndpoints.Conflit($"Le type « {retires.First(r => r.Code == u.Key).Libelle} » est porté par {u.Nb} projet(s) : décochez « Proposé » au lieu de le supprimer.");
            db.TypesConstruction.RemoveRange(retires);
            var pris = existants.Select(e => e.Code).ToHashSet();
            for (var i = 0; i < liste.Count; i++)
            {
                var t = existants.FirstOrDefault(e => e.Code == liste[i].Code);
                if (t is null) db.TypesConstruction.Add(t = new TypeConstructionParam { Code = NouveauCode(liste[i].Libelle!, pris) });
                t.Libelle = liste[i].Libelle!;
                t.Actif = liste[i].Actif;
                t.ForfaitFixe = liste[i].Forfait is > 0 ? Math.Round(liste[i].Forfait!.Value, 2) : null;
                t.Ordre = i;
                t.UpdatedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
            TypesConstruction.Charger(await db.TypesConstruction.AsNoTracking().ToListAsync());
            return Results.Ok(await Vue(db, true));
        });
    }

    /// <summary>Code stable d'un nouveau type, tiré de son libellé : « Bâtiment industriel » donne « batiment_industriel ».</summary>
    internal static string NouveauCode(string libelle, HashSet<string> pris)
    {
        var sb = new StringBuilder();
        foreach (var c in libelle.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsAsciiLetterOrDigit(c) ? c : '_');
        }
        var baseCode = string.Join('_', sb.ToString().Split('_', StringSplitOptions.RemoveEmptyEntries));
        if (baseCode.Length == 0) baseCode = "type";
        if (baseCode.Length > 34) baseCode = baseCode[..34];
        var code = baseCode;
        for (var n = 2; !pris.Add(code); n++) code = $"{baseCode}_{n}";
        return code;
    }

    private static async Task<bool> Modifiable(IHabilitations habilitations, ClaimsPrincipal user) =>
        await habilitations.NiveauAsync(user, Habilitations.TypesConstruction) >= Habilitations.Ecriture;

    private static async Task<object> Vue(ProjetsDbContext db, bool modifiable)
    {
        var types = await db.TypesConstruction.AsNoTracking().OrderBy(t => t.Ordre).ToListAsync();
        var parType = await db.Projets.GroupBy(p => p.TypeConstruction).Select(x => new { x.Key, Nb = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Nb);
        return new
        {
            modifiable,
            types = types.Select(t => new { code = t.Code, libelle = t.Libelle, actif = t.Actif, forfait = t.ForfaitFixe, projets = parType.GetValueOrDefault(t.Code) }),
        };
    }
}
