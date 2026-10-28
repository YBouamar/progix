using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Facturation.Domain;
using Progix.Modules.Facturation.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Endpoints;

public record DepenseInput(string Entite, DateOnly DateDepense, string? Libelle, decimal Montant, string Mode, string? Reference);

public static class TresorerieEndpoints
{
    public sealed record Mouvement(Guid Id, DateOnly Date, string Type, string Libelle, string Objet, string Entite, string Mode, decimal Montant, Guid? FactureId, string SaisiPar, string? Recu = null);

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api").WithTags("Trésorerie").RequireAuthorization(Policies.Commercial);

        // Journal du mois : encaissements, remboursements et dépenses. Un effet entre au journal à sa date d'échéance.
        g.MapGet("/tresorerie", async (FacturationDbContext db, string? mois, string? entite, string? type, string? mode, string? q) =>
        {
            var j = await Charger(db, mois, entite, type, mode, q);
            return Results.Ok(new
            {
                mois = j.Debut.ToString("yyyy-MM"),
                kpis = new { encaisse = j.Mois.Encaisse, depenses = j.Mois.Depenses, solde = j.Mois.Solde },
                precedent = new { encaisse = j.Precedent.Encaisse, depenses = j.Precedent.Depenses, solde = j.Precedent.Solde },
                compteurs = Types.Select(t => new { type = t.Key, label = t.Value.Pluriel, nb = j.Base.Count(m => m.Type == t.Key) }),
                mouvements = j.Mouvements.Select(m => new
                {
                    id = m.Id, date = m.Date, type = m.Type, libelle = m.Libelle, objet = m.Objet, entite = m.Entite, mode = m.Mode,
                    modeLabel = ModesPaiement.Libelles.GetValueOrDefault(m.Mode, m.Mode), montant = m.Montant, factureId = m.FactureId, saisiPar = m.SaisiPar, recu = m.Recu,
                }),
                effetsAEchoir = j.AEchoir.Select(x => new { id = x.Id, echeance = x.Echeance, recuLe = x.RecuLe, client = x.Client, facture = x.Facture, reference = x.Reference, banque = x.Banque, montant = x.Montant, entite = x.Entite }),
            });
        });

        // Exports du journal affiché : mêmes filtres que l'écran.
        g.MapGet("/tresorerie/export.xlsx", async (FacturationDbContext db, string? mois, string? entite, string? type, string? mode, string? q) =>
        {
            var j = await Charger(db, mois, entite, type, mode, q);
            return Results.File(Pdf.ExportsTresorerie.Excel(j), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"tresorerie-{j.Debut:yyyy-MM}.xlsx");
        });

        g.MapGet("/tresorerie/export.pdf", async (FacturationDbContext db, string? mois, string? entite, string? type, string? mode, string? q, ClaimsPrincipal user) =>
        {
            var j = await Charger(db, mois, entite, type, mode, q);
            return Results.File(Pdf.ExportsTresorerie.Pdf(j, user.UserName()), "application/pdf", $"tresorerie-{j.Debut:yyyy-MM}.pdf");
        });

