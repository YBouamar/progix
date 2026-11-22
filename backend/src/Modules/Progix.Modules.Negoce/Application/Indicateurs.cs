using Progix.Modules.Negoce.Domain;

namespace Progix.Modules.Negoce.Application;

/// <summary>
/// Calculs des indicateurs du tableau de bord des ventes directes, des achats et du stock : purs, testables sans base.
/// Mêmes conventions qu'en post-tension : le chiffre d'affaires se compte en TTC, après avoirs.
/// </summary>
public static class Indicateurs
{
    /// <summary>Période précédente : le même nombre de mois entiers quand la période en couvre, sinon le même nombre de jours.</summary>
    public static (DateOnly Du, DateOnly Au) Precedente(DateOnly du, DateOnly au)
    {
        if (du.Day == 1 && au.AddDays(1).Day == 1)
        {
            var mois = (au.Year - du.Year) * 12 + au.Month - du.Month + 1;
            return (du.AddMonths(-mois), du.AddDays(-1));
        }
        var jours = au.DayNumber - du.DayNumber + 1;
        return (du.AddDays(-jours), du.AddDays(-1));
    }

    public static bool Dans(DateOnly d, DateOnly du, DateOnly au) => d >= du && d <= au;

    /// <summary>Montant TTC dû d'une facture, avoirs non annulés déduits.</summary>
    public static decimal Du(DocumentVente facture, IEnumerable<DocumentVente> avoirs) =>
        facture.TotalTTC - avoirs.Where(a => !a.Annule).Sum(a => a.TotalTTC);

    /// <summary>Chiffre d'affaires : factures émises sur la période (hors annulées), TTC après avoirs.</summary>
    public static decimal ChiffreAffaires(IEnumerable<DocumentVente> factures, ILookup<Guid?, DocumentVente> avoirs, DateOnly du, DateOnly au) =>
        factures.Where(f => !f.Annule && Dans(f.DateDocument, du, au)).Sum(f => Du(f, avoirs[f.Id]));

    /// <summary>Encaissements de la période : paiements non annulés des factures non annulées.</summary>
    public static decimal Encaisse(IEnumerable<DocumentVente> factures, DateOnly du, DateOnly au) =>
        factures.Where(f => !f.Annule).SelectMany(f => f.Paiements).Where(p => !p.Annule && Dans(p.DatePaiement, du, au)).Sum(p => p.Montant);

    /// <summary>Marge brute HT des livraisons de la période : prix de vente moins coût moyen au moment de la sortie. Les avoirs ne la corrigent pas.</summary>
    public static decimal MargeBrute(IEnumerable<DocumentVente> livraisons, DateOnly du, DateOnly au) =>
        livraisons.Where(b => !b.Annule && Dans(b.DateDocument, du, au))
            .SelectMany(b => b.Lignes).Sum(l => Montants.LigneHT(l.Quantite, l.PrixUnitaire) - Montants.Arrondir(l.Quantite * l.CoutUnitaire));

    /// <summary>Part des devis de la période (hors annulés) devenus commande ; null sans devis.</summary>
    public static decimal? TauxConversion(IEnumerable<DocumentVente> devis, DateOnly du, DateOnly au)
    {
        var periode = devis.Where(d => !d.Annule && Dans(d.DateDocument, du, au)).ToList();
        return periode.Count == 0 ? null : Math.Round(periode.Count(d => d.Etat == EtatsVente.Accepte) * 100m / periode.Count, 1);
    }

    /// <summary>Commandes ouvertes dont au moins une ligne reste à livrer.</summary>
    public static int ALivrer(IEnumerable<DocumentVente> commandes, ILookup<Guid?, DocumentVente> enfants) =>
        commandes.Count(c => c.Etat == EtatsVente.Ouvert && c.Lignes.Any(l => Avancement.ResteALivrer(l, enfants[c.Id]) > 0));

    /// <summary>Livré mais pas encore facturé, en HT : on ne facture que ce qui est livré.</summary>
    public static (int Nb, decimal MontantHt) AFacturer(IEnumerable<DocumentVente> commandes, ILookup<Guid?, DocumentVente> enfants)
    {
        var ouvertes = commandes.Where(c => c.Etat == EtatsVente.Ouvert).ToList();
        var montants = ouvertes.Select(c => c.Lignes.Sum(l => Montants.LigneHT(Math.Max(0, Avancement.ResteAFacturer(l, enfants[c.Id])), l.PrixUnitaire))).ToList();
        return (montants.Count(m => m > 0), montants.Sum());
    }

    /// <summary>Article sous son seuil d'alerte ; un article sans seuil n'est jamais signalé.</summary>
    public static bool SousSeuil(decimal stock, decimal seuil) => seuil > 0 && stock <= seuil;
}
