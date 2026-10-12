using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Facturation.Domain;
using Progix.Modules.Facturation.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Endpoints;

public record EncaissementInput(DateOnly DatePaiement, string Mode, decimal Montant, string? Reference, string? Banque, DateOnly? EcheanceEffet, string? MotDePasse = null);
public record AnnulationInput(string? Motif);

public static class EncaissementsEndpoints
{
    /// <summary>Plafond légal des règlements en espèces : par client et par mois civil.</summary>
    public const decimal PlafondEspeces = 5000m;

    /// <summary>Annulation d'un règlement : responsable administratif, chef de projet, directeur.</summary>
    public static readonly string[] RolesAnnulation = [Roles.ResponsableAdministratif, Roles.ChefProjet, Roles.Directeur];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api").WithTags("Encaissements").RequireAuthorization(Policies.Commercial);

        // Factures qui attendent encore un règlement, pour encaisser depuis la trésorerie.
        g.MapGet("/factures/a-encaisser", async (FacturationDbContext db) =>
        {
            var factures = await db.Factures.AsNoTracking().Include(f => f.Encaissements).Include(f => f.Avoirs).OrderBy(f => f.ClientNom).ThenBy(f => f.Numero).ToListAsync();
            return Results.Ok(factures.Where(f => !f.Annulee && f.ACouvrir > 0).Select(f => new
            {
                id = f.Id, numero = f.Numero, entite = f.Entite, clientId = f.ClientId, client = f.ClientNom, projet = f.ProjetLibelle, echeance = f.Echeance, aCouvrir = f.ACouvrir,
            }));
        });

        // Espèces déjà encaissées pour le client de la facture sur le mois de la date donnée.
        g.MapGet("/factures/{id:guid}/especes", async (Guid id, DateOnly date, FacturationDbContext db) =>
        {
            var f = await db.Factures.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (f is null) return Results.NotFound();
            var deja = await EspecesDuMois(db, f.ClientId, date);
            return Results.Ok(new { plafond = PlafondEspeces, deja, disponible = Math.Max(0, PlafondEspeces - deja) });
        });

