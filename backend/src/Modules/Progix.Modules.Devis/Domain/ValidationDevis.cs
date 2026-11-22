namespace Progix.Modules.Devis.Domain;

/// <summary>
/// Un projet n'a qu'un seul devis validé. À la validation d'un devis, les autres devis du projet encore ouverts (brouillon ou envoyé) sont annulés.
/// Les devis expirés, remplacés ou déjà rejetés gardent leur état : ils n'étaient plus en jeu.
/// </summary>
public static class ValidationDevis
{
    /// <summary>Motif de refus : un autre devis du projet est déjà validé. Null si la validation est possible.</summary>
    public static string? Refus(DevisEntete a_valider, IEnumerable<DevisEntete> autres) =>
        autres.FirstOrDefault(x => x.Id != a_valider.Id && x.ProjetId == a_valider.ProjetId && x.Etat == EtatsDevis.Valide) is { } deja
            ? $"Le devis {deja.Numero} (version {deja.Version}) est déjà validé pour ce projet : un seul devis se valide. Rejetez-le ou créez-en une nouvelle version."
            : null;

    /// <summary>Devis du projet à annuler quand celui-ci est validé : les autres brouillons et devis envoyés.</summary>
    public static List<DevisEntete> AAnnuler(DevisEntete valide, IEnumerable<DevisEntete> autres) =>
        autres.Where(x => x.Id != valide.Id && x.ProjetId == valide.ProjetId && x.Etat is EtatsDevis.Brouillon or EtatsDevis.Envoye).ToList();

    public static string MotifAnnulation(DevisEntete valide) => $"Annulé automatiquement : le devis {valide.Numero} (version {valide.Version}) a été validé pour ce projet.";
}
