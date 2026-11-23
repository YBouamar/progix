using Progix.Modules.Negoce.Domain;

namespace Progix.Modules.Negoce.Application;

/// <summary>
/// Quand un devis ou une commande de vente peut disparaître au lieu d'être annulé.
/// Le devis ouvert non signé est un brouillon ; la commande est un document interne, jamais signé : elle se supprime tant que rien n'est livré ni facturé.
/// </summary>
public static class Suppression
{
    /// <summary>Message de refus, ou null si le devis se supprime. <paramref name="aCommande"/> : une commande existe encore (même annulée).</summary>
    public static string? RefusDevis(DocumentVente devis, bool aCommande)
    {
        if (devis.Jeton is not null) return "Ce devis est signé : il ne se supprime plus. Annulez-le avec un motif.";
        if (devis.Etat == EtatsVente.Accepte || aCommande) return "Ce devis est devenu une commande : supprimez d'abord la commande.";
        return null;
    }

    /// <summary>Message de refus, ou null si la commande se supprime. <paramref name="documentsLies"/> : bons de livraison et factures, annulés compris.</summary>
    public static string? RefusCommande(int documentsLies) =>
        documentsLies > 0 ? "Cette commande a des bons de livraison ou des factures : elle ne se supprime plus. Annulez-la avec un motif une fois ces documents annulés." : null;
}
