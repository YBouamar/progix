using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

public record CableInjectionInput(string? Cable, bool TestAir, decimal? Fluidite, decimal? Temperature, decimal? Pression, string? Depart, string? Fin);
public record CoulisInput(string? CimentType, decimal? CimentKg, decimal? CimentTemperature, string? Eau, decimal? EauLitres, string? AdjuvantType, decimal? AdjuvantGrammes);
public record FicheInjectionInput(decimal? AirAmbiant, string? Malaxeur, string? Pompe, string? CimentType, decimal? CimentKg, decimal? CimentTemperature,
    string? Eau, decimal? EauLitres, string? AdjuvantType, decimal? AdjuvantGrammes, List<CableInjectionInput>? Cables, string? ClientNom, string? ClientSignature);

/// <summary>Fiche d'injection d'une dalle (étape 10), enregistrement ENR-PT-04.</summary>
public static partial class InjectionEndpoints
{
    private const int SignatureMax = 400_000;

    [GeneratedRegex(@"^([01]\d|2[0-3]):[0-5]\d$")]
    private static partial Regex Heure();

    public static void Map(IEndpointRouteBuilder app)
    {
        // Composition du coulis proposée sur les nouvelles fiches d'injection.
        app.MapGet("/api/parametres/coulis", async (ProjetsDbContext db, ClaimsPrincipal user) =>
            Results.Ok(CoulisDto(await db.ParametresCoulis.AsNoTracking().FirstOrDefaultAsync() ?? new ParametresCoulis(), user))).WithTags("Paramétrage").RequireAuthorization();
        app.MapPut("/api/parametres/coulis", async (CoulisInput input, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            if (!(user.IsInRole(Roles.Directeur) || user.IsInRole(Roles.ChefProjet))) return Results.Forbid();
            if (input.CimentKg < 0 || input.EauLitres < 0 || input.AdjuvantGrammes < 0) return ProjetsEndpoints.Conflit("Les quantités ne peuvent pas être négatives.");
            var c = await db.ParametresCoulis.FirstOrDefaultAsync();
            if (c is null) db.ParametresCoulis.Add(c = new ParametresCoulis());
            c.CimentType = Clean(input.CimentType); c.CimentKg = input.CimentKg; c.CimentTemperature = input.CimentTemperature; c.Eau = Clean(input.Eau); c.EauLitres = input.EauLitres;
            c.AdjuvantType = Clean(input.AdjuvantType); c.AdjuvantGrammes = input.AdjuvantGrammes; c.ModifiePar = user.UserName(); c.ModifieLe = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(CoulisDto(c, user));
        }).WithTags("Paramétrage").RequireAuthorization();

        var g = app.MapGroup("/api/projets/{id:guid}/dalles/{did:guid}/injection").WithTags("Suivi de chantier").RequireAuthorization();

        g.MapGet("/", async (Guid id, Guid did, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var (projet, dalle) = await Charger(db, id, did);
            return projet is null || dalle is null ? Results.NotFound() : Results.Ok(await Vue(db, projet, dalle, user));
        });

        g.MapPut("/", async (Guid id, Guid did, FicheInjectionInput input, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var (projet, dalle) = await Charger(db, id, did);
            if (projet is null || dalle is null) return Results.NotFound();
            if (!PeutSaisir(user)) return Results.Forbid();
            if (await Verrou(db, projet, dalle) is { } verrou) return ProjetsEndpoints.Conflit(verrou);

            var cables = input.Cables ?? [];
            if (cables.Any(c => string.IsNullOrWhiteSpace(c.Cable))) return ProjetsEndpoints.Conflit("Chaque ligne doit porter le numéro du câble.");
            if (cables.GroupBy(c => c.Cable!.Trim().ToUpperInvariant()).Any(x => x.Count() > 1)) return ProjetsEndpoints.Conflit("Un câble figure deux fois dans la fiche.");
            if (cables.Any(c => c.Fluidite < 0 || c.Pression < 0)) return ProjetsEndpoints.Conflit("La fluidité et la pression ne peuvent pas être négatives.");
            if (cables.Any(c => (!string.IsNullOrWhiteSpace(c.Depart) && !Heure().IsMatch(c.Depart)) || (!string.IsNullOrWhiteSpace(c.Fin) && !Heure().IsMatch(c.Fin))))
                return ProjetsEndpoints.Conflit("Les heures de départ et de fin se saisissent au format HH:MM.");
            if (input.ClientSignature is { Length: > 0 } s && (!s.StartsWith("data:image/png;base64,") || s.Length > SignatureMax))
                return ProjetsEndpoints.Conflit("Signature du client invalide.");

            var f = await db.FichesInjection.FirstOrDefaultAsync(x => x.DalleId == did);
            if (f is null) db.FichesInjection.Add(f = new FicheInjection { DalleId = did });
            f.AirAmbiant = input.AirAmbiant;
            f.Malaxeur = Clean(input.Malaxeur);
            f.Pompe = Clean(input.Pompe);
            f.CimentType = Clean(input.CimentType);
            f.CimentKg = input.CimentKg;
            f.CimentTemperature = input.CimentTemperature;
            f.Eau = Clean(input.Eau);
            f.EauLitres = input.EauLitres;
            f.AdjuvantType = Clean(input.AdjuvantType);
            f.AdjuvantGrammes = input.AdjuvantGrammes;
            f.ClientNom = Clean(input.ClientNom);
            f.ClientSignature = string.IsNullOrEmpty(input.ClientSignature) ? null : input.ClientSignature;
            f.Cables = cables.Select(c => new CableInjection
            {
                Cable = c.Cable!.Trim().ToUpperInvariant(), TestAir = c.TestAir, Fluidite = c.Fluidite, Temperature = c.Temperature,
                Pression = c.Pression, Depart = Clean(c.Depart), Fin = Clean(c.Fin),
            }).ToList();
            f.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(await Vue(db, projet, dalle, user));
        });
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static object CoulisDto(ParametresCoulis c, ClaimsPrincipal user) => new
    {
        cimentType = c.CimentType, cimentKg = c.CimentKg, cimentTemperature = c.CimentTemperature, eau = c.Eau, eauLitres = c.EauLitres, adjuvantType = c.AdjuvantType, adjuvantGrammes = c.AdjuvantGrammes,
        modifiable = user.IsInRole(Roles.Directeur) || user.IsInRole(Roles.ChefProjet), modifiePar = c.ModifiePar, modifieLe = c.ModifieLe,
    };

    private static bool PeutSaisir(ClaimsPrincipal u) =>
        u.IsInRole(Roles.Directeur) || u.IsInRole(Roles.ChefProjet) || Etapes.Get(Etapes.Injection).Roles.Any(u.IsInRole);

    private static async Task<(Projet?, Dalle?)> Charger(ProjetsDbContext db, Guid id, Guid did) =>
        (await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id),
         await db.Dalles.AsNoTracking().FirstOrDefaultAsync(d => d.Id == did && d.ProjetId == id));

