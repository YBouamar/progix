namespace Progix.Modules.Devis.Domain;

/// <summary>
/// Suppression d'une dalle présente dans des devis : seuls les devis déjà validés (ou remplacés après validation) la retiennent.
/// Dans un devis brouillon, envoyé ou expiré, la dalle est retirée et le prix recalculé ; un devis rejeté garde sa ligne telle quelle.
/// </summary>
public static class RetraitDalle
{
    /// <summary>Le devis est-il un obstacle à la suppression de la dalle ? Vrai pour un devis validé ou remplacé.</summary>
    public static bool Bloque(string etat) => etat is EtatsDevis.Valide or EtatsDevis.Remplace;

    /// <summary>Un devis que la suppression de la dalle modifie : brouillon, envoyé ou expiré.</summary>
    public static bool Modifiable(string etat) => etat is EtatsDevis.Brouillon or EtatsDevis.Envoye or EtatsDevis.Expire;

    /// <summary>
    /// Retire la dalle du devis et recalcule ses totaux. Un devis déjà envoyé ou expiré redevient brouillon : sa signature, son envoi
    /// et sa relecture ne valent plus, il faut le relire, le signer et le renvoyer. Renvoie vrai si le devis contenait la dalle.
    /// </summary>
    public static bool Retirer(DevisEntete d, Guid dalleId, IReadOnlyList<TrancheForfait> grille, bool exonereTva, decimal? forfaitPropre)
    {
        if (d.Lignes.RemoveAll(l => l.DalleId == dalleId) == 0) return false;
        var ordre = 1;
        foreach (var l in d.Lignes.OrderBy(l => l.Ordre)) l.Ordre = ordre++;
        Calcul.Recalculer(d, grille, exonereTva, forfaitPropre);
        if (d.Etat != EtatsDevis.Brouillon)
        {
            d.Etat = EtatsDevis.Brouillon;
            d.EnvoyeLe = null; d.SignePar = null; d.SigneLe = null; d.Jeton = null;
        }
        ValidationDevis.ReinitialiserRevue(d);
        return true;
    }
}