        g.MapPost("/depenses", async (DepenseInput input, FacturationDbContext db, ClaimsPrincipal user) =>
        {
            if (input.Entite is not (Entites.Apt or Entites.Bbr)) return Conflit("Choisissez l'entité qui porte la dépense.");
            if (string.IsNullOrWhiteSpace(input.Libelle)) return Conflit("Le libellé est obligatoire.");
            if (input.Montant <= 0) return Conflit("Le montant doit être supérieur à zéro.");
            if (!ModesPaiement.Libelles.ContainsKey(input.Mode)) return Conflit("Mode de paiement inconnu.");
            if (input.DateDepense > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))) return Conflit("La date de la dépense ne peut pas être dans le futur.");
            var x = new Depense
            {
                Entite = input.Entite, DateDepense = input.DateDepense, Libelle = input.Libelle.Trim(), Montant = Math.Round(input.Montant, 2), Mode = input.Mode,
                Reference = string.IsNullOrWhiteSpace(input.Reference) ? null : input.Reference.Trim(), SaisiPar = user.UserName(),
            };
            db.Depenses.Add(x);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = x.Id });
        });

        g.MapPost("/depenses/{id:guid}/annuler", async (Guid id, AnnulationInput input, FacturationDbContext db, ClaimsPrincipal user) =>
        {
            if (!EncaissementsEndpoints.RolesAnnulation.Any(user.IsInRole)) return Results.Forbid();
            var x = await db.Depenses.FirstOrDefaultAsync(v => v.Id == id);
            if (x is null) return Results.NotFound();
            if (x.Annule) return Conflit("Cette dépense est déjà annulée.");
            if (string.IsNullOrWhiteSpace(input.Motif)) return Conflit("Le motif de l'annulation est obligatoire.");
            x.Annule = true; x.MotifAnnulation = input.Motif.Trim(); x.AnnulePar = user.UserName(); x.AnnuleLe = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = x.Id });
        });
    }

    public static readonly IReadOnlyDictionary<string, (string Singulier, string Pluriel)> Types = new Dictionary<string, (string, string)>
    {
        ["enc"] = ("Encaissement", "Encaissements"), ["remb"] = ("Remboursement", "Remboursements"), ["dep"] = ("Dépense de dalle", "Dépenses de dalle"), ["gen"] = ("Dépense générale", "Dépenses générales"),
    };

    public sealed record Totaux(decimal Encaisse, decimal Depenses) { public decimal Solde => Encaisse - Depenses; }
    public sealed record EffetAEchoir(Guid Id, DateOnly Echeance, DateOnly RecuLe, string Client, string Facture, string? Reference, string? Banque, decimal Montant, string Entite);
    /// <param name="Base">Mouvements du mois après filtres de mode et de recherche, avant le filtre de type.</param>
    public sealed record Journal(DateOnly Debut, string? Entite, string? Type, string? Mode, string? Recherche, Totaux Mois, Totaux Precedent, List<Mouvement> Base, List<Mouvement> Mouvements, List<EffetAEchoir> AEchoir);

    internal static Totaux Total(IEnumerable<Mouvement> m)
    {
        var l = m.ToList();
        return new Totaux(l.Where(x => x.Type is "enc" or "remb").Sum(x => x.Montant), -l.Where(x => x.Type is "gen" or "dep").Sum(x => x.Montant));
    }

    private static async Task<Journal> Charger(FacturationDbContext db, string? mois, string? entite, string? type, string? mode, string? q)
    {
        var jour = DateOnly.FromDateTime(DateTime.UtcNow);
        var debut = DateOnly.TryParseExact(mois + "-01", "yyyy-MM-dd", out var d) ? d : new DateOnly(jour.Year, jour.Month, 1);
        var fin = debut.AddMonths(1);
        var (tous, aEchoir) = await Mouvements(db, debut.AddMonths(-1), fin, entite);

        var duMois = tous.Where(m => m.Date >= debut).ToList();
        var filtres = duMois.Where(m => string.IsNullOrEmpty(mode) || m.Mode == mode);
        if (!string.IsNullOrWhiteSpace(q)) filtres = filtres.Where(m => $"{m.Libelle} {m.Objet}".Contains(q.Trim(), StringComparison.OrdinalIgnoreCase));
        var base_ = filtres.ToList();
        return new Journal(debut, entite, type, mode, q, Total(duMois), Total(tous.Where(m => m.Date < debut)), base_,
            base_.Where(m => string.IsNullOrEmpty(type) || m.Type == type).OrderByDescending(m => m.Date).ThenBy(m => m.Type).ToList(), aEchoir);
    }

    /// <summary>Mouvements de trésorerie entre deux dates (fin exclue). Un effet compte à sa date d'échéance ; les effets non échus sont rendus à part.</summary>
    internal static async Task<(List<Mouvement> Tous, List<EffetAEchoir> AEchoir)> Mouvements(FacturationDbContext db, DateOnly avant, DateOnly fin, string? entite)
    {
        var jour = DateOnly.FromDateTime(DateTime.UtcNow);
        var enc = (await db.Encaissements.AsNoTracking()
            .Where(e => !e.Annule && (string.IsNullOrEmpty(entite) || e.Entite == entite)
                && ((e.Mode == ModesPaiement.Effet && !e.Remboursement) ? (e.EcheanceEffet >= avant) : (e.DatePaiement >= avant && e.DatePaiement < fin)))
            .ToListAsync()).Select(e => new { e, Numero = e.NumeroRecu ?? "", e.ClientNom, e.ProjetCode, e.Entite }).ToList();
        var dep = await db.Depenses.AsNoTracking().Where(x => !x.Annule && x.DateDepense >= avant && x.DateDepense < fin && (string.IsNullOrEmpty(entite) || x.Entite == entite)).ToListAsync();

        var tous = new List<Mouvement>();
        var aEchoir = new List<EffetAEchoir>();
        foreach (var x in enc)
        {
            var effet = x.e.Mode == ModesPaiement.Effet && !x.e.Remboursement;
            var date = effet ? x.e.EcheanceEffet!.Value : x.e.DatePaiement;
            if (effet && date > jour)
            {
                aEchoir.Add(new EffetAEchoir(x.e.Id, date, x.e.DatePaiement, x.ClientNom, x.Numero, x.e.Reference, x.e.Banque, x.e.Montant, x.Entite));
                continue;
            }
            if (date >= fin) continue;
            tous.Add(new Mouvement(x.e.Id, date, x.e.Remboursement ? "remb" : "enc", $"{x.ClientNom} · {x.Numero}", x.ProjetCode, x.Entite, x.e.Mode,
                x.e.Remboursement ? -x.e.Montant : x.e.Montant, x.e.FactureId, x.e.SaisiPar, x.e.NumeroRecu));
        }
        tous.AddRange(dep.Select(x => x.DalleId is null
            ? new Mouvement(x.Id, x.DateDepense, "gen", x.Libelle, "Hors projet", x.Entite, x.Mode, -x.Montant, null, x.SaisiPar)
            : new Mouvement(x.Id, x.DateDepense, "dep", string.IsNullOrEmpty(x.Libelle) ? x.Categorie ?? "" : $"{x.Categorie} · {x.Libelle}", x.DalleReference ?? "", x.Entite, x.Mode, -x.Montant, null, x.SaisiPar)));
        return (tous, aEchoir);
    }

    private static IResult Conflit(string message) => Results.Conflict(new { message });
}
