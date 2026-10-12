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
/// Statistiques de performance : dalles dont les plans d'exécution sont terminés, par dessinateur,
/// et projets suivis, par ingénieur. Par mois et sur l'année.
/// </summary>
public static class PerformanceEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/statistiques/performance", async (int? annee, ProjetsDbContext db, IUtilisateursLookup users, ClaimsPrincipal user) =>
        {
            var direction = user.IsInRole(Roles.Directeur) || user.IsInRole(Roles.ChefProjet);
            var voitIngenieurs = direction || user.IsInRole(Roles.IngenieurChef);
            var voitDessinateurs = direction || user.IsInRole(Roles.DessinateurChef);
            if (!voitIngenieurs && !voitDessinateurs) return Results.Forbid();

            var jour = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(1));
            var an = annee is >= 2000 and <= 2100 ? annee.Value : jour.Year;
            var vivants = await db.Projets.AsNoTracking().Where(p => !p.Supprime).Select(p => p.Id).ToListAsync();

            object? dessinateurs = null, ingenieurs = null;
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
                dessinateurs = noms.Select(n =>
                {
                    var siennes = lignes.Where(l => l.Id == n.Key && l.Le.Year == an).ToList();
                    return new
                    {
                        id = n.Key, nom = n.Value, mois = Enumerable.Range(1, 12).Select(m => siennes.Count(l => l.Le.Month == m)).ToArray(),
                        total = siennes.Count, surface = siennes.Sum(l => l.Surface),
                    };
                }).OrderByDescending(x => x.total).ThenBy(x => x.nom).ToList();
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
                ingenieurs = noms.Select(n =>
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
            }

            return Results.Ok(new { annee = an, annees = Enumerable.Range(premiere, jour.Year - premiere + 1).Reverse(), moisCourant = an == jour.Year ? jour.Month : (int?)null, dessinateurs, ingenieurs });
        }).WithTags("Statistiques").RequireAuthorization();
    }
}
