using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using Progix.Modules.Projets.Application;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

/// <summary>
/// Génération du fond de plan d'une étude à partir du plan de coffrage déposé : l'ingénieur choisit le niveau,
/// confirme le rôle des calques, regarde l'aperçu, puis dépose le fichier produit comme pièce « fond de plan ».
/// </summary>
public static class FondPlanEndpoints
{
    public record Repere(double X, double Y);

    /// <param name="FichierId">Plan de coffrage déposé sur l'étude.</param>
    /// <param name="Calques">Rôles confirmés ou corrigés ; les calques absents gardent le rôle proposé.</param>
    /// <param name="Niveau">Repère du niveau choisi (centre de sa zone) ; absent : le niveau unique, s'il n'y en a qu'un.</param>
    public record Input(Guid FichierId, Dictionary<string, string>? Calques, Repere? Niveau);

    private sealed record Analyse(PlanCoffrage Plan, Dictionary<string, string> Roles, List<(NiveauPlan Niveau, FondPlanNiveau Fond)> Plans, (NiveauPlan Niveau, FondPlanNiveau Fond)? Choisi);

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/etudes/{dalleId:guid}/fond-plan").WithTags("Étude").RequireAuthorization();

        // Niveaux détectés, rôle de chaque calque et aperçu du niveau choisi. Rien n'est enregistré.
        g.MapPost("/analyse", async (Guid dalleId, Input input, ProjetsDbContext db, IFichiersContenu fichiers, IEnumerable<IContexteFichiers> contextes,
            IMemoryCache cache, ILoggerFactory journaux, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var (a, refus) = await AnalyserAsync(dalleId, input, db, fichiers, contextes, cache, journaux, user, ct);
            if (a is null) return refus!;
            var zone = a.Choisi?.Niveau.Zone;
            int Dans(string calque) => zone is null ? 0
                : a.Plan.Contours.Count(c => c.Calque == calque && zone.Contains(c.Polygone.InteriorPoint.Coordinate)) + a.Plan.Ronds.Count(r => r.Calque == calque && zone.Contains(r.X, r.Y));
            var calques = a.Roles.Select(r => new
            {
                nom = r.Key, role = r.Value, propose = FondPlan.RolePropose(r.Key),
                formes = a.Plan.Contours.Count(c => c.Calque == r.Key) + a.Plan.Ronds.Count(x => x.Calque == r.Key), dansNiveau = Dans(r.Key),
            }).OrderByDescending(c => c.role != FondPlan.Ignore).ThenByDescending(c => c.dansNiveau).ThenByDescending(c => c.formes).ThenBy(c => c.nom).ToList();
            return Results.Ok(new
            {
                roles = FondPlan.Roles.Select(r => new { code = r.Code, label = r.Label }),
                calques,
                niveaux = a.Plans.Select(p => new
                {
                    x = p.Niveau.X, y = p.Niveau.Y, nom = p.Niveau.Nom, surface = Math.Round(p.Fond.Dalle.Area, 2),
                    poteaux = p.Fond.Poteaux.Count + p.Fond.Ronds.Count, voiles = p.Fond.Voiles.Count, reservations = p.Fond.Reservations.Count,
                    choisi = a.Choisi is { } c && ReferenceEquals(c.Niveau, p.Niveau),
                }),
                apercu = a.Choisi is { } ch ? Apercu(ch.Fond) : null,
            });
        });

