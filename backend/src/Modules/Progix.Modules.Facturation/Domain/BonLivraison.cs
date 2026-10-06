using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Domain;

/// <summary>Bon de livraison. Une fois émis, il ne se modifie pas et ne se supprime pas.</summary>
public class BonLivraison : Entity
{
    /// <summary>Numéro BL-XXXXX-AAAA, attribué à l'émission.</summary>
    public string Numero { get; set; } = "";

    /// <summary>Entité émettrice : chaque entité a sa propre numérotation.</summary>
    public string Entite { get; set; } = "";

    /// <summary>Numéro porté avant une réaffectation du projet à une autre entité.</summary>
    public string? AncienNumero { get; set; }
    public Guid ProjetId { get; set; }
    public DateOnly DateLivraison { get; set; }
    public decimal Surface { get; set; }
    public string EmisPar { get; set; } = "";

    // Signature électronique par mot de passe : obligatoire avant d'imprimer ou d'envoyer le bon.
    public string? SignePar { get; set; }
    public DateTime? SigneLe { get; set; }
    public string? Jeton { get; set; }

    /// <summary>Un bon de livraison ne peut être facturé qu'une fois.</summary>
    public Guid? FactureId { get; set; }
    public List<LigneLivraison> Lignes { get; set; } = [];
}

/// <summary>Dalle livrée. Une dalle ne figure que sur un seul bon de livraison.</summary>
public class LigneLivraison : Entity
{
    public Guid BonLivraisonId { get; set; }
    public Guid DalleId { get; set; }
    public int Ordre { get; set; }
    public string Reference { get; set; } = "";
    public string Emplacement { get; set; } = "";
    public decimal Surface { get; set; }
}

public class Compteur
{
    public string Cle { get; set; } = "";
    public int Valeur { get; set; }
}
