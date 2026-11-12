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
/// Statistiques de performance, en deux vues séparées : dalles dont les plans d'exécution sont terminés, par dessinateur,
/// et projets suivis, par ingénieur. Par mois et sur l'année.
/// </summary>
public static class PerformanceEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        // Deux pages, deux modules d'habilitation : la performance des dessinateurs et celle des ingénieurs.
        app.MapGet("/api/statistiques/performance/dessinateurs", (int? annee, ProjetsDbContext db, IUtilisateursLookup users, ClaimsPrincipal user) =>
            VueAsync(annee, dessinateurs: true, db, users, user)).WithTags("Statistiques").RequireAuthorization();
        app.MapGet("/api/statistiques/performance/ingenieurs", (int? annee, ProjetsDbContext db, IUtilisateursLookup users, ClaimsPrincipal user) =>
            VueAsync(annee, dessinateurs: false, db, users, user)).WithTags("Statistiques").RequireAuthorization();

        // Évaluation des dessinateurs : rendement par jour travaillé, score, rang et niveau (feuille « Évaluation » du classeur d'origine).
        app.MapGet("/api/statistiques/performance/dessinateurs/evaluation", async (int? annee, ProjetsDbContext db, IUtilisateursLookup users, ClaimsPrincipal user) =>
            VoitDessinateurs(user) ? Results.Ok(await EvaluationAsync(annee, db, users, user)) : Results.Forbid()).WithTags("Statistiques").RequireAuthorization();

        app.MapPut("/api/statistiques/performance/dessinateurs/evaluation/parametres", async (ParametresEvaluationInput input, int? annee, ProjetsDbContext db, IUtilisateursLookup users, ClaimsPrincipal user) =>
        {
            if (!Direction(user)) return Results.Forbid();
            var p = await db.ParametresEvaluation.FirstOrDefaultAsync();
            if (p is null) db.ParametresEvaluation.Add(p = new ParametresEvaluation());
            p.JoursParMois = input.JoursParMois; p.JoursReference = input.JoursReference;
            p.SeuilExcellent = input.SeuilExcellent; p.SeuilBon = input.SeuilBon; p.SeuilMoyen = input.SeuilMoyen; p.MoisMinimum = input.MoisMinimum;
            p.PoidsSurface = input.PoidsSurface; p.PoidsDossiers = input.PoidsDossiers; p.ObjectifSurface = input.ObjectifSurface; p.ObjectifDossiers = input.ObjectifDossiers;
            if (p.Refus() is { } refus) return ProjetsEndpoints.Conflit(refus);
            p.ModifiePar = user.UserName(); p.ModifieLe = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(await EvaluationAsync(annee, db, users, user));
        }).WithTags("Statistiques").RequireAuthorization();
    }

    public record ParametresEvaluationInput(decimal JoursParMois, decimal JoursReference, decimal SeuilExcellent, decimal SeuilBon, decimal SeuilMoyen, decimal MoisMinimum,
        decimal PoidsSurface, decimal PoidsDossiers, decimal ObjectifSurface, decimal ObjectifDossiers);

    private static bool Direction(ClaimsPrincipal u) => u.IsInRole(Roles.Directeur) || u.IsInRole(Roles.ChefProjet);
    private static bool VoitDessinateurs(ClaimsPrincipal u) => Direction(u) || u.IsInRole(Roles.DessinateurChef);

    private static async Task<object> EvaluationAsync(int? annee, ProjetsDbContext db, IUtilisateursLookup users, ClaimsPrincipal user)
    {
        var jour = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(1));
        var an = annee is >= 2000 and <= 2100 ? annee.Value : jour.Year;
        var p = await db.ParametresEvaluation.AsNoTracking().FirstOrDefaultAsync() ?? new ParametresEvaluation();

        // Dossiers terminés dans l'année, hors projets supprimés : le premier dossier d'une dalle est un EXE, les suivants des MODIF.
        // Un dossier partagé compte à parts égales pour chacun de ses dessinateurs, dossier et surface.
        var dossiers = await (from v in db.VersionsPlans.AsNoTracking()
                              join d in db.Dalles.AsNoTracking() on v.DalleId equals d.Id
                              join pr in db.Projets.AsNoTracking() on d.ProjetId equals pr.Id
                              where !pr.Supprime && v.Etat == ExecutionDalle.Termine && v.TermineLe != null
                              select new { v.TermineLe, v.Surface, v.DessinateursIds, v.DessinateursNoms, Premier = !db.VersionsPlans.Any(w => w.DalleId == v.DalleId && w.Lettre < v.Lettre) }).ToListAsync();
        var parts = new Dictionary<Guid, (decimal NbModif, decimal SurfaceModif, decimal NbExe, decimal SurfaceExe)>();
        var nomsReleves = new Dictionary<Guid, string>();
        foreach (var x in dossiers.Where(x => x.TermineLe!.Value.AddHours(1).Year == an))
        {
            var ids = ExecutionDalle.Ids(x.DessinateursIds);
            var noms = (x.DessinateursNoms ?? "").Split(" + ", StringSplitOptions.RemoveEmptyEntries);
            var part = ExecutionDalle.Part(ids.Count);
            for (var i = 0; i < ids.Count; i++)
            {
                var t = parts.GetValueOrDefault(ids[i]);
                parts[ids[i]] = x.Premier ? (t.NbModif, t.SurfaceModif, t.NbExe + part, t.SurfaceExe + (x.Surface ?? 0) * part)
                    : (t.NbModif + part, t.SurfaceModif + (x.Surface ?? 0) * part, t.NbExe, t.SurfaceExe);
                if (i < noms.Length) nomsReleves.TryAdd(ids[i], noms[i]);
            }
        }

        // Personnes évaluées : les dessinateurs, plus toute personne qui a terminé un dossier dans l'année (dessinateur en chef compris).
        var presences = (await users.PresencesAsync([Roles.Dessinateur, Roles.DessinateurChef])).GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.First());
        var personnes = presences.Values.Where(x => x.Role == Roles.Dessinateur || parts.ContainsKey(x.Id))
            // Une personne partie avant l'année, ou arrivée après, n'y figure pas.
            .Where(x => parts.ContainsKey(x.Id) || ((x.Depart is not DateOnly d || d.Year >= an) && (x.Arrivee is not DateOnly a || a.Year <= an) && (x.Actif || x.Depart is not null)))
            .Select(x =>
            {
                var t = parts.GetValueOrDefault(x.Id);
                var partie = !x.Actif || (x.Depart is DateOnly d && d <= jour);
                return new EvaluationDessinateurs.Personne(x.Id, x.Nom, partie, x.Arrivee, x.Depart,
                    // Compte désactivé sans date de départ : on ne sait pas jusqu'où compter les mois.
                    !x.Actif && x.Depart is null ? null : EvaluationDessinateurs.Mois(x.Arrivee, x.Depart, an, jour), t.NbModif, t.SurfaceModif, t.NbExe, t.SurfaceExe);
            }).ToList();
        personnes.AddRange(parts.Where(x => !presences.ContainsKey(x.Key))
            .Select(x => new EvaluationDessinateurs.Personne(x.Key, nomsReleves.GetValueOrDefault(x.Key, "Personne inconnue"), false, null, null, null, x.Value.NbModif, x.Value.SurfaceModif, x.Value.NbExe, x.Value.SurfaceExe)));

        var r = EvaluationDessinateurs.Calculer(personnes, p);
        object Ligne(EvaluationDessinateurs.Ligne l) => new
        {
            id = l.P.Id, nom = l.P.Nom, partie = l.P.Partie, arrivee = l.P.Arrivee, depart = l.P.Depart, mois = l.P.Mois, jours = l.Jours,
            nbModif = l.P.NbModif, surfaceModif = l.P.SurfaceModif, nbExe = l.P.NbExe, surfaceExe = l.P.SurfaceExe,
            modifParJour = l.ModifParJour, exeParJour = l.ExeParJour, surfaceModifParJour = l.SurfaceModifParJour, surfaceExeParJour = l.SurfaceExeParJour,
            dossiersParJour = l.DossiersParJour, surfaceParJour = l.SurfaceParJour, surfaceParDossier = l.SurfaceParDossier, score = l.Score, rang = l.Rang, niveau = l.Niveau,
        };
        return new
        {
            annee = an, modifiable = Direction(user),
            parametres = new
            {
                joursParMois = p.JoursParMois, joursReference = p.JoursReference, seuilExcellent = p.SeuilExcellent, seuilBon = p.SeuilBon, seuilMoyen = p.SeuilMoyen, moisMinimum = p.MoisMinimum,
                poidsSurface = p.PoidsSurface, poidsDossiers = p.PoidsDossiers, objectifSurface = p.ObjectifSurface, objectifDossiers = p.ObjectifDossiers, modifiePar = p.ModifiePar, modifieLe = p.ModifieLe,
            },
            medianeDossiers = r.MedianeDossiers, medianeSurface = r.MedianeSurface,
            enCours = r.EnCours.Select(Ligne), parties = r.Parties.Select(Ligne),
            repartition = EvaluationDessinateurs.Niveaux.Select(n => new { niveau = n, nb = r.EnCours.Count(l => l.Niveau == n) }),
        };
    }

    private static async Task<IResult> VueAsync(int? annee, bool dessinateurs, ProjetsDbContext db, IUtilisateursLookup users, ClaimsPrincipal user)
    {
        {
            var direction = user.IsInRole(Roles.Directeur) || user.IsInRole(Roles.ChefProjet);
            var voitIngenieurs = !dessinateurs && (direction || user.IsInRole(Roles.IngenieurChef));
            var voitDessinateurs = dessinateurs && (direction || user.IsInRole(Roles.DessinateurChef));
            if (!voitIngenieurs && !voitDessinateurs) return Results.Forbid();

            var jour = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(1));
            var an = annee is >= 2000 and <= 2100 ? annee.Value : jour.Year;
            var vivants = await db.Projets.AsNoTracking().Where(p => !p.Supprime).Select(p => p.Id).ToListAsync();

            object? parDessinateur = null, parIngenieur = null;
            // Production année par année, toutes personnes confondues, pour le graphe d'évolution.
            List<object>? dessinateursParAn = null, ingenieursParAn = null;
            var premiere = jour.Year;

            if (voitDessinateurs)
            {
                // Une dalle est finie pour son dessinateur quand l'étape « Plans d'exécution » est validée.
                var faites = await (from e in db.Etapes.AsNoTracking()
                                    join d in db.Dalles.AsNoTracking() on e.DalleId equals d.Id
                                    where e.Numero == 1 && e.FaitLe != null && d.DessinateurId != null && vivants.Contains(d.ProjetId)
                                    select new { d.DessinateurId, d.DessinateurNom, e.FaitLe, d.Surface }).ToListAsync();
                var lignes = faites.Select(x => new { Id = x.DessinateurId!.Value, Nom = x.DessinateurNom ?? "", Le = x.FaitLe!.Value.AddHours(1), x.Surface }).ToList();
                if (lignes.Count > 0) premiere = Math.Min(premiere, lignes.Min(x => x.Le.Year));
                var noms = (await users.ParRolesAsync([Roles.Dessinateur, Roles.DessinateurChef])).ToDictionary(u => u.Id, u => u.Nom);
                foreach (var l in lignes) noms.TryAdd(l.Id, l.Nom);
                parDessinateur = noms.Select(n =>
                {
                    var siennes = lignes.Where(l => l.Id == n.Key && l.Le.Year == an).ToList();
                    return new
                    {
                        id = n.Key, nom = n.Value, mois = Enumerable.Range(1, 12).Select(m => siennes.Count(l => l.Le.Month == m)).ToArray(),
                        total = siennes.Count, surface = siennes.Sum(l => l.Surface),
                    };
                }).OrderByDescending(x => x.total).ThenBy(x => x.nom).ToList();
                dessinateursParAn = Enumerable.Range(premiere, jour.Year - premiere + 1)
                    .Select(a => (object)new { annee = a, total = lignes.Count(l => l.Le.Year == a), surface = lignes.Where(l => l.Le.Year == a).Sum(l => l.Surface) }).ToList();
            }

            if (voitIngenieurs)
            {
                // Un projet compte pour un ingénieur chaque mois où il y est affecté, ne serait-ce qu'un jour.
                var aff = (await db.Affectations.AsNoTracking().Where(a => a.Fonction == Fonctions.Ingenieur).ToListAsync()).Where(a => vivants.Contains(a.ProjetId)).ToList();
                if (aff.Count > 0) premiere = Math.Min(premiere, aff.Min(a => a.Debut.Year));
                var noms = (await users.ParRolesAsync([Roles.Ingenieur, Roles.IngenieurChef])).ToDictionary(u => u.Id, u => u.Nom);
                foreach (var a in aff) noms.TryAdd(a.UserId, a.UserNom);
                // Surface suivie : celle des dalles des projets de l'ingénieur sur l'année, le même ensemble que la colonne « Projets ».
                var dalles = await db.Dalles.AsNoTracking().Where(d => vivants.Contains(d.ProjetId)).Select(d => new { d.ProjetId, d.Surface }).ToListAsync();
                List<Guid> ProjetsSuivis(Guid id, DateOnly du, DateOnly au) =>
                    du > jour ? [] : aff.Where(a => a.UserId == id && a.Debut <= au && (a.Fin ?? jour) >= du).Select(a => a.ProjetId).Distinct().ToList();
                parIngenieur = noms.Select(n =>
                {
                    var projetsAnnee = ProjetsSuivis(n.Key, new DateOnly(an, 1, 1), new DateOnly(an, 12, 31));
                    return new
                    {
                        id = n.Key, nom = n.Value,
                        mois = Enumerable.Range(1, 12).Select(m => ProjetsSuivis(n.Key, new DateOnly(an, m, 1), new DateOnly(an, m, 1).AddMonths(1).AddDays(-1)).Count).ToArray(),
                        total = projetsAnnee.Count,
                        surface = dalles.Where(d => projetsAnnee.Contains(d.ProjetId)).Sum(d => d.Surface),
                        enCours = aff.Where(a => a.UserId == n.Key && a.Fin == null).Select(a => a.ProjetId).Distinct().Count(),
                    };
                }).OrderByDescending(x => x.total).ThenBy(x => x.nom).ToList();
                // Projets suivis dans l'année par au moins un ingénieur, chacun compté une fois.
                ingenieursParAn = Enumerable.Range(premiere, jour.Year - premiere + 1).Select(a =>
                {
                    var projets = noms.Keys.SelectMany(id => ProjetsSuivis(id, new DateOnly(a, 1, 1), new DateOnly(a, 12, 31))).Distinct().ToList();
                    return (object)new { annee = a, total = projets.Count, surface = dalles.Where(d => projets.Contains(d.ProjetId)).Sum(d => d.Surface) };
                }).ToList();
            }

            return Results.Ok(new { annee = an, annees = Enumerable.Range(premiere, jour.Year - premiere + 1).Reverse(), moisCourant = an == jour.Year ? jour.Month : (int?)null, dessinateurs = parDessinateur, ingenieurs = parIngenieur, dessinateursParAn, ingenieursParAn });
        }
    }
}
