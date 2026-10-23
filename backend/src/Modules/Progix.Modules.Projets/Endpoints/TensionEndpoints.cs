using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

public record MesureInput(string? Cable, decimal? Longueur, decimal Theorique, decimal? A25, decimal? A100, decimal? B25, decimal? B100);
public record FicheTensionInput(List<MesureInput>? Cables, decimal? ForceKn = null, string? Cr = null, string? Cmp = null);
public record ParametresTensionInput(decimal ForceKn, decimal SectionMm2, decimal ModuleMpa, decimal PertesPct);

/// <summary>Fiche de mise en tension d'une dalle (étape 9).</summary>
public static class TensionEndpoints
{
    public static async Task<ParametresTension> ParametresAsync(ProjetsDbContext db) =>
        await db.ParametresTension.AsNoTracking().FirstOrDefaultAsync() ?? new ParametresTension();

    private static object Dto(ParametresTension p, ClaimsPrincipal user) => new
    {
        forceKn = p.ForceKn, sectionMm2 = p.SectionMm2, moduleMpa = p.ModuleMpa, pertesPct = p.PertesPct, mmParMetre = p.MmParMetre,
        modifiePar = p.ModifiePar, modifieLe = p.ModifieLe, modifiable = user.IsInRole(Roles.Directeur) || user.IsInRole(Roles.ChefProjet),
    };

    public static void Map(IEndpointRouteBuilder app)
    {
        var param = app.MapGroup("/api/parametres/tension").WithTags("Paramétrage").RequireAuthorization();
        param.MapGet("/", async (ProjetsDbContext db, ClaimsPrincipal user) => Results.Ok(Dto(await ParametresAsync(db), user)));
        param.MapPut("/", async (ParametresTensionInput input, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            if (input.ForceKn <= 0 || input.SectionMm2 <= 0 || input.ModuleMpa <= 0) return ProjetsEndpoints.Conflit("La force, la section et le module doivent être supérieurs à 0.");
            if (input.PertesPct is < 0 or >= 100) return ProjetsEndpoints.Conflit("Les pertes par frottement sont comprises entre 0 et 100 %.");
            var p = await db.ParametresTension.FirstOrDefaultAsync();
            if (p is null) db.ParametresTension.Add(p = new ParametresTension());
            p.ForceKn = input.ForceKn; p.SectionMm2 = input.SectionMm2; p.ModuleMpa = input.ModuleMpa; p.PertesPct = input.PertesPct;
            p.ModifiePar = user.UserName(); p.ModifieLe = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(Dto(p, user));
        }).RequireAuthorization(Policies.Direction);

        var g = app.MapGroup("/api/projets/{id:guid}/dalles/{did:guid}/tension").WithTags("Suivi de chantier").RequireAuthorization();

        g.MapGet("/", async (Guid id, Guid did, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var (projet, dalle) = await Charger(db, id, did);
            return projet is null || dalle is null ? Results.NotFound() : Results.Ok(await Vue(db, projet, dalle, user));
        });

        g.MapPut("/", async (Guid id, Guid did, FicheTensionInput input, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var (projet, dalle) = await Charger(db, id, did);
            if (projet is null || dalle is null) return Results.NotFound();
            if (!PeutSaisir(user)) return Results.Forbid();
            if (await Verrou(db, projet, dalle) is { } verrou) return ProjetsEndpoints.Conflit(verrou);

            var cables = input.Cables ?? [];
            if (cables.Any(c => string.IsNullOrWhiteSpace(c.Cable))) return ProjetsEndpoints.Conflit("Chaque ligne doit porter le nom du câble.");
            if (cables.Any(c => c.Theorique <= 0)) return ProjetsEndpoints.Conflit("L'allongement théorique de chaque câble doit être supérieur à 0.");
            if (cables.Any(c => c.Longueur <= 0)) return ProjetsEndpoints.Conflit("La longueur d'un câble doit être supérieure à 0.");
            if (cables.Any(c => c.A25 < 0 || c.A100 < 0 || c.B25 < 0 || c.B100 < 0)) return ProjetsEndpoints.Conflit("Une lecture ne peut pas être négative.");
            if (cables.Any(c => c.A100 < c.A25 || c.B100 < c.B25)) return ProjetsEndpoints.Conflit("La lecture à 100 % ne peut pas être inférieure à la lecture à 25 %.");
            if (cables.GroupBy(c => c.Cable!.Trim().ToUpperInvariant()).Any(x => x.Count() > 1)) return ProjetsEndpoints.Conflit("Un câble figure deux fois dans la fiche.");

            if (input.ForceKn is <= 0) return ProjetsEndpoints.Conflit("La force doit être supérieure à 0.");
            var entete = await db.FichesTension.FirstOrDefaultAsync(f => f.DalleId == did);
            if (entete is null) db.FichesTension.Add(entete = new FicheTension { DalleId = did });
            entete.ForceKn = input.ForceKn; entete.UpdatedAt = DateTime.UtcNow;
            entete.Cr = string.IsNullOrWhiteSpace(input.Cr) ? null : input.Cr.Trim(); entete.Cmp = string.IsNullOrWhiteSpace(input.Cmp) ? null : input.Cmp.Trim();
            db.MesuresTension.RemoveRange(db.MesuresTension.Where(m => m.DalleId == did));
            db.MesuresTension.AddRange(cables.Select((c, i) => new MesureTension
            {
                DalleId = did, Ordre = i, Cable = c.Cable!.Trim().ToUpperInvariant(), Longueur = c.Longueur, Theorique = c.Theorique,
                A25 = c.A25, A100 = c.A100, B25 = c.B25, B100 = c.B100,
            }));
            await db.SaveChangesAsync();
            return Results.Ok(await Vue(db, projet, dalle, user));
        });
    }

