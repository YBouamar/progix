using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Facturation.Domain;
using Progix.Modules.Facturation.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Endpoints;

/// <param name="MontantHT">Montant hors taxes d'un avoir partiel. Ignoré pour un avoir total.</param>
public record AvoirInput(bool Total, decimal? MontantHT, DateOnly DateAvoir, string? Motif);

public static class AvoirsEndpoints
{
    /// <summary>Annulation d'un avoir : directeur et responsable administratif.</summary>
    public static readonly string[] RolesAnnulation = [Roles.Directeur, Roles.ChefProjet, Roles.ResponsableAdministratif];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api").WithTags("Avoirs").RequireAuthorization(Policies.Commercial);

        g.MapGet("/avoirs", async (FacturationDbContext db, int? annee, string? q) =>
        {
            var query = from a in db.Avoirs.AsNoTracking() join f in db.Factures.AsNoTracking() on a.FactureId equals f.Id select new { a, f };
            if (annee is int an) query = query.Where(x => x.a.DateAvoir >= new DateOnly(an, 1, 1) && x.a.DateAvoir <= new DateOnly(an, 12, 31));
            if (!string.IsNullOrWhiteSpace(q))
            {
                var like = $"%{q.Trim()}%";
                query = query.Where(x => EF.Functions.ILike(x.a.Numero, like) || EF.Functions.ILike(x.f.Numero, like) || EF.Functions.ILike(x.f.ProjetLibelle, like)
                    || EF.Functions.ILike(x.f.ProjetCode, like) || EF.Functions.ILike(x.f.ClientNom, like));
            }
            var items = await query.OrderByDescending(x => x.a.DateAvoir).ThenByDescending(x => x.a.Numero).Take(500).ToListAsync();
            return Results.Ok(items.Select(x => new
            {
                id = x.a.Id, numero = x.a.Numero, entite = x.a.Entite, dateAvoir = x.a.DateAvoir, motif = x.a.Motif, total = x.a.Total, totalTTC = x.a.TotalTTC, annule = x.a.Annule,
                factureId = x.f.Id, factureNumero = x.f.Numero, projet = x.f.ProjetLibelle, client = x.f.ClientNom,
            }));
        });

        g.MapPost("/factures/{id:guid}/avoirs", async (Guid id, AvoirInput input, FacturationDbContext db, ClaimsPrincipal user) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var f = await db.Factures.Include(x => x.Lignes).Include(x => x.Avoirs).FirstOrDefaultAsync(x => x.Id == id);
            if (f is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(input.Motif)) return Conflit("Le motif de l'avoir est obligatoire.");
            if (f.Annulee) return Conflit("Cette facture est déjà annulée par un avoir total.");
            if (input.DateAvoir < f.DateFacture) return Conflit("La date de l'avoir ne peut pas précéder celle de la facture.");

            var dejaHT = f.Avoirs.Where(x => !x.Annule).Sum(x => x.MontantHT);
            var restantHT = f.NetHT - dejaHT;
            var a = new Avoir
            {
                Entite = f.Entite, FactureId = f.Id, ProjetId = f.ProjetId, DateAvoir = input.DateAvoir, Motif = input.Motif.Trim(),
                Total = input.Total, TauxTva = f.TauxTva, EmisPar = user.UserName(),
            };
            if (input.Total)
            {
                // Le solde exact de la facture, sans écart d'arrondi.
                a.MontantHT = restantHT;
                a.TotalTTC = f.Du;
                a.Tva = a.TotalTTC - a.MontantHT;
            }
            else
            {
                var ht = Math.Round(input.MontantHT ?? 0, 2);
                if (ht <= 0) return Conflit("Le montant hors taxes de l'avoir doit être supérieur à zéro.");
                if (ht >= restantHT) return Conflit($"Un avoir partiel reste inférieur au montant hors taxes non encore couvert ({restantHT.ToString("N2", Fr)} DH). Pour tout annuler, émettez un avoir total.");
                a.MontantHT = ht;
                a.Tva = Math.Round(ht * f.TauxTva / 100m, 2);
                a.TotalTTC = a.MontantHT + a.Tva;
            }

            var annee = a.DateAvoir.Year;
            a.Numero = $"AV-{await db.NumeroSuivantAsync($"avoir:{a.Entite}:{annee}"):00000}-{annee}";
            db.Avoirs.Add(a);
            if (a.Total)
            {
                // La facture est annulée : ses bons de livraison redeviennent facturables.
                var bons = await db.BonsLivraison.Where(b => b.FactureId == f.Id).ToListAsync();
                foreach (var b in bons) b.FactureId = null;
            }
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            await db.ImputerAsync([f]);
            return Results.Ok(FacturesEndpoints.Dto(f, true, await FacturesEndpoints.PaiementsDe(db, f)));
        });

        g.MapPost("/avoirs/{id:guid}/annuler", async (Guid id, AnnulationInput input, FacturationDbContext db, ClaimsPrincipal user) =>
        {
            if (!RolesAnnulation.Any(user.IsInRole)) return Results.Forbid();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var a = await db.Avoirs.FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            if (a.Annule) return Conflit("Cet avoir est déjà annulé.");
            if (string.IsNullOrWhiteSpace(input.Motif)) return Conflit("Le motif de l'annulation est obligatoire.");
            var f = await db.Factures.Include(x => x.Lignes).Include(x => x.Avoirs).FirstAsync(x => x.Id == a.FactureId);
            if (a.Total)
            {
                // Annuler un avoir total remet la facture en vigueur : ce qu'elle facturait ne doit pas avoir été refacturé entre-temps.
                if (f.Type == TypesFacture.Acompte)
                {
                    var autres = await db.Factures.Where(x => x.DevisId == f.DevisId && x.Id != f.Id && x.Type == TypesFacture.Acompte && !x.Avoirs.Any(v => v.Total && !v.Annule)).SumAsync(x => x.AcomptePct ?? 0);
                    if (autres + (f.AcomptePct ?? 0) > 100) return Conflit("Impossible d'annuler cet avoir : l'acompte a été refacturé depuis, le total dépasserait 100 % du devis.");
                }
                else
                {
                    var dalles = f.Lignes.Where(l => l.DalleId != null).Select(l => l.DalleId!.Value).ToList();
                    var bons = await db.BonsLivraison.Where(b => b.Lignes.Any(l => dalles.Contains(l.DalleId))).ToListAsync();
                    if (bons.Any(b => b.FactureId != null && b.FactureId != f.Id)) return Conflit("Impossible d'annuler cet avoir : les bons de livraison de cette facture ont été refacturés depuis.");
                    foreach (var b in bons) b.FactureId = f.Id;
                }
            }
            a.Annule = true; a.MotifAnnulation = input.Motif.Trim(); a.AnnulePar = user.UserName(); a.AnnuleLe = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            await db.ImputerAsync([f]);
            return Results.Ok(FacturesEndpoints.Dto(f, true, await FacturesEndpoints.PaiementsDe(db, f)));
        });
    }

    private static readonly System.Globalization.CultureInfo Fr = new("fr-FR");
    private static IResult Conflit(string message) => Results.Conflict(new { message });
}