    private static async Task<string?> Verrou(ProjetsDbContext db, Projet projet, Dalle dalle)
    {
        if (projet.Supprime) return "Un projet supprimé est en lecture seule.";
        if (projet.Bloque) return "Projet bloqué : les étapes de chantier sont suspendues.";
        if (dalle.EtapesFaites >= Etapes.Injection) return "L'injection est validée : la fiche est figée.";
        if (dalle.EtapesFaites < Etapes.Injection - 1) return "L'injection n'est pas encore en cours.";
        var e = await db.Etapes.AsNoTracking().FirstOrDefaultAsync(x => x.DalleId == dalle.Id && x.Numero == Etapes.Injection);
        return e?.Validee == true ? null : "La validation technique et la validation administrative sont requises avant l'injection.";
    }

    /// <summary>Ce qui manque dans la fiche pour valider l'étape 10, ou null.</summary>
    public static async Task<string?> RefusAsync(ProjetsDbContext db, Guid dalleId)
    {
        var f = await db.FichesInjection.AsNoTracking().FirstOrDefaultAsync(x => x.DalleId == dalleId);
        if (f is null || f.Cables.Count == 0) return "Saisissez la fiche d'injection avant de valider l'étape.";
        var manque = new List<string>();
        if (f.AirAmbiant is null) manque.Add("la température de l'air ambiant");
        if (f.Malaxeur is null) manque.Add("le malaxeur");
        if (f.Pompe is null) manque.Add("la pompe");
        if (f.CimentType is null || f.CimentKg is null || f.EauLitres is null) manque.Add("la composition du coulis");
        var incomplets = f.Cables.Count(c => !c.Complet);
        if (incomplets > 0) manque.Add($"les mesures de {incomplets} câble(s)");
        if (f.ClientNom is null) manque.Add("le nom du signataire du client");
        if (f.ClientSignature is null) manque.Add("la signature du client");
        return manque.Count == 0 ? null : $"Fiche d'injection incomplète : il manque {string.Join(", ", manque)}.";
    }