        // Écrit le fond de plan du niveau choisi et le dépose comme pièce de l'étude.
        g.MapPost("/deposer", async (Guid dalleId, Input input, ProjetsDbContext db, IFichiersContenu fichiers, IEnumerable<IContexteFichiers> contextes,
            IMemoryCache cache, ILoggerFactory journaux, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var (a, refus) = await AnalyserAsync(dalleId, input, db, fichiers, contextes, cache, journaux, user, ct);
            if (a is null) return refus!;
            if (a.Choisi is not { } choisi) return ProjetsEndpoints.Conflit("Choisissez le niveau dont il faut produire le fond de plan.");
            using var tampon = new MemoryStream();
            PlanCoffrageDwg.Ecrire(choisi.Fond, tampon);
            using var contenu = new MemoryStream(tampon.ToArray());
            var nom = $"Fond de plan {NomFichier(choisi.Niveau.Nom)}.dxf";
            var f = await fichiers.DeposerAsync(EtudeDalle.Contexte, dalleId, EtudeDalle.FondPlan, nom, "application/dxf", contenu, user, ct);
            return Results.Ok(new { id = f.Id, nom = f.Nom, taille = f.Taille });
        });
    }

    /// <summary>Contrôle les droits, lit le plan de coffrage (gardé en mémoire un quart d'heure) et applique les rôles demandés.</summary>
    private static async Task<(Analyse? Analyse, IResult? Refus)> AnalyserAsync(Guid dalleId, Input input, ProjetsDbContext db, IFichiersContenu fichiers,
        IEnumerable<IContexteFichiers> contextes, IMemoryCache cache, ILoggerFactory journaux, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await db.Dalles.AsNoTracking().AnyAsync(d => d.Id == dalleId, ct)) return (null, Results.NotFound());
        // Produire le fond de plan revient à déposer une pièce : mêmes droits.
        var ctx = contextes.First(c => c.Contexte == EtudeDalle.Contexte);
        if (!ctx.PeutDeposer(user)) return (null, Results.Forbid());
        if (await ctx.RefusPourAsync(user, dalleId, EtudeDalle.FondPlan, ct) is { } refus) return (null, ProjetsEndpoints.Conflit(refus));

        if (!cache.TryGetValue(Cle(input.FichierId), out (Guid Dalle, PlanCoffrage Plan) lu))
        {
            var fichier = await fichiers.LireAsync(EtudeDalle.Contexte, input.FichierId, ct);
            if (fichier is not { } f || f.Fichier.CibleId != dalleId || f.Fichier.Categorie != EtudeDalle.Coffrage)
                return (null, ProjetsEndpoints.Conflit("Déposez d'abord le plan de coffrage de cette étude."));
            using var contenu = f.Contenu;
            if (!f.Fichier.Nom.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase) && !f.Fichier.Nom.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase))
                return (null, ProjetsEndpoints.Conflit("Le plan de coffrage doit être un fichier AutoCAD, au format DWG ou DXF."));
            try
            {
                lu = (dalleId, PlanCoffrageDwg.Lire(contenu, f.Fichier.Nom));
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                journaux.CreateLogger("Progix.FondPlan").LogWarning(e, "Plan de coffrage illisible : {Nom}", f.Fichier.Nom);
                return (null, ProjetsEndpoints.Conflit("Ce fichier n'a pas pu être lu. Enregistrez-le depuis AutoCAD au format DWG 2018 ou DXF, puis déposez-le à nouveau."));
            }
            cache.Set(Cle(input.FichierId), lu, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(15) });
        }
        else if (lu.Dalle != dalleId) return (null, ProjetsEndpoints.Conflit("Déposez d'abord le plan de coffrage de cette étude."));

        var roles = FondPlan.RolesProposes(lu.Plan);
        foreach (var (calque, role) in input.Calques ?? [])
            if (roles.ContainsKey(calque) && FondPlan.Roles.Any(r => r.Code == role)) roles[calque] = role;
        var plans = FondPlan.Plans(lu.Plan, roles);
        (NiveauPlan Niveau, FondPlanNiveau Fond)? choisi = input.Niveau is { } repere
            ? plans.OrderBy(p => p.Niveau.Zone.Contains(repere.X, repere.Y) ? 0 : 1).ThenBy(p => Math.Pow(p.Niveau.X - repere.X, 2) + Math.Pow(p.Niveau.Y - repere.Y, 2)).Cast<(NiveauPlan, FondPlanNiveau)?>().FirstOrDefault()
            : plans.Count == 1 ? plans[0] : null;
        return (new Analyse(lu.Plan, roles, plans, choisi), null);
    }

    private static string Cle(Guid fichierId) => $"plan-coffrage:{fichierId:N}";

    private static string NomFichier(string niveau)
    {
        var propre = new string(niveau.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|' ? ' ' : c).ToArray());
        return string.Join(' ', propre.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Géométrie de l'aperçu, en mètres, le coin bas gauche de la dalle à l'origine.</summary>
    private static object Apercu(FondPlanNiveau f)
    {
        var e = f.Dalle.EnvelopeInternal;
        double[][] Points(Polygon p) => p.ExteriorRing.Coordinates.Select(c => new[] { Math.Round(c.X - e.MinX, 3), Math.Round(c.Y - e.MinY, 3) }).ToArray();
        return new
        {
            largeur = Math.Round(e.Width, 3), hauteur = Math.Round(e.Height, 3), surface = Math.Round(f.Dalle.Area, 2),
            dalle = Points(f.Dalle), poteaux = f.Poteaux.Select(Points), voiles = f.Voiles.Select(Points), reservations = f.Reservations.Select(Points),
            ronds = f.Ronds.Select(r => new { x = Math.Round(r.X - e.MinX, 3), y = Math.Round(r.Y - e.MinY, 3), r = Math.Round(r.Rayon, 3) }),
        };
    }
}
