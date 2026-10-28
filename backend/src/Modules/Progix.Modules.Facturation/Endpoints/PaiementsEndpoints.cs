using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Progix.Modules.Facturation.Domain;
using Progix.Modules.Facturation.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Endpoints;

public record VentilationInput(Guid DalleId, decimal Montant);
public record PaiementInput(DateOnly DatePaiement, string Mode, string? Reference, string? Banque, DateOnly? EcheanceEffet, string? MotDePasse, List<VentilationInput>? Dalles);
public record RemboursementInput(DateOnly DatePaiement, string Mode, string? Reference, string? Banque, string? MotDePasse, Guid DalleId, decimal Montant);

/// <summary>Paiements d'un projet, suivis par dalle : saisie ventilée, remboursement d'un trop-perçu, taux de paiement.</summary>
public static class PaiementsEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/projets/{id:guid}").WithTags("Encaissements");

        // Taux de paiement par dalle : rôles qui voient les chiffres financiers.
        g.MapGet("/paiements", async (Guid id, FacturationDbContext db, IProjetsLookup projets, IDevisLookup devis, ClaimsPrincipal user) =>
        {
            var projet = await projets.ProjetAsync(id, avecDalles: true);
            return projet is null ? Results.NotFound() : Results.Ok(await Vue(db, projet, devis, user));
        }).RequireAuthorization(Policies.Devis);

        g.MapGet("/especes", async (Guid id, DateOnly date, FacturationDbContext db, IProjetsLookup projets) =>
        {
            var projet = await projets.ProjetAsync(id);
            if (projet is null) return Results.NotFound();
            var deja = await EncaissementsEndpoints.EspecesDuMois(db, projet.ClientId, date);
            return Results.Ok(new { plafond = EncaissementsEndpoints.PlafondEspeces, deja, disponible = Math.Max(0, EncaissementsEndpoints.PlafondEspeces - deja) });
        }).RequireAuthorization(Policies.Commercial);

        g.MapPost("/paiements", async (Guid id, PaiementInput input, FacturationDbContext db, IProjetsLookup projets, IDevisLookup devis, IPartenairesLookup partenaires,
            IUtilisateursLookup users, ClaimsPrincipal user, INotificationsDirection notifications, ILoggerFactory logs) =>
        {
            // L'encaissement vaut signature électronique du reçu : le mot de passe de la personne qui encaisse est exigé.
            if (string.IsNullOrEmpty(input.MotDePasse) || !await users.VerifierMotDePasseAsync(user.UserId(), input.MotDePasse))
                return Conflit("Mot de passe incorrect : il signe électroniquement le reçu de paiement.");
            var projet = await projets.ProjetAsync(id, avecDalles: true);
            if (projet is null) return Results.NotFound();
            if (projet.Supprime) return Conflit("Un projet supprimé est en lecture seule.");
            if (!ModesPaiement.Libelles.ContainsKey(input.Mode)) return Conflit("Mode de paiement inconnu.");
            if (input.DatePaiement > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))) return Conflit("La date de paiement ne peut pas être dans le futur.");
            var reference = Nettoyer(input.Reference);
            if (reference is null && input.Mode is ModesPaiement.Cheque or ModesPaiement.Effet)
                return Conflit(input.Mode == ModesPaiement.Cheque ? "Le numéro du chèque est obligatoire." : "Le numéro de l'effet est obligatoire.");
            if (input.Mode == ModesPaiement.Effet && input.EcheanceEffet is null) return Conflit("La date d'échéance de l'effet est obligatoire.");
            if (input.Mode == ModesPaiement.Effet && input.EcheanceEffet < input.DatePaiement) return Conflit("L'échéance de l'effet ne peut pas précéder sa date de réception.");
            var ventilation = (input.Dalles ?? []).Where(v => v.Montant != 0).GroupBy(v => v.DalleId).Select(x => (DalleId: x.Key, Montant: Math.Round(x.Sum(v => v.Montant), 2))).ToList();
            if (ventilation.Count == 0) return Conflit("Répartissez le paiement sur au moins une dalle.");
            if (ventilation.Any(v => v.Montant < 0)) return Conflit("Le montant affecté à une dalle ne peut pas être négatif.");
            var client = await partenaires.LegalAsync(projet.ClientId);
            if (client is null) return Conflit("Client du projet introuvable.");

            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var (situation, _, _) = await Paiements.SituationAsync(db, projet, devis);
            foreach (var v in ventilation)
            {
                var s = situation.FirstOrDefault(x => x.DalleId == v.DalleId);
                var dalle = projet.Dalles.FirstOrDefault(x => x.Id == v.DalleId);
                if (dalle is null) return Conflit("Une dalle de la répartition n'appartient pas à ce projet.");
                if (s is null) return Conflit($"La dalle {dalle.Reference} ne figure dans aucun devis validé : son montant est inconnu.");
                if (v.Montant > s.ACouvrir)
                    return Conflit(s.ACouvrir <= 0 ? $"La dalle {dalle.Reference} est déjà entièrement payée{(s.EffetsEnAttente > 0 ? ", en comptant les effets en attente d'échéance" : "")}."
                        : $"Le montant affecté à la dalle {dalle.Reference} dépasse ce qu'il reste à payer ({DH(s.ACouvrir)}).");
            }
            var montant = ventilation.Sum(v => v.Montant);
            if (input.Mode == ModesPaiement.Especes)
            {
                var deja = await EncaissementsEndpoints.EspecesDuMois(db, projet.ClientId, input.DatePaiement);
                if (deja + montant > EncaissementsEndpoints.PlafondEspeces)
                    return Conflit($"Plafond des espèces dépassé : {DH(EncaissementsEndpoints.PlafondEspeces)} par client et par mois, dont {DH(deja)} déjà encaissés ce mois. Il reste {DH(Math.Max(0, EncaissementsEndpoints.PlafondEspeces - deja))} possibles en espèces.");
            }
            var annee = input.DatePaiement.Year;
            var e = Nouveau(projet, client, input.DatePaiement, input.Mode, montant, reference, input.Banque, user);
            e.NumeroRecu = $"REC-{await db.NumeroSuivantAsync($"recu:{projet.Entite}:{annee}"):00000}-{annee}";
            e.EcheanceEffet = input.Mode == ModesPaiement.Effet ? input.EcheanceEffet : null;
            e.Dalles = ventilation.Select(v => new PaiementDalle { DalleId = v.DalleId, Montant = v.Montant, Reference = projet.Dalles.First(x => x.Id == v.DalleId).Reference }).ToList();
            db.Encaissements.Add(e);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // Un courriel prévient les directeurs ; un échec d'envoi n'annule pas le paiement.
            try
            {
                var lignes = new List<KeyValuePair<string, string>>
                {
                    new("Montant", DH(montant)), new("Mode", ModesPaiement.Libelles[input.Mode] + (reference is null ? "" : $" · {reference}")),
                    new("Client", client.Nom), new("Projet", $"{projet.Code} · {projet.Libelle}"),
                    new("Dalles", string.Join(", ", e.Dalles.Select(d => $"{d.Reference} : {DH(d.Montant)}"))),
                    new("Date du paiement", input.DatePaiement.ToString("dd/MM/yyyy")), new("Reçu", e.NumeroRecu), new("Saisi par", user.UserName()),
                };
                if (e.EcheanceEffet is { } ech) lignes.Add(new("Échéance de l'effet", ech.ToString("dd/MM/yyyy")));
                await notifications.SignalerAsync($"Nouveau paiement · {DH(montant)} · {client.Nom}", $"Nouveau paiement de {DH(montant)}", lignes, $"/projets/{projet.Id}");
            }
            catch (Exception ex) { logs.CreateLogger("Notifications").LogWarning(ex, "Courriel de paiement non préparé pour le reçu {Recu}", e.NumeroRecu); }
            return Results.Ok(new { recuId = e.Id, vue = await Vue(db, projet, devis, user) });
        }).RequireAuthorization(Policies.Commercial);

        // Remboursement d'un trop-perçu sur une dalle : payée au-delà de son montant TTC, après un avoir par exemple.
        g.MapPost("/remboursements", async (Guid id, RemboursementInput input, FacturationDbContext db, IProjetsLookup projets, IDevisLookup devis, IPartenairesLookup partenaires,
            IUtilisateursLookup users, ClaimsPrincipal user) =>
        {
            if (string.IsNullOrEmpty(input.MotDePasse) || !await users.VerifierMotDePasseAsync(user.UserId(), input.MotDePasse))
                return Conflit("Mot de passe incorrect : il signe électroniquement le reçu de remboursement.");
            var projet = await projets.ProjetAsync(id, avecDalles: true);
            if (projet is null) return Results.NotFound();
            if (projet.Supprime) return Conflit("Un projet supprimé est en lecture seule.");
            if (!ModesPaiement.Libelles.ContainsKey(input.Mode) || input.Mode == ModesPaiement.Effet) return Conflit("Un remboursement se fait par virement, chèque ou espèces.");
            var montant = Math.Round(input.Montant, 2);
            if (montant <= 0) return Conflit("Le montant doit être supérieur à zéro.");
            if (input.DatePaiement > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))) return Conflit("La date du remboursement ne peut pas être dans le futur.");
            var reference = Nettoyer(input.Reference);
            if (reference is null && input.Mode == ModesPaiement.Cheque) return Conflit("Le numéro du chèque est obligatoire.");
            var client = await partenaires.LegalAsync(projet.ClientId);
            if (client is null) return Conflit("Client du projet introuvable.");

            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var (situation, _, _) = await Paiements.SituationAsync(db, projet, devis);
            var s = situation.FirstOrDefault(x => x.DalleId == input.DalleId);
            if (s is null) return Conflit("Dalle introuvable dans ce projet, ou hors de tout devis validé.");
            if (s.TropPercu <= 0) return Conflit($"Aucun trop-perçu à rembourser sur la dalle {s.Reference}.");
            if (montant > s.TropPercu) return Conflit($"Le montant dépasse le trop-perçu à rembourser sur la dalle {s.Reference} ({DH(s.TropPercu)}).");
            var annee = input.DatePaiement.Year;
            var e = Nouveau(projet, client, input.DatePaiement, input.Mode, montant, reference, input.Banque, user);
            // Les reçus de remboursement ont leur propre série, RMB-XXXXX-AAAA par entité.
            e.NumeroRecu = $"RMB-{await db.NumeroSuivantAsync($"remboursement:{projet.Entite}:{annee}"):00000}-{annee}";
            e.Remboursement = true;
            e.Dalles = [new PaiementDalle { DalleId = s.DalleId, Reference = s.Reference, Montant = montant }];
            db.Encaissements.Add(e);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(new { recuId = e.Id, vue = await Vue(db, projet, devis, user) });
        }).RequireAuthorization(Policies.Commercial);
    }

    private static Encaissement Nouveau(ProjetResume projet, PartenaireLegal client, DateOnly date, string mode, decimal montant, string? reference, string? banque, ClaimsPrincipal user) => new()
    {
        ProjetId = projet.Id, Entite = projet.Entite, ProjetCode = projet.Code, ProjetLibelle = projet.Libelle,
        ClientId = client.Id, ClientNom = client.Nom, ClientAdresse = client.Adresse, ClientIdentifiants = client.Identifiants,
        DatePaiement = date, Mode = mode, Montant = montant, Reference = reference, Banque = Nettoyer(banque), SaisiPar = user.UserName(), SigneLe = DateTime.UtcNow,
        Jeton = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
    };

    internal static async Task<object> Vue(FacturationDbContext db, ProjetResume projet, IDevisLookup devis, ClaimsPrincipal user)
    {
        var (dalles, encaissements, _) = await Paiements.SituationAsync(db, projet, devis);
        var jour = DateOnly.FromDateTime(DateTime.UtcNow);
        var du = dalles.Sum(d => d.MontantTTC);
        var paye = dalles.Sum(d => d.Paye);
        return new
        {
            peutSaisir = !projet.Supprime && Policies.RolesCommercial.Any(user.IsInRole),
            peutAnnuler = EncaissementsEndpoints.RolesAnnulation.Any(user.IsInRole),
            plafondEspeces = EncaissementsEndpoints.PlafondEspeces,
            totaux = new { montantTTC = du, avoirs = dalles.Sum(d => d.Avoirs), paye, effetsEnAttente = dalles.Sum(d => d.EffetsEnAttente), reste = dalles.Sum(d => d.Reste), taux = du > 0 ? Math.Round(paye / du * 100m, 1) : (decimal?)null },
            dalles = dalles.Select(d => new
            {
                dalleId = d.DalleId, reference = d.Reference, emplacement = d.Emplacement, montantTTC = d.MontantTTC, avoirs = d.Avoirs, du = d.Du,
                paye = d.Paye, effetsEnAttente = d.EffetsEnAttente, reste = d.Reste, aCouvrir = d.ACouvrir, tropPercu = d.TropPercu, taux = d.Taux,
            }),
            // Dalles hors de tout devis validé : pas de montant, donc pas de taux.
            horsDevis = projet.Dalles.Where(x => dalles.All(d => d.DalleId != x.Id)).Select(x => x.Reference),
            paiements = encaissements.Select(e => new
            {
                id = e.Id, datePaiement = e.DatePaiement, mode = e.Mode, modeLabel = ModesPaiement.Libelles.GetValueOrDefault(e.Mode, e.Mode), montant = e.Montant,
                reference = e.Reference, banque = e.Banque, echeanceEffet = e.EcheanceEffet, enAttente = e.EnAttente(jour), remboursement = e.Remboursement,
                numeroRecu = e.NumeroRecu, saisiPar = e.SaisiPar, annule = e.Annule, motifAnnulation = e.MotifAnnulation, annulePar = e.AnnulePar,
                dalles = e.Dalles.Select(x => new { dalleId = x.DalleId, reference = x.Reference, montant = x.Montant }),
            }),
        };
    }

    private static string? Nettoyer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static readonly System.Globalization.CultureInfo Fr = new("fr-FR");
    private static string DH(decimal v) => v.ToString("N2", Fr) + " DH";
    private static IResult Conflit(string message) => Results.Conflict(new { message });
}
