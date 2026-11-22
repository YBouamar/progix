using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Negoce.Domain;
using Progix.Modules.Negoce.Endpoints;
using Progix.Modules.Negoce.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Negoce.Application;

public record LigneVenteInput(Guid ArticleId, decimal Quantite, decimal? PrixUnitaire);
public record DocumentVenteInput(string? Entite, Guid ClientId, Guid? ProjetId, DateOnly? DateDocument, DateOnly? Echeance, string? Notes, List<LigneVenteInput>? Lignes);
public record DupliquerInput(Guid? ClientId, string? Entite, Guid? ProjetId, bool RecalculerPrix);
public record LigneRepriseInput(Guid LigneId, decimal Quantite);

/// <summary>Ce qu'un document de vente reprend de son contexte : client, projet éventuel, groupe de prix.</summary>
public record ContexteVente(string Entite, PartenaireFiche Client, ProjetResume? Projet, GroupeClient? Groupe);

/// <summary>Règles communes aux documents de vente directe : contexte, prix des lignes, libellés.</summary>
public static class VentesSupport
{
    public static string EtatLabel(string type, string etat) => (type, etat) switch
    {
        (TypesVente.Commande, EtatsVente.Ouvert) => "En cours",
        (_, EtatsVente.Ouvert) => "Ouvert",
        (_, EtatsVente.Accepte) => "Accepté",
        (_, EtatsVente.Refuse) => "Refusé",
        (_, EtatsVente.Emis) => "Émis",
        (_, EtatsVente.Annule) => "Annulé",
        _ => etat,
    };

    /// <summary>
    /// Contexte d'une vente : l'entité est libre (elle peut différer de celle du projet), le client doit exister, le projet est facultatif.
    /// Renvoie le motif de refus à la place du contexte quand un élément est invalide.
    /// </summary>
    public static async Task<(ContexteVente? Contexte, IResult? Erreur)> ContexteAsync(string? entite, Guid clientId, Guid? projetId, NegoceDbContext db, IPartenairesLookup partenaires, IProjetsLookup projets)
    {
        var code = (entite ?? "").Trim().ToUpperInvariant();
        if (!Entites.Labels.ContainsKey(code)) return (null, Reponses.Invalide("entite", "Entité inconnue."));
        var client = (await partenaires.FichesAsync([clientId])).GetValueOrDefault(clientId);
        if (client is null) return (null, Reponses.Invalide("clientId", "Client introuvable."));
        ProjetResume? projet = null;
        if (projetId is { } p)
        {
            projet = await projets.ProjetAsync(p);
            if (projet is null || projet.Supprime) return (null, Reponses.Invalide("projetId", "Projet introuvable."));
        }
        return (new ContexteVente(code, client, projet, await CatalogueEndpoints.GroupeDuClientAsync(db, clientId)), null);
    }

    /// <summary>
    /// Lignes d'un devis ou d'une commande. Sans prix saisi, le prix est celui du groupe du client (ou le prix public) ;
    /// avec <paramref name="forcerPrixGroupe"/>, le prix saisi est ignoré (duplication avec recalcul).
    /// </summary>
    public static async Task<(List<LigneVente>? Lignes, IResult? Erreur)> LignesAsync(IEnumerable<LigneVenteInput>? entrees, GroupeClient? groupe, NegoceDbContext db, bool forcerPrixGroupe = false)
    {
        var liste = entrees?.ToList() ?? [];
        if (liste.Count == 0) return (null, Reponses.Invalide("lignes", "Ajoutez au moins une ligne."));
        var lignes = new List<LigneVente>();
        foreach (var e in liste)
        {
            if (e.Quantite <= 0) return (null, Reponses.Invalide("lignes", "Chaque quantité doit être supérieure à zéro."));
            if (e.PrixUnitaire is < 0) return (null, Reponses.Invalide("lignes", "Un prix ne peut pas être négatif."));
            var a = await db.Articles.AsNoTracking().Include(x => x.Unite).FirstOrDefaultAsync(x => x.Id == e.ArticleId);
            if (a is null) return (null, Reponses.Invalide("lignes", "Un article est introuvable."));
            if (!a.Actif) return (null, Reponses.Conflit($"L'article « {a.Designation} » est désactivé."));
            var prixGroupe = Tarification.PrixPour(a.PrixVente, groupe is null ? null : Tarification.RegleApplicable(groupe.Regles, a.Id, a.CategorieId));
            lignes.Add(new LigneVente
            {
                ArticleId = a.Id, Code = a.Code, Designation = a.Designation, Unite = a.Unite?.Symbole ?? "", Quantite = e.Quantite,
                PrixUnitaire = forcerPrixGroupe ? prixGroupe : e.PrixUnitaire ?? prixGroupe,
            });
        }
        return (lignes, null);
    }

