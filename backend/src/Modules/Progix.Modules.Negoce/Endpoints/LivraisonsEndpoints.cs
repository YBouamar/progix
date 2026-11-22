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

public record LivraisonInput(Guid CommandeId, DateOnly? DateDocument, string? Notes, List<LigneRepriseInput>? Lignes);

/// <summary>Bons de livraison : plusieurs par commande, chacun sort ses articles du stock au coût moyen du moment.</summary>
public static class LivraisonsEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/negoce/livraisons").WithTags("Négoce - bons de livraison").RequireAuthorization().AddEndpointFilter<ConcurrenceFilter>();

        g.MapGet("/", async (Guid? commandeId, Guid? clientId, string? etat, string? recherche, NegoceDbContext db) =>
        {
            var q = db.Documents.AsNoTracking().Include(x => x.Lignes).Where(x => x.Type == TypesVente.Livraison);
            if (commandeId is { } c) q = q.Where(x => x.ParentId == c);
            if (clientId is { } cl) q = q.Where(x => x.ClientId == cl);
            if (!string.IsNullOrWhiteSpace(etat)) q = q.Where(x => x.Etat == etat);
            if (!string.IsNullOrWhiteSpace(recherche)) { var t = recherche.Trim().ToLower(); q = q.Where(x => x.Numero.ToLower().Contains(t) || x.ClientNom.ToLower().Contains(t) || (x.ParentNumero ?? "").ToLower().Contains(t)); }
            return Results.Ok((await q.OrderByDescending(x => x.DateDocument).ThenByDescending(x => x.Numero).ToListAsync()).Select(VentesSupport.Resume));
        });

        g.MapGet("/{id:guid}", async (Guid id, NegoceDbContext db) =>
        {
            var x = await DevisEndpoints.Charger(db, id, TypesVente.Livraison, suivi: false);
            return x is null ? Results.NotFound() : Results.Ok(VentesSupport.Detail(x));
        });

        g.MapPost("/", async (LivraisonInput input, NegoceDbContext db, GestionStock stock, Numeros numeros, ClaimsPrincipal user) =>
        {
            var commande = await DevisEndpoints.Charger(db, input.CommandeId, TypesVente.Commande, suivi: false);
            if (commande is null) return Invalide("commandeId", "Commande introuvable.");
            if (commande.Etat != EtatsVente.Ouvert) return Conflit("Cette commande est annulée.");
            var reprises = (input.Lignes ?? []).Where(l => l.Quantite != 0).ToList();
            if (reprises.Count == 0) return Invalide("lignes", "Indiquez au moins une quantité à livrer.");
            if (reprises.GroupBy(l => l.LigneId).Any(x => x.Count() > 1)) return Invalide("lignes", "Une ligne de la commande est répétée.");
            var enfants = await DevisEndpoints.Enfants(db, commande.Id);
            var date = input.DateDocument ?? DateOnly.FromDateTime(DateTime.Today);
            var bl = new DocumentVente
            {
                Type = TypesVente.Livraison, Etat = EtatsVente.Emis, Entite = commande.Entite, ClientId = commande.ClientId, ClientNom = commande.ClientNom, ProjetId = commande.ProjetId,
                ProjetCode = commande.ProjetCode, ProjetLibelle = commande.ProjetLibelle, GroupeId = commande.GroupeId, GroupeNom = commande.GroupeNom, ParentId = commande.Id,
                ParentNumero = commande.Numero, DateDocument = date, TauxTva = commande.TauxTva, Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(), CreePar = user.UserName(),
            };
            var sorties = new List<MouvementStock>();
            foreach (var r in reprises)
            {
                var ligne = commande.Lignes.FirstOrDefault(l => l.Id == r.LigneId);
                if (ligne is null) return Invalide("lignes", "Une ligne n'appartient pas à cette commande.");
                var reste = Avancement.ResteALivrer(ligne, enfants);
                if (r.Quantite <= 0) return Invalide("lignes", "Une quantité à livrer doit être supérieure à zéro.");
                if (r.Quantite > reste) return Conflit($"« {ligne.Designation} » : {r.Quantite:0.####} à livrer pour {reste:0.####} restant.");
                var article = await db.Articles.FirstAsync(a => a.Id == ligne.ArticleId);
                var (m, refus) = await stock.EnregistrerAsync(article, TypesMouvement.Sortie, r.Quantite, null, date, "Livraison", user.UserName(), "bl");
                if (m is null) return Conflit($"« {ligne.Designation} » : {refus}");
                sorties.Add(m);
                bl.Lignes.Add(new LigneVente
                {
                    ArticleId = ligne.ArticleId, Code = ligne.Code, Designation = ligne.Designation, Unite = ligne.Unite, Quantite = r.Quantite, PrixUnitaire = ligne.PrixUnitaire,
                    SourceLigneId = ligne.Id, CoutUnitaire = m.CoutUnitaire,
                });
            }
            bl.Numero = await numeros.FacturationAsync(TypesNumero.BonLivraison, bl.Entite, date.Year);
            foreach (var m in sorties) { m.PieceId = bl.Id; m.PieceNumero = bl.Numero; m.Motif = $"Livraison {bl.Numero}"; }
            db.Documents.Add(bl);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = bl.Id, numero = bl.Numero });
        });

        // Annulation : les articles reviennent en stock, sauf si une facture couvre déjà les quantités livrées.
        g.MapPost("/{id:guid}/annuler", async (Guid id, MotifInput input, NegoceDbContext db, GestionStock stock, ClaimsPrincipal user) =>
        {
            var bl = await DevisEndpoints.Charger(db, id, TypesVente.Livraison, suivi: true);
            if (bl is null) return Results.NotFound();
            if (bl.Etat == EtatsVente.Annule) return Conflit("Ce bon de livraison est déjà annulé.");
            if (string.IsNullOrWhiteSpace(input.Motif)) return Conflit("Le motif de l'annulation est obligatoire.");
            if (bl.ParentId is { } commandeId)
            {
                var autres = (await DevisEndpoints.Enfants(db, commandeId)).Where(e => e.Id != bl.Id).ToList();
                foreach (var l in bl.Lignes.Where(l => l.SourceLigneId is not null))
                {
                    var livre = Avancement.Reprise(autres, TypesVente.Livraison, l.SourceLigneId!.Value);
                    var facture = Avancement.Reprise(autres, TypesVente.Facture, l.SourceLigneId!.Value);
                    if (facture > livre) return Conflit($"« {l.Designation} » est déjà facturé : annulez d'abord la facture.");
                }
            }
            foreach (var l in bl.Lignes)
            {
                var article = await db.Articles.FirstAsync(a => a.Id == l.ArticleId);
                var (m, refus) = await stock.EnregistrerAsync(article, TypesMouvement.Correction, l.Quantite, l.CoutUnitaire, DateOnly.FromDateTime(DateTime.Today),
                    $"Annulation du bon de livraison {bl.Numero}", user.UserName(), "bl", bl.Id, bl.Numero);
                if (m is null) return Conflit(refus!);
            }
            bl.Etat = EtatsVente.Annule; bl.MotifAnnulation = input.Motif.Trim(); bl.AnnulePar = user.UserName(); bl.AnnuleLe = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = bl.Id, etat = bl.Etat });
        });
    }
}
