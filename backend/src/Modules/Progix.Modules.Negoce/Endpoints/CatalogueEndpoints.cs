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

public record UniteInput(string? Libelle, string? Symbole, bool? Actif);
public record CategorieInput(string? Libelle, int? Ordre, bool? Actif);
public record ArticleInput(string? Code, string? Designation, Guid CategorieId, Guid UniteId, decimal? PrixVente, string? Origine, decimal? CoutMoyen,
    decimal? SeuilAlerte, decimal? KgParMetre, bool? Actif);
public record PrixAchatInput(Guid ArticleId, Guid FournisseurId, decimal PrixAchat);

/// <summary>Catalogue : unités, catégories, articles et prix d'achat par fournisseur. Les droits fins (prix de vente, prix d'achat) sont contrôlés ici.</summary>
public static class CatalogueEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/negoce").WithTags("Négoce - catalogue").RequireAuthorization().AddEndpointFilter<ConcurrenceFilter>();

        // Unités
        g.MapGet("/unites", async (NegoceDbContext db) =>
            Results.Ok(await db.Unites.AsNoTracking().OrderBy(u => u.Libelle).Select(u => new { id = u.Id, libelle = u.Libelle, symbole = u.Symbole, actif = u.Actif }).ToListAsync()));
        g.MapPost("/unites", async (UniteInput input, NegoceDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(input.Libelle)) return Invalide("libelle", "Le libellé est obligatoire.");
            var libelle = input.Libelle.Trim();
            if (await db.Unites.AnyAsync(u => u.Libelle == libelle)) return Conflit("Cette unité existe déjà.");
            var u = new Unite { Libelle = libelle, Symbole = (input.Symbole ?? libelle).Trim() };
            db.Unites.Add(u);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = u.Id });
        });
        g.MapPut("/unites/{id:guid}", async (Guid id, UniteInput input, NegoceDbContext db) =>
        {
            var u = await db.Unites.FirstOrDefaultAsync(x => x.Id == id);
            if (u is null) return Results.NotFound();
            if (!string.IsNullOrWhiteSpace(input.Libelle))
            {
                var libelle = input.Libelle.Trim();
                if (await db.Unites.AnyAsync(x => x.Libelle == libelle && x.Id != id)) return Conflit("Cette unité existe déjà.");
                u.Libelle = libelle;
            }
            if (input.Symbole is not null) u.Symbole = input.Symbole.Trim();
            if (input.Actif is { } a) u.Actif = a;
            u.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = u.Id });
        });

        // Catégories
        g.MapGet("/categories", async (NegoceDbContext db) =>
            Results.Ok(await db.Categories.AsNoTracking().OrderBy(c => c.Ordre).ThenBy(c => c.Libelle).Select(c => new { id = c.Id, libelle = c.Libelle, ordre = c.Ordre, actif = c.Actif }).ToListAsync()));
        g.MapPost("/categories", async (CategorieInput input, NegoceDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(input.Libelle)) return Invalide("libelle", "Le libellé est obligatoire.");
            var libelle = input.Libelle.Trim();
            if (await db.Categories.AnyAsync(c => c.Libelle == libelle)) return Conflit("Cette catégorie existe déjà.");
            var c = new CategorieArticle { Libelle = libelle, Ordre = input.Ordre ?? (await db.Categories.MaxAsync(x => (int?)x.Ordre) ?? 0) + 1 };
            db.Categories.Add(c);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = c.Id });
        });
        g.MapPut("/categories/{id:guid}", async (Guid id, CategorieInput input, NegoceDbContext db) =>
        {
            var c = await db.Categories.FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return Results.NotFound();
            if (!string.IsNullOrWhiteSpace(input.Libelle))
            {
                var libelle = input.Libelle.Trim();
                if (await db.Categories.AnyAsync(x => x.Libelle == libelle && x.Id != id)) return Conflit("Cette catégorie existe déjà.");
                c.Libelle = libelle;
            }
            if (input.Ordre is { } o) c.Ordre = o;
            if (input.Actif is { } a) c.Actif = a;
            c.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = c.Id });
        });

        // Articles
        g.MapGet("/articles", async (string? recherche, Guid? categorieId, string? origine, bool? actif, bool? sousSeuil, NegoceDbContext db, GestionStock stock, DroitsNegoce droits, ClaimsPrincipal user) =>
        {
            var q = db.Articles.AsNoTracking().Include(a => a.Categorie).Include(a => a.Unite).AsQueryable();
            if (!string.IsNullOrWhiteSpace(recherche))
            {
                var t = recherche.Trim().ToLower();
                q = q.Where(a => a.Code.ToLower().Contains(t) || a.Designation.ToLower().Contains(t));
            }
            if (categorieId is { } c) q = q.Where(a => a.CategorieId == c);
            if (!string.IsNullOrWhiteSpace(origine)) q = q.Where(a => a.Origine == origine);
            if (actif is { } ac) q = q.Where(a => a.Actif == ac);
            var articles = await q.OrderBy(a => a.Categorie!.Ordre).ThenBy(a => a.Designation).ToListAsync();
            var stocks = await stock.StocksAsync();
            var voitCouts = await droits.VoitPrixAchatAsync(user);
            var liste = articles.Select(a => Vue(a, stocks.GetValueOrDefault(a.Id), voitCouts));
            if (sousSeuil == true) liste = liste.Where(a => a.sousSeuil);
            return Results.Ok(liste);
        });
        g.MapGet("/articles/{id:guid}", async (Guid id, NegoceDbContext db, GestionStock stock, DroitsNegoce droits, ClaimsPrincipal user) =>
        {
            var a = await db.Articles.AsNoTracking().Include(x => x.Categorie).Include(x => x.Unite).FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            var voitCouts = await droits.VoitPrixAchatAsync(user);
            var prix = !voitCouts ? null : await (from p in db.PrixFournisseurs.AsNoTracking() where p.ArticleId == id
                join f in db.Fournisseurs.AsNoTracking() on p.FournisseurId equals f.Id orderby p.PrixAchat
                select new { id = p.Id, fournisseurId = f.Id, fournisseur = f.Nom, prixAchat = p.PrixAchat, misAJourLe = p.UpdatedAt ?? p.CreatedAt }).ToListAsync();
            return Results.Ok(new { article = Vue(a, await stock.StockAsync(id), voitCouts), prixFournisseurs = prix });
        });
        g.MapPost("/articles", async (ArticleInput input, NegoceDbContext db, DroitsNegoce droits, ClaimsPrincipal user) =>
        {
            var a = new Article();
            if (await Appliquer(a, input, db, droits, user, creation: true) is { } erreur) return erreur;
            db.Articles.Add(a);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = a.Id });
        });
        g.MapPut("/articles/{id:guid}", async (Guid id, ArticleInput input, NegoceDbContext db, DroitsNegoce droits, ClaimsPrincipal user) =>
        {
            var a = await db.Articles.FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            if (await Appliquer(a, input, db, droits, user, creation: false) is { } erreur) return erreur;
            a.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = a.Id });
        });

        // Prix de vente d'un client : prix public et prix de son groupe pour chaque article actif.
        g.MapGet("/articles/prix", async (Guid? clientId, NegoceDbContext db) =>
        {
            var groupe = clientId is { } c ? await GroupeDuClientAsync(db, c) : null;
            var articles = await db.Articles.AsNoTracking().Where(a => a.Actif).ToListAsync();
            return Results.Ok(new
            {
                groupe = groupe is null ? null : new { id = groupe.Id, nom = groupe.Nom },
                prix = articles.Select(a =>
                {
                    var regle = groupe is null ? null : Tarification.RegleApplicable(groupe.Regles, a.Id, a.CategorieId);
                    return new { articleId = a.Id, prixPublic = a.PrixVente, prixClient = Tarification.PrixPour(a.PrixVente, regle), regle = regle?.Type };
                }),
            });
        });

        // Prix d'achat par fournisseur : réservé aux fonctions qui les voient.
        g.MapGet("/prix-achat", async (Guid? articleId, Guid? fournisseurId, NegoceDbContext db) =>
        {
            var q = from p in db.PrixFournisseurs.AsNoTracking()
                    join a in db.Articles.AsNoTracking() on p.ArticleId equals a.Id
                    join f in db.Fournisseurs.AsNoTracking() on p.FournisseurId equals f.Id
                    where (articleId == null || p.ArticleId == articleId) && (fournisseurId == null || p.FournisseurId == fournisseurId)
                    orderby a.Designation, f.Nom
                    select new { id = p.Id, articleId = a.Id, code = a.Code, designation = a.Designation, fournisseurId = f.Id, fournisseur = f.Nom, prixAchat = p.PrixAchat, misAJourLe = p.UpdatedAt ?? p.CreatedAt };
            return Results.Ok(await q.ToListAsync());
        });
        g.MapPut("/prix-achat", async (PrixAchatInput input, NegoceDbContext db) =>
        {
            if (input.PrixAchat < 0) return Invalide("prixAchat", "Le prix d'achat ne peut pas être négatif.");
            var article = await db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == input.ArticleId);
            if (article is null) return Invalide("articleId", "Article introuvable.");
            if (article.Origine == OriginesArticle.Produit) return Conflit("Un article produit n'a pas de prix d'achat : son coût moyen se saisit sur sa fiche.");
            if (!await db.Fournisseurs.AnyAsync(f => f.Id == input.FournisseurId)) return Invalide("fournisseurId", "Fournisseur introuvable.");
            var p = await db.PrixFournisseurs.FirstOrDefaultAsync(x => x.ArticleId == input.ArticleId && x.FournisseurId == input.FournisseurId);
            if (p is null) db.PrixFournisseurs.Add(p = new PrixFournisseur { ArticleId = input.ArticleId, FournisseurId = input.FournisseurId, PrixAchat = input.PrixAchat });
            else { p.PrixAchat = input.PrixAchat; p.UpdatedAt = DateTime.UtcNow; }
            await db.SaveChangesAsync();
            return Results.Ok(new { id = p.Id });
        });
        g.MapDelete("/prix-achat/{id:guid}", async (Guid id, NegoceDbContext db) =>
        {
            var p = await db.PrixFournisseurs.FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            db.PrixFournisseurs.Remove(p);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    /// <summary>Groupe d'un client, avec ses règles, ou null.</summary>
    internal static async Task<GroupeClient?> GroupeDuClientAsync(NegoceDbContext db, Guid clientId)
    {
        var m = await db.MembresGroupe.AsNoTracking().FirstOrDefaultAsync(x => x.ClientId == clientId);
        return m is null ? null : await db.Groupes.AsNoTracking().Include(x => x.Regles).FirstOrDefaultAsync(x => x.Id == m.GroupeId && x.Actif);
    }

    private record ArticleVue(Guid id, string code, string designation, Guid categorieId, string? categorie, Guid uniteId, string? unite, string? uniteSymbole,
        decimal prixVente, string origine, decimal? coutMoyen, decimal? valeurStock, decimal seuilAlerte, decimal? kgParMetre, bool actif, decimal stock, bool sousSeuil);

    private static ArticleVue Vue(Article a, decimal stock, bool voitCouts) => new(
        a.Id, a.Code, a.Designation, a.CategorieId, a.Categorie?.Libelle, a.UniteId, a.Unite?.Libelle, a.Unite?.Symbole, a.PrixVente, a.Origine,
        voitCouts ? a.CoutMoyen : null, voitCouts ? Math.Round(stock * a.CoutMoyen, 2) : null, a.SeuilAlerte, a.KgParMetre, a.Actif, stock,
        a.Actif && a.SeuilAlerte > 0 && stock <= a.SeuilAlerte);

    /// <summary>Reporte la saisie sur l'article. Le prix de vente et le coût moyen ne se modifient qu'avec le droit de leur module.</summary>
    private static async Task<IResult?> Appliquer(Article a, ArticleInput input, NegoceDbContext db, DroitsNegoce droits, ClaimsPrincipal user, bool creation)
    {
        var code = (input.Code ?? "").Trim();
        var designation = (input.Designation ?? "").Trim();
        if (code.Length == 0) return Invalide("code", "Le code est obligatoire.");
        if (designation.Length == 0) return Invalide("designation", "La désignation est obligatoire.");
        var origine = input.Origine ?? (creation ? OriginesArticle.Achete : a.Origine);
        if (!OriginesArticle.Valide(origine)) return Invalide("origine", "L'origine est « Acheté » ou « Produit ».");
        if (await db.Articles.AnyAsync(x => x.Code == code && x.Id != a.Id)) return Conflit($"Le code « {code} » est déjà utilisé.");
        if (!await db.Categories.AnyAsync(c => c.Id == input.CategorieId && (c.Actif || c.Id == a.CategorieId))) return Invalide("categorieId", "Catégorie introuvable ou désactivée.");
        if (!await db.Unites.AnyAsync(u => u.Id == input.UniteId && (u.Actif || u.Id == a.UniteId))) return Invalide("uniteId", "Unité introuvable ou désactivée.");
        if (input.SeuilAlerte is < 0) return Invalide("seuilAlerte", "Le seuil ne peut pas être négatif.");
        if (input.KgParMetre is <= 0) return Invalide("kgParMetre", "Le poids au mètre doit être supérieur à zéro.");
        if (input.PrixVente is < 0) return Invalide("prixVente", "Le prix de vente ne peut pas être négatif.");
        if (input.CoutMoyen is < 0) return Invalide("coutMoyen", "Le coût moyen ne peut pas être négatif.");

        a.Code = code; a.Designation = designation; a.CategorieId = input.CategorieId; a.UniteId = input.UniteId; a.Origine = origine;
        if (input.SeuilAlerte is { } s) a.SeuilAlerte = s;
        if (creation || input.KgParMetre is not null) a.KgParMetre = input.KgParMetre;
        if (input.Actif is { } actif) a.Actif = actif;
        if (input.PrixVente is { } pv && await droits.ModifiePrixVenteAsync(user)) a.PrixVente = pv;
        // Un article acheté prend son coût moyen des réceptions ; seul l'article produit le reçoit par saisie.
        if (origine == OriginesArticle.Produit && input.CoutMoyen is { } cm && await droits.ModifiePrixAchatAsync(user)) a.CoutMoyen = cm;
        return null;
    }
}