    /// <summary>TVA du document : 20 %, sauf client exonéré.</summary>
    public static decimal Taux(PartenaireFiche client) => client.ExonereTva ? 0 : Tva.Taux;

    /// <summary>Reste à payer d'une facture : son total, moins les avoirs émis, moins les paiements non annulés.</summary>
    public static decimal ResteAPayer(DocumentVente facture, IEnumerable<DocumentVente> avoirs) =>
        facture.TotalTTC - avoirs.Where(a => !a.Annule).Sum(a => a.TotalTTC) - facture.Encaisse;

    public static string StatutPaiement(DocumentVente facture, decimal reste) =>
        facture.Annule ? "annulee" : reste <= 0.005m ? "payee" : facture.Encaisse > 0 ? "partielle" : "impayee";

    public static object Ligne(LigneVente l) => new
    {
        id = l.Id, articleId = l.ArticleId, code = l.Code, designation = l.Designation, unite = l.Unite, quantite = l.Quantite,
        prixUnitaire = l.PrixUnitaire, montantHt = Montants.LigneHT(l.Quantite, l.PrixUnitaire), sourceLigneId = l.SourceLigneId,
    };

    public static object Resume(DocumentVente d) => new
    {
        id = d.Id, type = d.Type, numero = d.Numero, entite = d.Entite, clientId = d.ClientId, client = d.ClientNom, projetId = d.ProjetId, projetCode = d.ProjetCode,
        date = d.DateDocument, echeance = d.Echeance, etat = d.Etat, etatLabel = EtatLabel(d.Type, d.Etat), totalHt = d.TotalHT, totalTva = d.TotalTVA, totalTtc = d.TotalTTC,
        parentId = d.ParentId, parentNumero = d.ParentNumero, signePar = d.SignePar, signeLe = d.SigneLe,
    };

    /// <summary>Champs communs du détail d'un document.</summary>
    public static Dictionary<string, object?> Detail(DocumentVente d) => new()
    {
        ["id"] = d.Id, ["type"] = d.Type, ["numero"] = d.Numero, ["entite"] = d.Entite, ["clientId"] = d.ClientId, ["client"] = d.ClientNom,
        ["projetId"] = d.ProjetId, ["projetCode"] = d.ProjetCode, ["projetLibelle"] = d.ProjetLibelle, ["groupeId"] = d.GroupeId, ["groupe"] = d.GroupeNom,
        ["parentId"] = d.ParentId, ["parentNumero"] = d.ParentNumero, ["date"] = d.DateDocument, ["echeance"] = d.Echeance, ["etat"] = d.Etat,
        ["etatLabel"] = EtatLabel(d.Type, d.Etat), ["tauxTva"] = d.TauxTva, ["notes"] = d.Notes, ["creePar"] = d.CreePar, ["signePar"] = d.SignePar, ["signeLe"] = d.SigneLe, ["motifAnnulation"] = d.MotifAnnulation,
        ["totalHt"] = d.TotalHT, ["totalTva"] = d.TotalTVA, ["totalTtc"] = d.TotalTTC, ["lignes"] = d.Lignes.OrderBy(l => l.CreatedAt).Select(Ligne).ToList(),
    };

    /// <summary>Reporte le contexte sur le document (client, projet, groupe, TVA).</summary>
    public static void Appliquer(DocumentVente d, ContexteVente c)
    {
        d.Entite = c.Entite;
        d.ClientId = c.Client.Id; d.ClientNom = c.Client.Nom; d.TauxTva = Taux(c.Client);
        d.ProjetId = c.Projet?.Id; d.ProjetCode = c.Projet?.Code; d.ProjetLibelle = c.Projet?.Libelle;
        d.GroupeId = c.Groupe?.Id; d.GroupeNom = c.Groupe?.Nom;
    }
}
