using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Facturation.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Endpoints;

public static class TableauDeBordEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        // Indicateurs financiers d'une période, comparés à la période précédente de même durée.
        app.MapGet("/api/tableau-de-bord/finances", async (FacturationDbContext db, DateOnly? debut, DateOnly? fin, string? entite) =>
        {
            var jour = DateOnly.FromDateTime(DateTime.UtcNow);
            var du = debut ?? new DateOnly(jour.Year, jour.Month, 1);
            var au = fin ?? du.AddMonths(1).AddDays(-1);
            if (au < du) return Results.Conflict(new { message = "La fin de la période ne peut pas précéder son début." });
            var (pDu, pAu) = Precedente(du, au);

            var factures = await db.Factures.AsNoTracking().Include(f => f.Avoirs).Include(f => f.Encaissements)
                .Where(f => string.IsNullOrEmpty(entite) || f.Entite == entite).ToListAsync();
            var (mouvements, _) = await TresorerieEndpoints.Mouvements(db, pDu, au.AddDays(1), entite);

            object Periode(DateOnly a, DateOnly b)
            {
                var ca = factures.Where(f => f.DateFacture >= a && f.DateFacture <= b).Sum(f => f.Du);
                var t = TresorerieEndpoints.Total(mouvements.Where(m => m.Date >= a && m.Date <= b));
                return new { debut = a, fin = b, chiffreAffaires = ca, encaisse = t.Encaisse, depenses = t.Depenses, marge = ca - t.Depenses };
            }
            var retards = factures.Where(f => FacturesEndpoints.Statut(f) == "retard").OrderBy(f => f.Echeance).ToList();
            return Results.Ok(new
            {
                periode = Periode(du, au), precedente = Periode(pDu, pAu),
                retards = new
                {
                    nb = retards.Count, total = retards.Sum(f => f.Reste),
                    factures = retards.Take(10).Select(f => new { id = f.Id, numero = f.Numero, client = f.ClientNom, echeance = f.Echeance, jours = jour.DayNumber - f.Echeance.DayNumber, reste = f.Reste }),
                },
            });
        }).WithTags("Tableau de bord").RequireAuthorization(Policies.Commercial);
    }

    /// <summary>Période précédente : le même nombre de mois entiers quand la période en couvre, sinon le même nombre de jours.</summary>
    private static (DateOnly, DateOnly) Precedente(DateOnly du, DateOnly au)
    {
        if (du.Day == 1 && au.AddDays(1).Day == 1)
        {
            var mois = (au.Year - du.Year) * 12 + au.Month - du.Month + 1;
            return (du.AddMonths(-mois), du.AddDays(-1));
        }
        var jours = au.DayNumber - du.DayNumber + 1;
        return (du.AddDays(-jours), du.AddDays(-1));
    }
}
