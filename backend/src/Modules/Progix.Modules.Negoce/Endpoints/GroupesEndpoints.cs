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

public record RegleInput(Guid? ArticleId, Guid? CategorieId, string? Type, decimal Valeur);
public record GroupeInput(string? Nom, string? Description, bool? Actif, List<RegleInput>? Regles);
public record MembreInput(Guid ClientId);

/// <summary>Groupes de clients et leurs prix spéciaux : remise en pourcentage, remise en dirhams par unité, ou prix fixé par article.</summary>
public static class GroupesEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/negoce/groupes").WithTags("Négoce - groupes de clients").RequireAuthorization().AddEndpointFilter<ConcurrenceFilter>();

        g.MapGet("/", async (NegoceDbContext db) =>
            Results.Ok(await db.Groupes.AsNoTracking().OrderBy(x => x.Nom)
                .Select(x => new { id = x.Id, nom = x.Nom, description = x.Description, actif = x.Actif, membres = x.Membres.Count, regles = x.Regles.Count }).ToListAsync()));

        g.MapGet("/{id:guid}", async (Guid id, NegoceDbContext db, IPartenairesLookup partenaires) =>
        {
            var x = await db.Groupes.AsNoTracking().Include(y => y.Regles).Include(y => y.Membres).FirstOrDefaultAsync(y => y.Id == id);
            if (x is null) return Results.NotFound();
            var articles = await db.Articles.AsNoTracking().Where(a => x.Regles.Select(r => r.ArticleId).Contains(a.Id)).ToDictionaryAsync(a => a.Id);
            var categories = await db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Libelle);
            var noms = await partenaires.NomsAsync(x.Membres.Select(m => m.ClientId));
            return Results.Ok(new
            {
                id = x.Id, nom = x.Nom, description = x.Description, actif = x.Actif,
                regles = x.Regles.Select(r => new
                {
                    id = r.Id, articleId = r.ArticleId, article = r.ArticleId is { } a && articles.TryGetValue(a, out var art) ? $"{art.Code} {art.Designation}" : null,
                    prixPublic = r.ArticleId is { } a2 && articles.TryGetValue(a2, out var art2) ? art2.PrixVente : (decimal?)null,
                    categorieId = r.CategorieId, categorie = r.CategorieId is { } c && categories.TryGetValue(c, out var lib) ? lib : null, type = r.Type, valeur = r.Valeur,
                }).OrderBy(r => r.articleId is null ? 0 : 1).ThenBy(r => r.categorie).ThenBy(r => r.article),
                membres = x.Membres.Select(m => new { clientId = m.ClientId, nom = noms.GetValueOrDefault(m.ClientId, "Client supprimé") }).OrderBy(m => m.nom),
            });
        });

        g.MapGet("/client/{clientId:guid}", async (Guid clientId, NegoceDbContext db) =>
        {
            var m = await db.MembresGroupe.AsNoTracking().FirstOrDefaultAsync(x => x.ClientId == clientId);
            var groupe = m is null ? null : await db.Groupes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == m.GroupeId);
            return Results.Ok(groupe is null ? null : new { id = groupe.Id, nom = groupe.Nom, actif = groupe.Actif });
        });

        g.MapPost("/", async (GroupeInput input, NegoceDbContext db) =>
        {
            var x = new GroupeClient();
            if (await Appliquer(x, input, db) is { } erreur) return erreur;
            db.Groupes.Add(x);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = x.Id });
        });

        g.MapPut("/{id:guid}", async (Guid id, GroupeInput input, NegoceDbContext db) =>
        {
            var x = await db.Groupes.Include(y => y.Regles).FirstOrDefaultAsync(y => y.Id == id);
            if (x is null) return Results.NotFound();
            if (await Appliquer(x, input, db) is { } erreur) return erreur;
            x.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = x.Id });
        });

        // Un client n'est que dans un groupe : l'ajouter ici le retire de l'ancien.
        g.MapPost("/{id:guid}/membres", async (Guid id, MembreInput input, NegoceDbContext db, IPartenairesLookup partenaires) =>
        {
            if (!await db.Groupes.AnyAsync(x => x.Id == id)) return Results.NotFound();
            if ((await partenaires.NomsAsync([input.ClientId])).Count == 0) return Invalide("clientId", "Client introuvable.");
            var m = await db.MembresGroupe.FirstOrDefaultAsync(x => x.ClientId == input.ClientId);
            if (m is null) db.MembresGroupe.Add(new MembreGroupe { ClientId = input.ClientId, GroupeId = id });
            else m.GroupeId = id;
            await db.SaveChangesAsync();
            return Results.Ok(new { clientId = input.ClientId, groupeId = id });
        });

        g.MapDelete("/{id:guid}/membres/{clientId:guid}", async (Guid id, Guid clientId, NegoceDbContext db) =>
        {
            var m = await db.MembresGroupe.FirstOrDefaultAsync(x => x.ClientId == clientId && x.GroupeId == id);
            if (m is null) return Results.NotFound();
            db.MembresGroupe.Remove(m);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static async Task<IResult?> Appliquer(GroupeClient x, GroupeInput input, NegoceDbContext db)
    {
        var nom = (input.Nom ?? "").Trim();
        if (nom.Length == 0) return Invalide("nom", "Le nom du groupe est obligatoire.");
        if (await db.Groupes.AnyAsync(y => y.Nom == nom && y.Id != x.Id)) return Conflit("Un groupe porte déjà ce nom.");
        var regles = new List<RegleGroupe>();
        foreach (var r in input.Regles ?? [])
        {
            var regle = new RegleGroupe { ArticleId = r.ArticleId, CategorieId = r.CategorieId, Type = r.Type ?? "", Valeur = r.Valeur };
            if (regle.Refus() is { } refus) return Invalide("regles", refus);
            if (regles.Any(y => y.ArticleId == regle.ArticleId && y.CategorieId == regle.CategorieId)) return Conflit("Deux règles portent sur le même article ou la même catégorie.");
            regles.Add(regle);
        }
        var articles = regles.Where(r => r.ArticleId is not null).Select(r => r.ArticleId!.Value).ToList();
        if (articles.Count > 0 && await db.Articles.CountAsync(a => articles.Contains(a.Id)) != articles.Count) return Invalide("regles", "Un article des règles est introuvable.");
        var categories = regles.Where(r => r.CategorieId is not null).Select(r => r.CategorieId!.Value).ToList();
        if (categories.Count > 0 && await db.Categories.CountAsync(c => categories.Contains(c.Id)) != categories.Count) return Invalide("regles", "Une catégorie des règles est introuvable.");

        x.Nom = nom;
        x.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        if (input.Actif is { } a) x.Actif = a;
        if (input.Regles is not null)
        {
            db.ReglesGroupe.RemoveRange(x.Regles);
            x.Regles = regles;
        }
        return null;
    }
}
