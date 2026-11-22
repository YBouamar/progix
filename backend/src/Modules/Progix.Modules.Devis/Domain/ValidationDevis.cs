namespace Progix.Modules.Devis.Domain;

/// <summary>
/// Un projet n'a qu'un seul devis validé. À la validation d'un devis, les autres devis du projet encore ouverts (brouillon ou envoyé) sont annulés.
/// Les devis expirés, remplacés ou déjà rejetés gardent leur état : ils n'étaient plus en jeu.
/// Projet lancé (en réalisation ou terminé) : une variante peut être validée par le client alors qu'un devis l'est déjà ;
/// le devis validé jusque-là est alors remplacé (il n'est pas annulé : le chantier s'est fait sur lui).
/// </summary>
public static class ValidationDevis
{
    /// <summary>Motif de refus : un autre devis du projet est déjà validé. Null si la validation est possible (toujours, sur un projet lancé).</summary>
    public static string? Refus(DevisEntete a_valider, IEnumerable<DevisEntete> autres, bool projetLance = false) =>
        !projetLance && autres.FirstOrDefault(x => x.Id != a_valider.Id && x.ProjetId == a_valider.ProjetId && x.Etat == EtatsDevis.Valide) is { } deja
            ? $"Le devis {deja.Numero} (version {deja.Version}) est déjà validé pour ce projet : un seul devis se valide. Rejetez-le ou créez-en une nouvelle version."
            : null;

    /// <summary>Devis du projet à annuler quand celui-ci est validé : les autres brouillons et devis envoyés.</summary>
    public static List<DevisEntete> AAnnuler(DevisEntete valide, IEnumerable<DevisEntete> autres) =>
        autres.Where(x => x.Id != valide.Id && x.ProjetId == valide.ProjetId && x.Etat is EtatsDevis.Brouillon or EtatsDevis.Envoye).ToList();

    /// <summary>Projet lancé : devis déjà validés que la variante validée remplace.</summary>
    public static List<DevisEntete> ARemplacer(DevisEntete valide, IEnumerable<DevisEntete> autres, bool projetLance) =>
        !projetLance ? [] : autres.Where(x => x.Id != valide.Id && x.ProjetId == valide.ProjetId && x.Etat == EtatsDevis.Valide).ToList();

    public static string MotifAnnulation(DevisEntete valide) => $"Annulé automatiquement : le devis {valide.Numero} (version {valide.Version}) a été validé pour ce projet.";

    /// <summary>Motif de refus de l'envoi : le brouillon doit d'abord être validé en interne. Null si l'envoi est possible.</summary>
    public static string? RefusEnvoi(DevisEntete d) => d.RevueEtat switch
    {
        RevuesDevis.Validee => null,
        RevuesDevis.Refusee => "Ce brouillon a été refusé à la relecture : corrigez-le d'après la remarque, il doit ensuite être validé par le chef de projet ou le directeur avant l'envoi.",
        _ => "Le brouillon doit d'abord être validé par le chef de projet ou le directeur avant d'être marqué envoyé.",
    };

    /// <summary>Relecture du brouillon : validation, ou refus avec une remarque obligatoire. Renvoie un motif de refus, sinon null.</summary>
    public static string? Reviser(DevisEntete d, bool valide, string? remarque, string par, DateTime le)
    {
        if (d.Etat != EtatsDevis.Brouillon) return "Seul un brouillon se relit : les devis déjà envoyés suivent leur propre circuit.";
        if (!valide && string.IsNullOrWhiteSpace(remarque)) return "La remarque est obligatoire pour refuser un brouillon : dites pourquoi il n'est pas validé.";
        d.RevueEtat = valide ? RevuesDevis.Validee : RevuesDevis.Refusee;
        d.RevuePar = par; d.RevueLe = le;
        d.RevueRemarque = string.IsNullOrWhiteSpace(remarque) ? null : remarque.Trim();
        return null;
    }

    /// <summary>Le contenu du brouillon a changé : la relecture ne vaut plus. Renvoie vrai s'il y en avait une.</summary>
    public static bool ReinitialiserRevue(DevisEntete d)
    {
        var avait = d.RevueEtat is not null;
        d.RevueEtat = null; d.RevuePar = null; d.RevueLe = null; d.RevueRemarque = null;
        return avait;
    }

    /// <summary>
    /// État retrouvé d'une version remplacée quand la version qui l'avait remplacée (un brouillon) est supprimée :
    /// validée si elle l'avait été, sinon envoyée (l'expiration se recalcule ensuite). Null si l'état ne se déduit pas.
    /// </summary>
    public static string? EtatAvantRemplacement(DevisEntete d) =>
        d.Etat != EtatsDevis.Remplace ? null : d.ValideLe is not null ? EtatsDevis.Valide : d.EnvoyeLe is not null ? EtatsDevis.Envoye : null;
}

/// <summary>Résultats de la relecture interne d'un brouillon.</summary>
public static class RevuesDevis
{
    public const string Validee = "validee";
    public const string Refusee = "refusee";
}
