using Progix.SharedKernel;

namespace Progix.Modules.Devis.Domain;

public static class EtatsDevis
{
    public const string Brouillon = "brouillon";
    public const string Envoye = "envoye";
    public const string Expire = "expire";
    public const string Valide = "valide";
    public const string Annule = "annule";
    public const string Remplace = "remplace";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Brouillon] = "Brouillon",
        [Envoye] = "Envoyé",
        [Expire] = "Expiré",
        [Valide] = "Validé",
        [Annule] = "Annulé",
        [Remplace] = "Remplacé",
    };
}

public class DevisEntete : Entity
{
    /// <summary>Numéro d'offre : DEV-XXXXX-AAAA, par an, commun à toutes les versions.</summary>
    public string Numero { get; set; } = "";

    /// <summary>Entité émettrice : chaque entité a sa propre numérotation.</summary>
    public string Entite { get; set; } = "";
    public int Version { get; set; } = 1;
    public Guid ProjetId { get; set; }
    public string Etat { get; set; } = EtatsDevis.Brouillon;
    public DateOnly DateDevis { get; set; }
    public int DureeValidite { get; set; } = 30;
    public Guid? CommercialId { get; set; }
    public string? CommercialNom { get; set; }

    /// <summary>Remise finale au m², appliquée sur la surface totale.</summary>
    public decimal RemiseM2 { get; set; }
    public string? RemiseMotif { get; set; }

    public List<Echeance> Echeances { get; set; } = [];
    public string? ModalitesContrat { get; set; }

    // Résultats du calcul, figés avec le devis.
    public decimal Forfait { get; set; }
    public bool ExonereTva { get; set; }
    public decimal SurfaceTotale { get; set; }
    public decimal TotalHT { get; set; }
    public decimal RemiseTotale { get; set; }
    public decimal NetHT { get; set; }
    public decimal Tva { get; set; }
    public decimal TotalTTC { get; set; }

    // Signature électronique par mot de passe : obligatoire avant d'imprimer ou d'envoyer le devis.
    public string? SignePar { get; set; }
    public DateTime? SigneLe { get; set; }
    public string? Jeton { get; set; }

    public string CreePar { get; set; } = "";
    public DateTime? EnvoyeLe { get; set; }
    public DateTime? ValideLe { get; set; }
    public string? ValidePar { get; set; }
    public DateTime? AnnuleLe { get; set; }
    public string? AnnuleMotif { get; set; }
    public DateTime? RemplaceLe { get; set; }
    public List<EvenementDevis> Evenements { get; set; } = [];
    public List<LigneDevis> Lignes { get; set; } = [];

    public DateOnly Echeance => DateDevis.AddDays(DureeValidite);
}

/// <summary>Échéance de paiement en pourcentage. La retenue de garantie est comprise dans les 100 %.</summary>
public class Echeance
{
    public string Label { get; set; } = "";
    public decimal Pourcentage { get; set; }
    public bool Retenue { get; set; }
}

public class LigneDevis : Entity
{
    public Guid DevisId { get; set; }
    public Guid DalleId { get; set; }
    public int Ordre { get; set; }
    public string Reference { get; set; } = "";
    public string Emplacement { get; set; } = "";
    public decimal Surface { get; set; }
    public decimal RatioActif { get; set; }

    /// <summary>Saisi et repris sur les documents, sans effet sur le prix.</summary>
    public decimal RatioPassif { get; set; }
    public List<SystemeLigne> Systemes { get; set; } = [];
    public int PrixM2 { get; set; }
    public decimal MontantHT { get; set; }
}

/// <summary>Un système est désigné par son nombre de torons.</summary>
public class SystemeLigne
{
    public int Torons { get; set; }
    public decimal Ancrages { get; set; }
    public decimal Gaine { get; set; }
}

/// <summary>Historique du devis : quoi, qui, quand.</summary>
public class EvenementDevis : Entity
{
    public Guid DevisId { get; set; }
    public string Quoi { get; set; } = "";
    public string Qui { get; set; } = "";
}

/// <summary>Valeurs proposées à la création d'un devis, réglables dans le Paramétrage. Une seule ligne.</summary>
public class ParametresDevis
{
    public int Id { get; set; } = 1;
    public int DureeValidite { get; set; } = 30;
    public string? Modalites { get; set; }
    public List<Echeance> Echeances { get; set; } = [];
    public string? ModifiePar { get; set; }
    public DateTime? ModifieLe { get; set; }

    public const string ModalitesParDefaut =
        "Délais des études : plan d'exécution en 5 jours par dalle après réception du plan de coffrage, des réservations et des hypothèses de charges.\n" +
        "Modification des plans d'exécution : 10 DH par m². Béton : fck = 35 MPa.";

    public static List<Echeance> EcheancesParDefaut() =>
    [
        new() { Label = "Avance à la signature du contrat", Pourcentage = 30 },
        new() { Label = "De chaque dalle, à la livraison", Pourcentage = 60 },
        new() { Label = "Retenue de garantie, payable 6 mois après la livraison", Pourcentage = 10, Retenue = true },
    ];
}

public class CompteurDevis
{
    public string Cle { get; set; } = "";
    public int Valeur { get; set; }
}
