using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Negoce.Application;
using Progix.Modules.Negoce.Domain;
using Progix.Modules.Negoce.Infrastructure;
using Progix.SharedKernel;
using static Progix.Modules.Negoce.Application.Reponses;

namespace Progix.Modules.Negoce.Endpoints;

public record LigneAchatInput(Guid ArticleId, decimal Quantite, decimal? PrixUnitaire);
public record CommandeAchatInput(string? Entite, Guid FournisseurId, DateOnly? DateCommande, string? Notes, List<LigneAchatInput>? Lignes);
public record LigneReceptionInput(Guid LigneId, decimal Quantite);
public record ReceptionInput(DateOnly? Date, string? Notes, List<LigneReceptionInput>? Lignes);
public record FactureFournisseurInput(string? Numero, DateOnly? Date, decimal MontantTtc, string? Notes);
public record PaiementFournisseurInput(DateOnly? Date, decimal Montant, string? Mode, string? Reference, string? Notes);
public record MotifInput(string? Motif);

/// <summary>Commandes d'achat, réceptions (même partielles), factures reçues et paiements des fournisseurs.</summary>
public static class AchatsEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/negoce/achats").WithTags("Négoce - achats").RequireAuthorization().AddEndpointFilter<ConcurrenceFilter>();

        g.MapGet("/", async (string? etat, Guid? fournisseurId, string? recherche, NegoceDbContext db, DroitsNegoce droits, ClaimsPrincipal user) =>
        {
            var q = db.CommandesAchat.AsNoTracking().Include(c => c.Lignes).Include(c => c.Paiements).AsQueryable();
            if (fournisseurId is { } f) q = q.Where(c => c.FournisseurId == f);
            if (!string.IsNullOrWhiteSpace(recherche)) { var t = recherche.Trim().ToLower(); q = q.Where(c => c.Numero.ToLower().Contains(t) || c.FournisseurNom.ToLower().Contains(t)); }
            var commandes = await q.OrderByDescending(c => c.DateCommande).ThenByDescending(c => c.Numero).ToListAsync();
            if (!string.IsNullOrWhiteSpace(etat)) commandes = commandes.Where(c => c.Etat == etat).ToList();
            var prix = await droits.VoitPrixAchatAsync(user);
            var paiements = await droits.NiveauAsync(user, Habilitations.NegocePaiementsFournisseurs) >= Habilitations.Lecture;
            return Results.Ok(commandes.Select(c => new
            {
                id = c.Id, numero = c.Numero, entite = c.Entite, fournisseurId = c.FournisseurId, fournisseur = c.FournisseurNom, date = c.DateCommande,
                etat = c.Etat, etatLabel = EtatsAchat.Label(c.Etat), lignes = c.Lignes.Count,
                totalHt = prix ? c.TotalHT : (decimal?)null, totalTtc = prix ? c.TotalTTC : (decimal?)null,
                paye = paiements && prix ? c.Paye : (decimal?)null, reste = paiements && prix && !c.Annule ? c.ResteAPayer : (decimal?)null,
            }));
        });

        g.MapGet("/{id:guid}", async (Guid id, NegoceDbContext db, IFichiersLookup fichiers, DroitsNegoce droits, ClaimsPrincipal user) =>
        {
            var c = await Charger(db, id, suivi: false);
            if (c is null) return Results.NotFound();
            var prix = await droits.VoitPrixAchatAsync(user);
            var paiements = await droits.NiveauAsync(user, Habilitations.NegocePaiementsFournisseurs) >= Habilitations.Lecture;
            var lignesParId = c.Lignes.ToDictionary(l => l.Id);
            object? factures = null, reglements = null;
            if (paiements)
            {
                var f = new List<object>();
                foreach (var x in c.Factures.OrderBy(x => x.DateFacture))
                    f.Add(new { id = x.Id, numero = x.NumeroFournisseur, date = x.DateFacture, montantTtc = x.MontantTTC, notes = x.Notes, par = x.Par, fichiers = await fichiers.NombreAsync(FichiersNegoce.FactureFournisseur, x.Id) });
                factures = f;
                var r = new List<object>();
                foreach (var x in c.Paiements.OrderBy(x => x.DatePaiement).ThenBy(x => x.CreatedAt))
                    r.Add(new
                    {
                        id = x.Id, date = x.DatePaiement, montant = x.Montant, mode = x.Mode, reference = x.Reference, notes = x.Notes, par = x.Par, annule = x.Annule, motifAnnulation = x.MotifAnnulation,
                        avecImage = ModesPaiementNegoce.AvecImage(x.Mode), fichiers = await fichiers.NombreAsync(FichiersNegoce.PaiementFournisseur, x.Id),
                    });
                reglements = r;
            }
            return Results.Ok(new
            {
                id = c.Id, numero = c.Numero, entite = c.Entite, fournisseurId = c.FournisseurId, fournisseur = c.FournisseurNom, date = c.DateCommande, notes = c.Notes,
                etat = c.Etat, etatLabel = EtatsAchat.Label(c.Etat), annule = c.Annule, motifAnnulation = c.MotifAnnulation, creePar = c.CreePar,
                totalHt = prix ? c.TotalHT : (decimal?)null, totalTva = prix ? c.TotalTVA : (decimal?)null, totalTtc = prix ? c.TotalTTC : (decimal?)null,
                paye = paiements && prix ? c.Paye : (decimal?)null, reste = paiements && prix && !c.Annule ? c.ResteAPayer : (decimal?)null,
                lignes = c.Lignes.OrderBy(l => l.CreatedAt).Select(l => new { id = l.Id, articleId = l.ArticleId, code = l.Code, designation = l.Designation, unite = l.Unite, quantite = l.Quantite, quantiteRecue = l.QuantiteRecue, reste = l.Reste, prixUnitaire = prix ? l.PrixUnitaire : (decimal?)null }),
                receptions = c.Receptions.OrderBy(x => x.DateReception).ThenBy(x => x.CreatedAt).Select(x => new
                {
                    id = x.Id, date = x.DateReception, par = x.Par, notes = x.Notes,
                    lignes = x.Lignes.Select(l => new { designation = lignesParId.TryGetValue(l.LigneAchatId, out var li) ? li.Designation : "", unite = lignesParId.TryGetValue(l.LigneAchatId, out var u) ? u.Unite : "", quantite = l.Quantite }),
                }),
                factures, paiements = reglements,
            });
        });

        g.MapPost("/", async (CommandeAchatInput input, NegoceDbContext db, Numeros numeros, ClaimsPrincipal user) =>
        {
            var c = new CommandeAchat { CreePar = user.UserName() };
            if (await Appliquer(c, input, db, creation: true) is { } erreur) return erreur;
            c.Numero = await numeros.CommandeAchatAsync(c.Entite, c.DateCommande.Year);
            db.CommandesAchat.Add(c);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = c.Id, numero = c.Numero });
        });

        // Une commande se modifie tant que rien n'a été reçu ni payé.
        g.MapPut("/{id:guid}", async (Guid id, CommandeAchatInput input, NegoceDbContext db) =>
        {
            var c = await Charger(db, id, suivi: true);
            if (c is null) return Results.NotFound();
            if (c.Annule) return Conflit("Cette commande est annulée.");
            if (c.Receptions.Count > 0) return Conflit("Une réception est déjà enregistrée : la commande ne se modifie plus.");
            if (c.Paiements.Any(p => !p.Annule)) return Conflit("Un paiement est déjà enregistré : la commande ne se modifie plus.");
            if (await Appliquer(c, input, db, creation: false) is { } erreur) return erreur;
            c.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = c.Id, numero = c.Numero });
        });

        g.MapPost("/{id:guid}/annuler", async (Guid id, MotifInput input, NegoceDbContext db, ClaimsPrincipal user) =>
        {
            var c = await Charger(db, id, suivi: true);
            if (c is null) return Results.NotFound();
            if (c.Annule) return Conflit("Cette commande est déjà annulée.");
            if (string.IsNullOrWhiteSpace(input.Motif)) return Conflit("Le motif de l'annulation est obligatoire.");
            if (c.Receptions.Count > 0) return Conflit("Des articles ont été reçus : la commande ne peut plus être annulée.");
            if (c.Paiements.Any(p => !p.Annule)) return Conflit("Annulez d'abord les paiements de cette commande.");
            c.Annule = true; c.MotifAnnulation = input.Motif.Trim(); c.AnnulePar = user.UserName(); c.AnnuleLe = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = c.Id, annule = true });
        });

        // Réception : chaque ligne reçue crée une entrée en stock au prix d'achat de la commande, ce qui recalcule le coût moyen.
        g.MapPost("/{id:guid}/receptions", async (Guid id, ReceptionInput input, NegoceDbContext db, GestionStock stock, ClaimsPrincipal user) =>
        {
            var c = await Charger(db, id, suivi: true);
            if (c is null) return Results.NotFound();
            if (c.Annule) return Conflit("Cette commande est annulée.");
            var recues = (input.Lignes ?? []).Where(l => l.Quantite != 0).ToList();
            if (recues.Count == 0) return Invalide("lignes", "Indiquez au moins une quantité reçue.");
            if (recues.GroupBy(l => l.LigneId).Any(x => x.Count() > 1)) return Invalide("lignes", "Une ligne de la commande est répétée.");
            var date = input.Date ?? DateOnly.FromDateTime(DateTime.Today);
            var reception = new ReceptionAchat { CommandeId = c.Id, DateReception = date, Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(), Par = user.UserName() };
            foreach (var r in recues)
            {
                var ligne = c.Lignes.FirstOrDefault(l => l.Id == r.LigneId);
                if (ligne is null) return Invalide("lignes", "Une ligne reçue n'appartient pas à cette commande.");
                if (Reception.Refus(ligne, r.Quantite) is { } refus) return Conflit(refus);
                var article = await db.Articles.FirstAsync(a => a.Id == ligne.ArticleId);
                var (m, motif) = await stock.EnregistrerAsync(article, TypesMouvement.Entree, r.Quantite, ligne.PrixUnitaire, date, $"Réception {c.Numero}", user.UserName(), "reception", reception.Id, c.Numero);
                if (m is null) return Conflit(motif!);
                ligne.QuantiteRecue += r.Quantite;
                reception.Lignes.Add(new LigneReception { ReceptionId = reception.Id, LigneAchatId = ligne.Id, ArticleId = ligne.ArticleId, Quantite = r.Quantite });
            }
            db.Receptions.Add(reception);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = reception.Id, etat = c.Etat, etatLabel = EtatsAchat.Label(c.Etat) });
        });

        g.MapPost("/{id:guid}/factures", async (Guid id, FactureFournisseurInput input, NegoceDbContext db, ClaimsPrincipal user) =>
        {
            var c = await db.CommandesAchat.FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return Results.NotFound();
            if (c.Annule) return Conflit("Cette commande est annulée.");
            if (string.IsNullOrWhiteSpace(input.Numero)) return Invalide("numero", "Le numéro de la facture du fournisseur est obligatoire.");
            if (input.MontantTtc <= 0) return Invalide("montantTtc", "Le montant doit être supérieur à zéro.");
            var numero = input.Numero.Trim();
            if (await db.FacturesFournisseur.AnyAsync(f => f.CommandeId == id && f.NumeroFournisseur == numero)) return Conflit("Cette facture est déjà enregistrée sur la commande.");
            var f = new FactureFournisseur { CommandeId = id, NumeroFournisseur = numero, DateFacture = input.Date ?? DateOnly.FromDateTime(DateTime.Today), MontantTTC = input.MontantTtc, Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(), Par = user.UserName() };
            db.FacturesFournisseur.Add(f);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = f.Id });
        });

        g.MapDelete("/{id:guid}/factures/{factureId:guid}", async (Guid id, Guid factureId, NegoceDbContext db) =>
        {
            var f = await db.FacturesFournisseur.FirstOrDefaultAsync(x => x.Id == factureId && x.CommandeId == id);
            if (f is null) return Results.NotFound();
            db.FacturesFournisseur.Remove(f);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapPost("/{id:guid}/paiements", async (Guid id, PaiementFournisseurInput input, NegoceDbContext db, ClaimsPrincipal user) =>
        {
            var c = await Charger(db, id, suivi: true);
            if (c is null) return Results.NotFound();
            if (c.Annule) return Conflit("Cette commande est annulée.");
            if (input.Mode is null || !ModesPaiementNegoce.Tous.Contains(input.Mode)) return Invalide("mode", "Mode de paiement : virement, chèque, effet ou espèces.");
            if (input.Montant <= 0) return Invalide("montant", "Le montant doit être supérieur à zéro.");
            if (input.Montant > c.ResteAPayer) return Conflit($"Le paiement dépasse le reste à payer ({c.ResteAPayer:N2} DH).");
            var p = new PaiementFournisseur
            {
                CommandeId = id, DatePaiement = input.Date ?? DateOnly.FromDateTime(DateTime.Today), Montant = input.Montant, Mode = input.Mode,
                Reference = string.IsNullOrWhiteSpace(input.Reference) ? null : input.Reference.Trim(), Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(), Par = user.UserName(),
            };
            db.PaiementsFournisseur.Add(p);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = p.Id, avecImage = ModesPaiementNegoce.AvecImage(p.Mode) });
        });

        // Un paiement s'annule avec un motif : il ne se modifie pas.
        g.MapPost("/{id:guid}/paiements/{paiementId:guid}/annuler", async (Guid id, Guid paiementId, MotifInput input, NegoceDbContext db, ClaimsPrincipal user) =>
        {
            var p = await db.PaiementsFournisseur.FirstOrDefaultAsync(x => x.Id == paiementId && x.CommandeId == id);
            if (p is null) return Results.NotFound();
            if (p.Annule) return Conflit("Ce paiement est déjà annulé.");
            if (string.IsNullOrWhiteSpace(input.Motif)) return Conflit("Le motif de l'annulation est obligatoire.");
            p.Annule = true; p.MotifAnnulation = input.Motif.Trim(); p.AnnulePar = user.UserName(); p.AnnuleLe = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = p.Id, annule = true });
        });
    }

    private static Task<CommandeAchat?> Charger(NegoceDbContext db, Guid id, bool suivi)
    {
        var q = db.CommandesAchat.Include(c => c.Lignes).Include(c => c.Receptions).ThenInclude(r => r.Lignes).Include(c => c.Factures).Include(c => c.Paiements).AsSplitQuery();
        return (suivi ? q : q.AsNoTracking()).FirstOrDefaultAsync(c => c.Id == id);
    }

    private static async Task<IResult?> Appliquer(CommandeAchat c, CommandeAchatInput input, NegoceDbContext db, bool creation)
    {
        var entite = (input.Entite ?? "").Trim().ToUpperInvariant();
        if (!Entites.Labels.ContainsKey(entite)) return Invalide("entite", "Entité inconnue.");
        if (!creation && entite != c.Entite && c.Numero.Length > 0) return Conflit("L'entité d'une commande numérotée ne change pas.");
        var fournisseur = await db.Fournisseurs.AsNoTracking().FirstOrDefaultAsync(f => f.Id == input.FournisseurId);
        if (fournisseur is null) return Invalide("fournisseurId", "Fournisseur introuvable.");
        if (!fournisseur.Actif && fournisseur.Id != c.FournisseurId) return Conflit("Ce fournisseur est désactivé.");
        if (input.Lignes is not { Count: > 0 }) return Invalide("lignes", "Ajoutez au moins une ligne.");
        var lignes = new List<LigneAchat>();
        foreach (var l in input.Lignes)
        {
            if (l.Quantite <= 0) return Invalide("lignes", "Chaque quantité doit être supérieure à zéro.");
            var article = await db.Articles.AsNoTracking().Include(a => a.Unite).FirstOrDefaultAsync(a => a.Id == l.ArticleId);
            if (article is null) return Invalide("lignes", "Un article de la commande est introuvable.");
            if (!article.Actif) return Conflit($"L'article « {article.Designation} » est désactivé.");
            if (article.Origine == OriginesArticle.Produit) return Conflit($"« {article.Designation} » est un article produit : il ne s'achète pas.");
            var prix = l.PrixUnitaire ?? (await db.PrixFournisseurs.AsNoTracking().FirstOrDefaultAsync(p => p.ArticleId == article.Id && p.FournisseurId == fournisseur.Id))?.PrixAchat;
            if (prix is null) return Conflit($"Le prix d'achat de « {article.Designation} » chez {fournisseur.Nom} n'est pas renseigné.");
            if (prix < 0) return Invalide("lignes", "Un prix d'achat ne peut pas être négatif.");
            lignes.Add(new LigneAchat { ArticleId = article.Id, Code = article.Code, Designation = article.Designation, Unite = article.Unite?.Symbole ?? "", Quantite = l.Quantite, PrixUnitaire = prix.Value });
        }
        c.Entite = creation ? entite : c.Entite;
        c.FournisseurId = fournisseur.Id; c.FournisseurNom = fournisseur.Nom;
        c.DateCommande = input.DateCommande ?? (creation ? DateOnly.FromDateTime(DateTime.Today) : c.DateCommande);
        c.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        if (!creation) db.LignesAchat.RemoveRange(c.Lignes);
        c.Lignes = lignes;
        return null;
    }
}
