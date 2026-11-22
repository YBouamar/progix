using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Negoce.Domain;
using Progix.Modules.Negoce.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Negoce.Application;

/// <summary>Réponses d'erreur communes : message en français prêt à afficher.</summary>
public static class Reponses
{
    public static IResult Conflit(string message) => Results.Conflict(new { message });

    public static IResult Invalide(string champ, string message) => Results.ValidationProblem(new Dictionary<string, string[]> { [champ] = [message] });
}

/// <summary>Droits fins du négoce : le prix d'achat et le coût moyen ne sont visibles que de certaines fonctions.</summary>
public class DroitsNegoce(IHabilitations habilitations)
{
    public Task<int> NiveauAsync(ClaimsPrincipal user, string module, CancellationToken ct = default) => habilitations.NiveauAsync(user, module, ct);

    public async Task<bool> VoitPrixAchatAsync(ClaimsPrincipal user, CancellationToken ct = default) =>
        await NiveauAsync(user, Habilitations.NegocePrixAchat, ct) >= Habilitations.Lecture;

    public async Task<bool> ModifiePrixAchatAsync(ClaimsPrincipal user, CancellationToken ct = default) =>
        await NiveauAsync(user, Habilitations.NegocePrixAchat, ct) >= Habilitations.Ecriture;

    public async Task<bool> ModifiePrixVenteAsync(ClaimsPrincipal user, CancellationToken ct = default) =>
        await NiveauAsync(user, Habilitations.NegoceGroupes, ct) >= Habilitations.Ecriture;
}

/// <summary>Numéros des documents : devis, bons, factures et avoirs dans la série de l'entité, commandes dans celle du négoce.</summary>
public class Numeros(INumerotationDevis devis, INumerotationFacturation facturation, NegoceDbContext db)
{
    public Task<string> DevisAsync(string entite, int annee, CancellationToken ct = default) => devis.ProchainAsync(entite, annee, ct);

    public Task<string> FacturationAsync(string type, string entite, int annee, CancellationToken ct = default) => facturation.ProchainAsync(type, entite, annee, ct);

    public async Task<string> CommandeVenteAsync(string entite, int annee, CancellationToken ct = default) =>
        $"CMD-{await db.NumeroSuivantAsync($"cmd:{entite}:{annee}", ct):00000}-{annee}";

    public async Task<string> CommandeAchatAsync(string entite, int annee, CancellationToken ct = default) =>
        $"ACH-{await db.NumeroSuivantAsync($"ach:{entite}:{annee}", ct):00000}-{annee}";
}

/// <summary>Stock : somme des mouvements, valorisée au coût moyen pondéré.</summary>
public class GestionStock(NegoceDbContext db)
{
    /// <summary>Stock d'un article, mouvements non encore enregistrés compris : plusieurs lignes d'un même bon se cumulent.</summary>
    public async Task<decimal> StockAsync(Guid articleId, CancellationToken ct = default)
    {
        var enBase = await db.Mouvements.Where(m => m.ArticleId == articleId).SumAsync(m => (decimal?)m.Quantite, ct) ?? 0;
        var enAttente = db.ChangeTracker.Entries<MouvementStock>().Where(e => e.State == EntityState.Added && e.Entity.ArticleId == articleId).Sum(e => e.Entity.Quantite);
        return enBase + enAttente;
    }

    public async Task<Dictionary<Guid, decimal>> StocksAsync(CancellationToken ct = default) =>
        await db.Mouvements.GroupBy(m => m.ArticleId).Select(g => new { g.Key, Total = g.Sum(m => m.Quantite) }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);

    /// <summary>
    /// Ajoute un mouvement au contexte (l'appelant enregistre). Une entrée recalcule le coût moyen de l'article ;
    /// une sortie est valorisée à ce coût moyen. Renvoie le motif de refus si le mouvement est impossible.
    /// </summary>
    public async Task<(MouvementStock? Mouvement, string? Refus)> EnregistrerAsync(Article article, string type, decimal saisie, decimal? coutUnitaire, DateOnly date,
        string? motif, string par, string? pieceType = null, Guid? pieceId = null, string? pieceNumero = null, CancellationToken ct = default)
    {
        var stock = await StockAsync(article.Id, ct);
        article.VersionStock++;
        var (quantite, refus) = Valorisation.Signer(type, saisie, stock, motif);
        if (quantite is not { } q) return (null, refus);
        var cout = article.CoutMoyen;
        if (q > 0)
        {
            cout = coutUnitaire ?? article.CoutMoyen;
            if (cout < 0) return (null, "Le coût unitaire ne peut pas être négatif.");
            article.CoutMoyen = Valorisation.NouveauCoutMoyen(stock, article.CoutMoyen, q, cout);
            article.UpdatedAt = DateTime.UtcNow;
        }
        var m = new MouvementStock
        {
            ArticleId = article.Id, Type = type, Quantite = q, CoutUnitaire = cout, Date = date, Motif = motif?.Trim(),
            PieceType = pieceType, PieceId = pieceId, PieceNumero = pieceNumero, Par = par,
        };
        db.Mouvements.Add(m);
        return (m, null);
    }
}

/// <summary>
/// Protège le stock des saisies simultanées : si un autre enregistrement a touché le même article entre la lecture et l'écriture,
/// la base refuse (verrou optimiste sur l'article) et l'utilisateur recommence avec le stock à jour.
/// </summary>
public class ConcurrenceFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { return await next(context); }
        catch (DbUpdateConcurrencyException)
        {
            return Reponses.Conflit("Le stock de cet article a été modifié par une autre saisie en même temps. Rechargez l'écran et recommencez.");
        }
    }
}
