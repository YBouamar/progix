namespace Progix.Modules.Projets.Domain;

/// <summary>
/// Suppression d'une dalle créée par erreur : permise seulement tant que rien n'en dépend.
/// Dès qu'une étape est faite, qu'un import ADAPT, une étude, une exécution, une commande ou une fiche existe,
/// ou que la dalle figure dans un devis validé ou un document de facturation, elle se garde (l'historique ne se perd pas). Un devis pas encore validé la perd.
/// </summary>
public static class SuppressionDalle
{
    /// <summary>Motif de refus tiré de la dalle elle-même, ou null si elle est encore vierge.</summary>
    public static string? Refus(Dalle d)
    {
        if (d.EtapesFaites > 0) return "des étapes du chantier sont déjà faites";
        if (d.Adapt is not null) return "les fichiers ADAPT ont été importés";
        if (d.DessinateurId is not null) return "un dessinateur lui est affecté";
        if (d.EtudePlanifiee is not null || d.EtudeTermineeLe is not null) return "son étude est planifiée ou terminée";
        if (d.Conforme is not null) return "un avis de conformité a été donné";
        return null;
    }

    public static string Message(string reference, string motif) =>
        $"La dalle {reference} ne peut pas être supprimée : {motif}.";
}
