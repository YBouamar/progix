using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Facturation.Domain;
using Progix.Modules.Facturation.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Endpoints;

/// <summary>
/// État des paiements de l'année par mode : encaissé, effets en attente d'échéance, remboursé, règlements annulés ;
/// et encaissé par mois et par mode. Comme en trésorerie, un effet compte à sa date d'échéance.
/// </summary>
public static class StatistiquesPaiementsEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/statistiques/paiements", async (int? annee, FacturationDbContext db) =>
        {
            var jour = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(1));
            var an = annee is >= 2000 and <= 2100 ? annee.Value : jour.Year;
            var tous = await db.Encaissements.AsNoTracking().ToListAsync();
            static DateOnly DateEffective(Encaissement e) => e.Mode == ModesPaiement.Effet && !e.Remboursement && e.EcheanceEffet is { } ech ? ech : e.DatePaiement;
            var annees = tous.Select(e => DateEffective(e).Year).Append(jour.Year).Distinct().OrderDescending().ToList();
            var del = tous.Where(e => DateEffective(e).Year == an).ToList();
            var valides = del.Where(e => !e.Annule).ToList();
            var encaisses = valides.Where(e => !e.Remboursement && !e.EnAttente(jour)).ToList();

            return Results.Ok(new
            {
                annee = an, annees, moisCourant = an == jour.Year ? jour.Month : (int?)null,
                modes = ModesPaiement.Libelles.Select(m => new
                {
                    mode = m.Key, label = m.Value,
                    encaisse = encaisses.Where(e => e.Mode == m.Key).Sum(e => e.Montant),
                    nombre = encaisses.Count(e => e.Mode == m.Key),
                    enAttente = valides.Where(e => e.Mode == m.Key && e.EnAttente(jour)).Sum(e => e.Montant),
                    rembourse = valides.Where(e => e.Mode == m.Key && e.Remboursement).Sum(e => e.Montant),
                    annules = del.Count(e => e.Mode == m.Key && e.Annule),
                }),
                mois = Enumerable.Range(1, 12).Select(mo => new
                {
                    mois = mo,
                    parMode = ModesPaiement.Libelles.Keys.ToDictionary(k => k, k => encaisses.Where(e => e.Mode == k && DateEffective(e).Month == mo).Sum(e => e.Montant)),
                }),
            });
        }).WithTags("Statistiques").RequireAuthorization(Policies.Devis);
    }
}
