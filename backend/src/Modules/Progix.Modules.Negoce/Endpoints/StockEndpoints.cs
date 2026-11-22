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

/// <param name="Quantite">Quantité en unité de stock (la quantité comptée pour un inventaire, signée pour une correction).</param>
/// <param name="Metres">Pour un article stocké en tonnes et mesuré en mètres (toron) : le métrage, converti selon le poids au mètre de l'article.</param>
public record MouvementInput(Guid ArticleId, string? Type, decimal? Quantite, decimal? Metres, decimal? CoutUnitaire, DateOnly? Date, string? Motif);

public static class StockEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/negoce").WithTags("Négoce - stock").RequireAuthorization().AddEndpointFilter<ConcurrenceFilter>();

        g.MapGet("/stock", async (string? recherche, Guid? categorieId, bool? sousSeuil, NegoceDbContext db, GestionStock stock, DroitsNegoce droits, ClaimsPrincipal user) =>
        {
            var q = db.Articles.AsNoTracking().Include(a => a.Categorie).Include(a => a.Unite).Where(a => a.Actif);
            if (!string.IsNullOrWhiteSpace(recherche)) { var t = recherche.Trim().ToLower(); q = q.Where(a => a.Code.ToLower().Contains(t) || a.Designation.ToLower().Contains(t)); }
            if (categorieId is { } c) q = q.Where(a => a.CategorieId == c);
            var articles = await q.OrderBy(a => a.Categorie!.Ordre).ThenBy(a => a.Designation).ToListAsync();
            var stocks = await stock.StocksAsync();
            var voitCouts = await droits.VoitPrixAchatAsync(user);
            var lignes = articles.Select(a =>
            {
                var s = stocks.GetValueOrDefault(a.Id);
                return new
                {
                    id = a.Id, code = a.Code, designation = a.Designation, categorie = a.Categorie?.Libelle, unite = a.Unite?.Symbole, origine = a.Origine,
                    stock = s, seuilAlerte = a.SeuilAlerte, sousSeuil = a.SeuilAlerte > 0 && s <= a.SeuilAlerte, kgParMetre = a.KgParMetre,
                    coutMoyen = voitCouts ? a.CoutMoyen : (decimal?)null, valeur = voitCouts ? Math.Round(s * a.CoutMoyen, 2) : (decimal?)null,
                };
            }).Where(l => sousSeuil != true || l.sousSeuil).ToList();
            return Results.Ok(new
            {
                articles = lignes, sousSeuil = lignes.Count(l => l.sousSeuil),
                valeurTotale = voitCouts ? lignes.Sum(l => l.valeur ?? 0) : (decimal?)null,
            });
        });

        g.MapGet("/mouvements", async (Guid? articleId, string? type, DateOnly? du, DateOnly? au, int? limite, NegoceDbContext db, DroitsNegoce droits, ClaimsPrincipal user) =>
        {
            var q = db.Mouvements.AsNoTracking().AsQueryable();
            if (articleId is { } a) q = q.Where(m => m.ArticleId == a);
            if (!string.IsNullOrWhiteSpace(type)) q = q.Where(m => m.Type == type);
            if (du is { } d) q = q.Where(m => m.Date >= d);
            if (au is { } f) q = q.Where(m => m.Date <= f);
            var voitCouts = await droits.VoitPrixAchatAsync(user);
            var liste = await (from m in q join art in db.Articles.AsNoTracking() on m.ArticleId equals art.Id
                               orderby m.Date descending, m.CreatedAt descending
                               select new { m, art.Code, art.Designation }).Take(Math.Clamp(limite ?? 200, 1, 1000)).ToListAsync();
            return Results.Ok(liste.Select(x => new
            {
                id = x.m.Id, articleId = x.m.ArticleId, code = x.Code, designation = x.Designation, type = x.m.Type, quantite = x.m.Quantite,
                coutUnitaire = voitCouts ? x.m.CoutUnitaire : (decimal?)null, date = x.m.Date, motif = x.m.Motif, pieceType = x.m.PieceType, pieceId = x.m.PieceId,
                piece = x.m.PieceNumero, par = x.m.Par,
            }));
        });

        g.MapPost("/mouvements", async (MouvementInput input, NegoceDbContext db, GestionStock gestion, DroitsNegoce droits, ClaimsPrincipal user) =>
        {
            if (input.Type is null || !TypesMouvement.Tous.Contains(input.Type)) return Invalide("type", "Type de mouvement inconnu.");
            var article = await db.Articles.FirstOrDefaultAsync(a => a.Id == input.ArticleId);
            if (article is null) return Invalide("articleId", "Article introuvable.");
            if (!article.Actif) return Conflit("Cet article est désactivé.");
            if (input.Type == TypesMouvement.Production && article.Origine != OriginesArticle.Produit) return Conflit("L'entrée « Production » ne concerne que les articles produits.");
            decimal saisie;
            if (input.Metres is { } metres)
            {
                if (article.KgParMetre is not { } kg) return Conflit("Cet article n'a pas de poids au mètre : saisissez la quantité dans son unité.");
                saisie = Valorisation.TonnesDepuisMetres(metres, kg);
            }
            else if (input.Quantite is { } q) saisie = q;
            else return Invalide("quantite", "La quantité est obligatoire.");
            // Le coût d'une entrée ne se saisit qu'avec le droit sur les prix d'achat ; sinon l'entrée se fait au coût moyen en cours.
            var cout = await droits.ModifiePrixAchatAsync(user) ? input.CoutUnitaire : null;
            var (m, refus) = await gestion.EnregistrerAsync(article, input.Type, saisie, cout, input.Date ?? DateOnly.FromDateTime(DateTime.Today), input.Motif, user.UserName());
            if (m is null) return Conflit(refus!);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = m.Id, quantite = m.Quantite, stock = await gestion.StockAsync(article.Id) });
        });
    }
}