    private static bool PeutSaisir(ClaimsPrincipal u) =>
        u.IsInRole(Roles.Directeur) || u.IsInRole(Roles.ChefProjet) || Etapes.Get(Etapes.MiseEnTension).Roles.Any(u.IsInRole);

    private static async Task<(Projet?, Dalle?)> Charger(ProjetsDbContext db, Guid id, Guid did) =>
        (await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id),
         await db.Dalles.AsNoTracking().FirstOrDefaultAsync(d => d.Id == did && d.ProjetId == id));

    /// <summary>La fiche se saisit pendant l'étape 9, une fois les deux validations données.</summary>
    private static async Task<string?> Verrou(ProjetsDbContext db, Projet projet, Dalle dalle)
    {
        if (projet.Supprime) return "Un projet supprimé est en lecture seule.";
        if (projet.Bloque) return "Projet bloqué : les étapes de chantier sont suspendues.";
        if (dalle.EtapesFaites >= Etapes.MiseEnTension) return "La mise en tension est validée : la fiche est figée.";
        if (dalle.EtapesFaites < Etapes.MiseEnTension - 1) return "La mise en tension n'est pas encore en cours.";
        var e = await db.Etapes.AsNoTracking().FirstOrDefaultAsync(x => x.DalleId == dalle.Id && x.Numero == Etapes.MiseEnTension);
        return e?.Validee == true ? null : "Les validations technique, administrative et du responsable des chantiers sont requises avant la saisie des mesures.";
    }

    internal sealed record Synthese(decimal Theorique, decimal Mesure, decimal? Ecart, bool? Conforme, int HorsTolerance, int Incompletes, int Total);

    internal static Synthese Calculer(IReadOnlyList<MesureTension> m)
    {
        var completes = m.Where(x => x.Mesure != null).ToList();
        var theo = completes.Sum(x => x.Theorique);
        var mes = completes.Sum(x => x.Mesure!.Value);
        decimal? ecart = theo > 0 ? Math.Round((mes - theo) / theo * 100, 1) : null;
        // EN 13670 : le groupe a sa propre tolérance, plus serrée que celle d'un câble seul (± 5 % contre ± 15 %).
        return new Synthese(theo, mes, ecart, theo > 0 ? mes >= theo * MesureTension.GroupeBas && mes <= theo * MesureTension.GroupeHaut : null,
            completes.Count(x => x.Conforme == false), m.Count - completes.Count, m.Count);
    }

    /// <summary>Ce qui empêche de valider l'étape 9, ou null. Hors tolérance, la validation reste possible avec une remarque.</summary>
    public static async Task<string?> RefusAsync(ProjetsDbContext db, Guid dalleId, string? remarque)
    {
        var s = Calculer(await db.MesuresTension.AsNoTracking().Where(m => m.DalleId == dalleId).ToListAsync());
        if (s.Total == 0) return "Saisissez la fiche de mise en tension avant de valider l'étape.";
        if (s.Incompletes > 0) return $"Il manque des lectures pour {s.Incompletes} câble(s) dans la fiche de mise en tension : côté A complet, et côté B complet s'il est commencé.";
        if ((s.HorsTolerance > 0 || s.Conforme == false) && remarque is null)
            return "Des mesures sont hors tolérance : une remarque décrivant l'investigation est obligatoire pour valider l'étape.";
        return null;
    }

    private static async Task<object> Vue(ProjetsDbContext db, Projet projet, Dalle dalle, ClaimsPrincipal user)
    {
        var mesures = await db.MesuresTension.AsNoTracking().Where(m => m.DalleId == dalle.Id).OrderBy(m => m.Ordre).ToListAsync();
        var parametres = await ParametresAsync(db);
        // Sans fiche, la liste des câbles vient de la commande usine, avec le théorique calculé sur la longueur.
        var proposes = false;
        if (mesures.Count == 0 && await db.CommandesUsine.AsNoTracking().FirstOrDefaultAsync(c => c.DalleId == dalle.Id) is { } commande)
        {
            mesures = commande.Cables().Select((c, i) => new MesureTension
            {
                DalleId = dalle.Id, Ordre = i, Cable = c.Nom, Longueur = c.Longueur, Theorique = Math.Round(c.Longueur * parametres.MmParMetre, 1),
            }).ToList();
            proposes = mesures.Count > 0;
        }
        var reperes = await CommandeEndpoints.ReperesAsync(db, dalle.Id);
        var verrou = await Verrou(db, projet, dalle);
        var entete = await db.FichesTension.AsNoTracking().FirstOrDefaultAsync(f => f.DalleId == dalle.Id);
        var s = Calculer(mesures);
        return new
        {
            modifiable = verrou is null && PeutSaisir(user),
            verrou,
            mmParMetre = parametres.MmParMetre, proposes,
            borneBasse = MesureTension.BorneBasse, borneHaute = MesureTension.BorneHaute, groupeBas = MesureTension.GroupeBas, groupeHaut = MesureTension.GroupeHaut,
            // La force se saisit par fiche ; elle est proposée à la valeur du Paramétrage.
            forceKn = entete?.ForceKn ?? parametres.ForceKn, cr = entete?.Cr, cmp = entete?.Cmp,
            cables = mesures.Select(m => new
            {
                cable = m.Cable, systeme = reperes.GetValueOrDefault(m.Cable)?.Systeme, sens = reperes.GetValueOrDefault(m.Cable)?.Sens, longueur = m.Longueur, theorique = m.Theorique, a25 = m.A25, a100 = m.A100, b25 = m.B25, b100 = m.B100,
                mesure = m.Mesure, ecart = m.Ecart, conforme = m.Conforme,
            }),
            groupe = new { theorique = s.Theorique, mesure = s.Mesure, ecart = s.Ecart, conforme = s.Conforme, horsTolerance = s.HorsTolerance, incompletes = s.Incompletes },
        };
    }
}
