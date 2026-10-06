using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Domain;

/// <summary>Dépense générale (hors projet : loyer, salaires, véhicules, fournitures) ou dépense rattachée à une dalle précise. Elle s'annule avec un motif, sans se modifier.</summary>
public class Depense : Entity
{
    public string Entite { get; set; } = "";
    public DateOnly DateDepense { get; set; }
    public string Libelle { get; set; } = "";
    public decimal Montant { get; set; }
    public string Mode { get; set; } = ModesPaiement.Virement;
    public string? Reference { get; set; }
    // Dépense de dalle : renseignés ensemble, vides pour une dépense générale.
    public Guid? ProjetId { get; set; }
    public Guid? DalleId { get; set; }
    public string? ProjetCode { get; set; }
    public string? DalleReference { get; set; }
    /// <summary>Catégorie paramétrée, figée à la saisie.</summary>
    public string? Categorie { get; set; }

    public string SaisiPar { get; set; } = "";

    public bool Annule { get; set; }
    public string? MotifAnnulation { get; set; }
    public string? AnnulePar { get; set; }
    public DateTime? AnnuleLe { get; set; }
}

/// <summary>Catégorie de dépense de dalle, paramétrable.</summary>
public class CategorieDepense : Entity
{
    public string Libelle { get; set; } = "";
    public bool Actif { get; set; } = true;
    public int Ordre { get; set; }

    public static readonly string[] Defauts = ["Acier de précontrainte", "Main-d'œuvre de pose", "Transport"];
}
