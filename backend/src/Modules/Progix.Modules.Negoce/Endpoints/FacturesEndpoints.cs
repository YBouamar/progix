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

public record FactureVenteInput(Guid CommandeId, DateOnly? DateDocument, DateOnly? Echeance, string? Notes, List<LigneRepriseInput>? Lignes);
public record AvoirVenteInput(Guid FactureId, DateOnly? DateDocument, string? Motif, List<LigneRepriseInput>? Lignes);
public record EncaissementVenteInput(Guid FactureId, DateOnly? Date, decimal Montant, string? Mode, string? Reference, string? Notes);

/// <summary>Factures de vente directe (une ou plusieurs par commande), avoirs et encaissements. Jamais mélangées à la facturation de la post-tension.</summary>
public static class FacturesEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var f = app.MapGroup("/api/negoce/factures").WithTags("Négoce - factures").RequireAuthorization().AddEndpointFilter<ConcurrenceFilter>();

        f.MapGet("/", async (string? statut, Guid? clientId, Guid? commandeId, string? recherche, NegoceDbContext db) =>
        {
            var q = db.Documents.AsNoTracking().Include(x => x.Lignes).Include(x => x.Paiements).Where(x => x.Type == TypesVente.Facture);
            if (clientId is { } cl) q = q.Where(x => x.ClientId == cl);
            if (commandeId is { } c) q = q.Where(x => x.ParentId == c);
            if (!string.IsNullOrWhiteSpace(recherche)) { var t = recherche.Trim().ToLower(); q = q.Where(x => x.Numero.ToLower().Contains(t) || x.ClientNom.ToLower().Contains(t) || (x.ParentNumero ?? "").ToLower().Contains(t)); }
            var factures = await q.OrderByDescending(x => x.DateDocument).ThenByDescending(x => x.Numero).ToListAsync();
            var ids = factures.Select(x => x.Id).ToList();
            var avoirs = await db.Documents.AsNoTracking().Include(x => x.Lignes).Where(x => x.Type == TypesVente.Avoir && x.ParentId != null && ids.Contains(x.ParentId.Value)).ToListAsync();
            var liste = factures.Select(x =>
            {
                var reste = VentesSupport.ResteAPayer(x, avoirs.Where(a => a.ParentId == x.Id));
                var s = VentesSupport.StatutPaiement(x, reste);
                return (x, reste, statut: s);
            }).Where(t => string.IsNullOrWhiteSpace(statut) || t.statut == statut).ToList();
            return Results.Ok(liste.Select(t => new
            {
                id = t.x.Id, numero = t.x.Numero, entite = t.x.Entite, clientId = t.x.ClientId, client = t.x.ClientNom, projetCode = t.x.ProjetCode, date = t.x.DateDocument, echeance = t.x.Echeance,
                etat = t.x.Etat, etatLabel = VentesSupport.EtatLabel(t.x.Type, t.x.Etat), totalHt = t.x.TotalHT, totalTtc = t.x.TotalTTC, encaisse = t.x.Encaisse,
                reste = t.x.Annule ? 0 : t.reste, statut = t.statut, parentId = t.x.ParentId, parentNumero = t.x.ParentNumero,
            }));
        });

        f.MapGet("/{id:guid}", async (Guid id, NegoceDbContext db, IFichiersLookup fichiers) =>
        {
            var x = await DevisEndpoints.Charger(db, id, TypesVente.Facture, suivi: false);
            if (x is null) return Results.NotFound();
            var avoirs = await db.Documents.AsNoTracking().Include(a => a.Lignes).Where(a => a.Type == TypesVente.Avoir && a.ParentId == id).OrderBy(a => a.DateDocument).ToListAsync();
            var reste = VentesSupport.ResteAPayer(x, avoirs);
            var detail = VentesSupport.Detail(x);
            detail["lignes"] = x.Lignes.OrderBy(l => l.CreatedAt).Select(l => new
            {
                id = l.Id, articleId = l.ArticleId, code = l.Code, designation = l.Designation, unite = l.Unite, quantite = l.Quantite, prixUnitaire = l.PrixUnitaire,
                montantHt = Montants.LigneHT(l.Quantite, l.PrixUnitaire), resteACrediter = Avancement.ResteACrediter(l, avoirs), sourceLigneId = l.SourceLigneId,
            }).ToList();
            var paiements = new List<object>();
            foreach (var p in x.Paiements.OrderBy(p => p.DatePaiement).ThenBy(p => p.CreatedAt))
                paiements.Add(new
                {
                    id = p.Id, date = p.DatePaiement, montant = p.Montant, mode = p.Mode, reference = p.Reference, notes = p.Notes, par = p.Par, annule = p.Annule, motifAnnulation = p.MotifAnnulation,
                    avecImage = ModesPaiementNegoce.AvecImage(p.Mode), fichiers = await fichiers.NombreAsync(FichiersNegoce.PaiementClient, p.Id),
                });
            detail["paiements"] = paiements;
            detail["avoirs"] = avoirs.Select(VentesSupport.Resume).ToList();
            detail["encaisse"] = x.Encaisse;
            detail["reste"] = x.Annule ? 0 : reste;
            detail["statut"] = VentesSupport.StatutPaiement(x, reste);
            return Results.Ok(detail);
        });

        // On ne facture que ce qui est livré, moins ce qui est déjà facturé.
        f.MapPost("/", async (FactureVenteInput input, NegoceDbContext db, Numeros numeros, ClaimsPrincipal user) =>
        {
            var commande = await DevisEndpoints.Charger(db, input.CommandeId, TypesVente.Commande, suivi: false);
            if (commande is null) return Invalide("commandeId", "Commande introuvable.");
            if (commande.Etat != EtatsVente.Ouvert) return Conflit("Cette commande est annulée.");
            var reprises = (input.Lignes ?? []).Where(l => l.Quantite != 0).ToList();
            if (reprises.Count == 0) return Invalide("lignes", "Indiquez au moins une quantité à facturer.");
            if (reprises.GroupBy(l => l.LigneId).Any(x => x.Count() > 1)) return Invalide("lignes", "Une ligne de la commande est répétée.");
            var enfants = await DevisEndpoints.Enfants(db, commande.Id);
            var date = input.DateDocument ?? DateOnly.FromDateTime(DateTime.Today);
            var facture = new DocumentVente
            {
                Type = TypesVente.Facture, Etat = EtatsVente.Emis, Entite = commande.Entite, ClientId = commande.ClientId, ClientNom = commande.ClientNom, ProjetId = commande.ProjetId,
                ProjetCode = commande.ProjetCode, ProjetLibelle = commande.ProjetLibelle, GroupeId = commande.GroupeId, GroupeNom = commande.GroupeNom, ParentId = commande.Id,
                ParentNumero = commande.Numero, DateDocument = date, Echeance = input.Echeance, TauxTva = commande.TauxTva, Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
                CreePar = user.UserName(),
            };
            foreach (var r in reprises)
            {
                var ligne = commande.Lignes.FirstOrDefault(l => l.Id == r.LigneId);
                if (ligne is null) return Invalide("lignes", "Une ligne n'appartient pas à cette commande.");
                if (r.Quantite <= 0) return Invalide("lignes", "Une quantité à facturer doit être supérieure à zéro.");
                var reste = Avancement.ResteAFacturer(ligne, enfants);
                if (r.Quantite > reste) return Conflit($"« {ligne.Designation} » : {r.Quantite:0.####} à facturer pour {Math.Max(0, reste):0.####} livré et non facturé.");
                facture.Lignes.Add(new LigneVente { ArticleId = ligne.ArticleId, Code = ligne.Code, Designation = ligne.Designation, Unite = ligne.Unite, Quantite = r.Quantite, PrixUnitaire = ligne.PrixUnitaire, SourceLigneId = ligne.Id });
            }
            facture.Numero = await numeros.FacturationAsync(TypesNumero.Facture, facture.Entite, date.Year);
            db.Documents.Add(facture);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = facture.Id, numero = facture.Numero });
        });

        f.MapPost("/{id:guid}/annuler", async (Guid id, MotifInput input, NegoceDbContext db, ClaimsPrincipal user) =>
        {
            var x = await DevisEndpoints.Charger(db, id, TypesVente.Facture, suivi: true);
            if (x is null) return Results.NotFound();
            if (x.Etat == EtatsVente.Annule) return Conflit("Cette facture est déjà annulée.");
            if (x.Paiements.Any(p => !p.Annule)) return Conflit("Annulez d'abord les paiements de cette facture.");
            if (await db.Documents.AnyAsync(a => a.Type == TypesVente.Avoir && a.ParentId == id && a.Etat != EtatsVente.Annule)) return Conflit("Annulez d'abord les avoirs de cette facture.");
            return await DevisEndpoints.Annuler(x, input, db, user);
        });

        var a = app.MapGroup("/api/negoce/avoirs").WithTags("Négoce - avoirs").RequireAuthorization().AddEndpointFilter<ConcurrenceFilter>();

        a.MapGet("/", async (Guid? factureId, Guid? clientId, string? recherche, NegoceDbContext db) =>
        {
            var q = db.Documents.AsNoTracking().Include(x => x.Lignes).Where(x => x.Type == TypesVente.Avoir);
            if (factureId is { } fa) q = q.Where(x => x.ParentId == fa);
            if (clientId is { } cl) q = q.Where(x => x.ClientId == cl);
            if (!string.IsNullOrWhiteSpace(recherche)) { var t = recherche.Trim().ToLower(); q = q.Where(x => x.Numero.ToLower().Contains(t) || x.ClientNom.ToLower().Contains(t) || (x.ParentNumero ?? "").ToLower().Contains(t)); }
            return Results.Ok((await q.OrderByDescending(x => x.DateDocument).ThenByDescending(x => x.Numero).ToListAsync()).Select(VentesSupport.Resume));
        });

        a.MapGet("/{id:guid}", async (Guid id, NegoceDbContext db) =>
        {
            var x = await DevisEndpoints.Charger(db, id, TypesVente.Avoir, suivi: false);
            return x is null ? Results.NotFound() : Results.Ok(VentesSupport.Detail(x));
        });

        // Un avoir reprend des lignes d'une facture émise, sans dépasser ce qui a été facturé.
        a.MapPost("/", async (AvoirVenteInput input, NegoceDbContext db, Numeros numeros, ClaimsPrincipal user) =>
        {
            var facture = await DevisEndpoints.Charger(db, input.FactureId, TypesVente.Facture, suivi: false);
            if (facture is null) return Invalide("factureId", "Facture introuvable.");
            if (facture.Annule) return Conflit("Cette facture est annulée.");
            if (string.IsNullOrWhiteSpace(input.Motif)) return Invalide("motif", "Le motif de l'avoir est obligatoire.");
            var reprises = (input.Lignes ?? []).Where(l => l.Quantite != 0).ToList();
            if (reprises.Count == 0) return Invalide("lignes", "Indiquez au moins une quantité à créditer.");
            if (reprises.GroupBy(l => l.LigneId).Any(x => x.Count() > 1)) return Invalide("lignes", "Une ligne de la facture est répétée.");
            var avoirs = await db.Documents.AsNoTracking().Include(x => x.Lignes).Where(x => x.Type == TypesVente.Avoir && x.ParentId == facture.Id && x.Etat != EtatsVente.Annule).ToListAsync();
            var date = input.DateDocument ?? DateOnly.FromDateTime(DateTime.Today);
            var avoir = new DocumentVente
            {
                Type = TypesVente.Avoir, Etat = EtatsVente.Emis, Entite = facture.Entite, ClientId = facture.ClientId, ClientNom = facture.ClientNom, ProjetId = facture.ProjetId,
                ProjetCode = facture.ProjetCode, ProjetLibelle = facture.ProjetLibelle, GroupeId = facture.GroupeId, GroupeNom = facture.GroupeNom, ParentId = facture.Id,
                ParentNumero = facture.Numero, DateDocument = date, TauxTva = facture.TauxTva, Notes = input.Motif.Trim(), CreePar = user.UserName(),
            };
            foreach (var r in reprises)
            {
                var ligne = facture.Lignes.FirstOrDefault(l => l.Id == r.LigneId);
                if (ligne is null) return Invalide("lignes", "Une ligne n'appartient pas à cette facture.");
                if (r.Quantite <= 0) return Invalide("lignes", "Une quantité à créditer doit être supérieure à zéro.");
                var reste = Avancement.ResteACrediter(ligne, avoirs);
                if (r.Quantite > reste) return Conflit($"« {ligne.Designation} » : {r.Quantite:0.####} à créditer pour {reste:0.####} restant.");
                avoir.Lignes.Add(new LigneVente { ArticleId = ligne.ArticleId, Code = ligne.Code, Designation = ligne.Designation, Unite = ligne.Unite, Quantite = r.Quantite, PrixUnitaire = ligne.PrixUnitaire, SourceLigneId = ligne.Id });
            }
            avoir.Numero = await numeros.FacturationAsync(TypesNumero.Avoir, avoir.Entite, date.Year);
            db.Documents.Add(avoir);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = avoir.Id, numero = avoir.Numero });
        });

        a.MapPost("/{id:guid}/annuler", async (Guid id, MotifInput input, NegoceDbContext db, ClaimsPrincipal user) =>
        {
            var x = await DevisEndpoints.Charger(db, id, TypesVente.Avoir, suivi: true);
            if (x is null) return Results.NotFound();
            if (x.Etat == EtatsVente.Annule) return Conflit("Cet avoir est déjà annulé.");
            return await DevisEndpoints.Annuler(x, input, db, user);
        });

        var e = app.MapGroup("/api/negoce/encaissements").WithTags("Négoce - encaissements").RequireAuthorization().AddEndpointFilter<ConcurrenceFilter>();

        e.MapGet("/", async (Guid? factureId, Guid? clientId, NegoceDbContext db, IFichiersLookup fichiers) =>
        {
            var q = from p in db.PaiementsClient.AsNoTracking() join d in db.Documents.AsNoTracking() on p.FactureId equals d.Id
                    where (factureId == null || p.FactureId == factureId) && (clientId == null || p.ClientId == clientId)
                    orderby p.DatePaiement descending, p.CreatedAt descending select new { p, d.Numero, d.ClientNom };
            var liste = new List<object>();
            foreach (var x in await q.Take(500).ToListAsync())
                liste.Add(new
                {
                    id = x.p.Id, factureId = x.p.FactureId, facture = x.Numero, client = x.ClientNom, entite = x.p.Entite, date = x.p.DatePaiement, montant = x.p.Montant, mode = x.p.Mode,
                    reference = x.p.Reference, annule = x.p.Annule, avecImage = ModesPaiementNegoce.AvecImage(x.p.Mode), fichiers = await fichiers.NombreAsync(FichiersNegoce.PaiementClient, x.p.Id),
                });
            return Results.Ok(liste);
        });

        // Espèces déjà encaissées pour le client d'une facture sur le mois : sert à afficher ce qu'il reste possible.
        e.MapGet("/especes", async (Guid factureId, DateOnly date, NegoceDbContext db, IEnumerable<IEspecesClient> especes) =>
        {
            var facture = await db.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == factureId && x.Type == TypesVente.Facture);
            if (facture is null) return Results.NotFound();
            var deja = 0m;
            foreach (var source in especes) deja += await source.DuMoisAsync(facture.ClientId, date);
            return Results.Ok(new { plafond = PlafondEspecesClient.Montant, deja, disponible = Math.Max(0, PlafondEspecesClient.Montant - deja) });
        });

        e.MapPost("/", async (EncaissementVenteInput input, NegoceDbContext db, IEnumerable<IEspecesClient> especes, ClaimsPrincipal user) =>
        {
            var facture = await DevisEndpoints.Charger(db, input.FactureId, TypesVente.Facture, suivi: true);
            if (facture is null) return Invalide("factureId", "Facture introuvable.");
            if (facture.Annule) return Conflit("Cette facture est annulée.");
            if (input.Mode is null || !ModesPaiementNegoce.Tous.Contains(input.Mode)) return Invalide("mode", "Mode de paiement : virement, chèque, effet ou espèces.");
            if (input.Montant <= 0) return Invalide("montant", "Le montant doit être supérieur à zéro.");
            var avoirs = await db.Documents.AsNoTracking().Include(x => x.Lignes).Where(x => x.Type == TypesVente.Avoir && x.ParentId == facture.Id).ToListAsync();
            var reste = VentesSupport.ResteAPayer(facture, avoirs);
            if (input.Montant > reste + 0.005m) return Conflit($"L'encaissement dépasse le reste à payer ({reste:N2} DH).");
            var date = input.Date ?? DateOnly.FromDateTime(DateTime.Today);
            // Plafond légal des espèces : par client et par mois civil, ventes directes et post-tension confondues.
            if (input.Mode == ModesPaiementNegoce.Especes)
            {
                var deja = 0m;
                foreach (var source in especes) deja += await source.DuMoisAsync(facture.ClientId, date);
                if (deja + input.Montant > PlafondEspecesClient.Montant)
                    return Conflit($"Plafond des espèces dépassé : {PlafondEspecesClient.Montant:N2} DH par client et par mois, dont {deja:N2} DH déjà encaissés ce mois (ventes directes et post-tension). Il reste {Math.Max(0, PlafondEspecesClient.Montant - deja):N2} DH possibles en espèces.");
            }
            var p = new PaiementClient
            {
                FactureId = facture.Id, ClientId = facture.ClientId, Entite = facture.Entite, DatePaiement = date, Montant = input.Montant, Mode = input.Mode,
                Reference = string.IsNullOrWhiteSpace(input.Reference) ? null : input.Reference.Trim(), Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(), Par = user.UserName(),
            };
            db.PaiementsClient.Add(p);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = p.Id, avecImage = ModesPaiementNegoce.AvecImage(p.Mode) });
        });

        e.MapPost("/{id:guid}/annuler", async (Guid id, MotifInput input, NegoceDbContext db, ClaimsPrincipal user) =>
        {
            var p = await db.PaiementsClient.FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            if (p.Annule) return Conflit("Cet encaissement est déjà annulé.");
            if (string.IsNullOrWhiteSpace(input.Motif)) return Conflit("Le motif de l'annulation est obligatoire.");
            p.Annule = true; p.MotifAnnulation = input.Motif.Trim(); p.AnnulePar = user.UserName(); p.AnnuleLe = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = p.Id, annule = true });
        });
    }
}