        g.MapPost("/factures/{id:guid}/encaissements", async (Guid id, EncaissementInput input, FacturationDbContext db, IUtilisateursLookup users, ClaimsPrincipal user,
            INotificationsDirection notifications, ILoggerFactory logs) =>
        {
            // L'encaissement vaut signature électronique du reçu : le mot de passe de la personne qui encaisse est exigé.
            if (string.IsNullOrEmpty(input.MotDePasse) || !await users.VerifierMotDePasseAsync(user.UserId(), input.MotDePasse))
                return Conflit("Mot de passe incorrect : il signe électroniquement le reçu de paiement.");
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var f = await db.Factures.Include(x => x.Lignes).Include(x => x.Encaissements).Include(x => x.Avoirs).FirstOrDefaultAsync(x => x.Id == id);
            if (f is null) return Results.NotFound();
            if (!ModesPaiement.Libelles.ContainsKey(input.Mode)) return Conflit("Mode de paiement inconnu.");
            var montant = Math.Round(input.Montant, 2);
            if (montant <= 0) return Conflit("Le montant doit être supérieur à zéro.");
            if (f.ACouvrir <= 0) return Conflit(f.EffetsEnAttente > 0 ? "Cette facture est entièrement couverte, en comptant les effets en attente d'échéance." : "Cette facture est déjà entièrement payée.");
            if (montant > f.ACouvrir) return Conflit($"Le montant dépasse le reste à couvrir de la facture ({DH(f.ACouvrir)}).");
            if (input.DatePaiement > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))) return Conflit("La date de paiement ne peut pas être dans le futur.");
            var reference = string.IsNullOrWhiteSpace(input.Reference) ? null : input.Reference.Trim();
            if (reference is null && input.Mode is ModesPaiement.Cheque or ModesPaiement.Effet)
                return Conflit(input.Mode == ModesPaiement.Cheque ? "Le numéro du chèque est obligatoire." : "Le numéro de l'effet est obligatoire.");
            if (input.Mode == ModesPaiement.Effet && input.EcheanceEffet < input.DatePaiement) return Conflit("L'échéance de l'effet ne peut pas précéder sa date de réception.");
            if (input.Mode == ModesPaiement.Effet && input.EcheanceEffet is null) return Conflit("La date d'échéance de l'effet est obligatoire.");
            if (f.Annulee) return Conflit("Cette facture est annulée par un avoir.");
            if (input.Mode == ModesPaiement.Especes)
            {
                var deja = await EspecesDuMois(db, f.ClientId, input.DatePaiement);
                if (deja + montant > PlafondEspeces)
                    return Conflit($"Plafond des espèces dépassé : {DH(PlafondEspeces)} par client et par mois, dont {DH(deja)} déjà encaissés ce mois. Il reste {DH(Math.Max(0, PlafondEspeces - deja))} possibles en espèces.");
            }
            var anneeRecu = input.DatePaiement.Year;
            db.Encaissements.Add(new Encaissement
            {
                NumeroRecu = $"REC-{await db.NumeroSuivantAsync($"recu:{f.Entite}:{anneeRecu}"):00000}-{anneeRecu}", SigneLe = DateTime.UtcNow,
                Jeton = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
                FactureId = f.Id, ClientId = f.ClientId, DatePaiement = input.DatePaiement, Mode = input.Mode, Montant = montant, Reference = reference, EcheanceEffet = input.Mode == ModesPaiement.Effet ? input.EcheanceEffet : null,
                Banque = string.IsNullOrWhiteSpace(input.Banque) ? null : input.Banque.Trim(), SaisiPar = user.UserName(),
            });
            var recu = db.Encaissements.Local.Last(x => x.FactureId == f.Id);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            // Un courriel prévient les directeurs ; un échec d'envoi n'annule pas le paiement.
            try
            {
                var lignes = new List<KeyValuePair<string, string>>
                {
                    new("Montant", DH(montant)), new("Mode", ModesPaiement.Libelles[input.Mode] + (reference is null ? "" : $" · {reference}")),
                    new("Client", f.ClientNom), new("Facture", $"{f.Numero} · reste {DH(Math.Max(0, f.Reste))}"), new("Projet", $"{f.ProjetCode} · {f.ProjetLibelle}"),
                    new("Date du paiement", input.DatePaiement.ToString("dd/MM/yyyy")), new("Reçu", recu.NumeroRecu), new("Saisi par", user.UserName()),
                };
                if (input.Mode == ModesPaiement.Effet && input.EcheanceEffet is { } ech) lignes.Add(new("Échéance de l'effet", ech.ToString("dd/MM/yyyy")));
                await notifications.SignalerAsync($"Nouveau paiement · {DH(montant)} · {f.ClientNom}", $"Nouveau paiement de {DH(montant)}", lignes, $"/facturation");
            }
            catch (Exception ex) { logs.CreateLogger("Notifications").LogWarning(ex, "Courriel de paiement non préparé pour la facture {Facture}", f.Numero); }
            return Results.Ok(FacturesEndpoints.Dto(f, true));
        });

        // Remboursement d'un trop-perçu : la facture a été payée, puis réduite ou annulée par un avoir.
        g.MapPost("/factures/{id:guid}/remboursements", async (Guid id, EncaissementInput input, FacturationDbContext db, IUtilisateursLookup users, ClaimsPrincipal user) =>
        {
            if (string.IsNullOrEmpty(input.MotDePasse) || !await users.VerifierMotDePasseAsync(user.UserId(), input.MotDePasse))
                return Conflit("Mot de passe incorrect : il signe électroniquement le reçu de remboursement.");
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var f = await db.Factures.Include(x => x.Lignes).Include(x => x.Encaissements).Include(x => x.Avoirs).FirstOrDefaultAsync(x => x.Id == id);
            if (f is null) return Results.NotFound();
            if (!ModesPaiement.Libelles.ContainsKey(input.Mode) || input.Mode == ModesPaiement.Effet) return Conflit("Un remboursement se fait par virement, chèque ou espèces.");
            var montant = Math.Round(input.Montant, 2);
            if (montant <= 0) return Conflit("Le montant doit être supérieur à zéro.");
            if (f.Reste >= 0) return Conflit("Aucun trop-perçu à rembourser sur cette facture.");
            if (montant > -f.Reste) return Conflit($"Le montant dépasse le trop-perçu à rembourser ({DH(-f.Reste)}).");
            if (input.DatePaiement > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))) return Conflit("La date du remboursement ne peut pas être dans le futur.");
            var reference = string.IsNullOrWhiteSpace(input.Reference) ? null : input.Reference.Trim();
            if (reference is null && input.Mode == ModesPaiement.Cheque) return Conflit("Le numéro du chèque est obligatoire.");
            var anneeRecu = input.DatePaiement.Year;
            db.Encaissements.Add(new Encaissement
            {
                // Les reçus de remboursement ont leur propre série, RMB-XXXXX-AAAA par entité.
                NumeroRecu = $"RMB-{await db.NumeroSuivantAsync($"remboursement:{f.Entite}:{anneeRecu}"):00000}-{anneeRecu}", SigneLe = DateTime.UtcNow,
                Jeton = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
                FactureId = f.Id, ClientId = f.ClientId, DatePaiement = input.DatePaiement, Mode = input.Mode, Montant = montant, Reference = reference, Remboursement = true,
                Banque = string.IsNullOrWhiteSpace(input.Banque) ? null : input.Banque.Trim(), SaisiPar = user.UserName(),
            });
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(FacturesEndpoints.Dto(f, true));
        });

        g.MapPost("/encaissements/{id:guid}/annuler", async (Guid id, AnnulationInput input, FacturationDbContext db, ClaimsPrincipal user) =>
        {
            var e = await db.Encaissements.FirstOrDefaultAsync(x => x.Id == id);
            if (e is null) return Results.NotFound();
            if (!RolesAnnulation.Any(user.IsInRole)) return Results.Forbid();
            if (e.Annule) return Conflit("Cet encaissement est déjà annulé.");
            if (string.IsNullOrWhiteSpace(input.Motif)) return Conflit("Le motif de l'annulation est obligatoire.");
            e.Annule = true; e.MotifAnnulation = input.Motif.Trim(); e.AnnulePar = user.UserName(); e.AnnuleLe = DateTime.UtcNow;
            await db.SaveChangesAsync();
            var f = await db.Factures.AsNoTracking().Include(x => x.Lignes).Include(x => x.Encaissements).Include(x => x.Avoirs).FirstAsync(x => x.Id == e.FactureId);
            return Results.Ok(FacturesEndpoints.Dto(f, true));
        });
    }

    private static readonly System.Globalization.CultureInfo Fr = new("fr-FR");
    private static string DH(decimal v) => v.ToString("N2", Fr) + " DH";

    private static IResult Conflit(string message) => Results.Conflict(new { message });

    private static async Task<decimal> EspecesDuMois(FacturationDbContext db, Guid clientId, DateOnly date)
    {
        var debut = new DateOnly(date.Year, date.Month, 1);
        var fin = debut.AddMonths(1);
        return await db.Encaissements.Where(e => e.ClientId == clientId && !e.Annule && !e.Remboursement && e.Mode == ModesPaiement.Especes && e.DatePaiement >= debut && e.DatePaiement < fin)
            .SumAsync(e => (decimal?)e.Montant) ?? 0;
    }
}