    public static async Task SignerAsync(ProjetsDbContext db, Guid dalleId, string par)
    {
        var f = await db.FichesInjection.FirstAsync(x => x.DalleId == dalleId);
        f.SignePar = par;
        f.SigneLe = DateTime.UtcNow;
    }

    private static async Task<object> Vue(ProjetsDbContext db, Projet projet, Dalle dalle, ClaimsPrincipal user)
    {
        var f = await db.FichesInjection.AsNoTracking().FirstOrDefaultAsync(x => x.DalleId == dalle.Id);
        var verrou = await Verrou(db, projet, dalle);
        // Sans fiche, la liste des câbles reprend celle de la mise en tension.
        var reperes = await CommandeEndpoints.ReperesAsync(db, dalle.Id);
        var cables = f?.Cables ?? (await db.MesuresTension.AsNoTracking().Where(m => m.DalleId == dalle.Id).OrderBy(m => m.Ordre).Select(m => m.Cable).ToListAsync())
            .Select(c => new CableInjection { Cable = c }).ToList();
        if (f is null)
        {
            // Nouvelle fiche : la composition du coulis vient du Paramétrage.
            var c = await db.ParametresCoulis.AsNoTracking().FirstOrDefaultAsync() ?? new ParametresCoulis();
            f = new FicheInjection { CimentType = c.CimentType, CimentKg = c.CimentKg, CimentTemperature = c.CimentTemperature, Eau = c.Eau, EauLitres = c.EauLitres, AdjuvantType = c.AdjuvantType, AdjuvantGrammes = c.AdjuvantGrammes };
        }
        return new
        {
            modifiable = verrou is null && PeutSaisir(user), verrou,
            airAmbiant = f.AirAmbiant, malaxeur = f.Malaxeur, pompe = f.Pompe,
            cimentType = f.CimentType, cimentKg = f.CimentKg, cimentTemperature = f.CimentTemperature,
            eau = f.Eau, eauLitres = f.EauLitres, adjuvantType = f.AdjuvantType, adjuvantGrammes = f.AdjuvantGrammes,
            clientNom = f.ClientNom, clientSignature = f.ClientSignature, signePar = f.SignePar, signeLe = f.SigneLe,
            cables = cables.Select(c => new
            {
                cable = c.Cable, systeme = reperes.GetValueOrDefault(c.Cable)?.Systeme, sens = reperes.GetValueOrDefault(c.Cable)?.Sens, testAir = c.TestAir, fluidite = c.Fluidite, temperature = c.Temperature, pression = c.Pression, depart = c.Depart, fin = c.Fin,
            }),
        };
    }
}
