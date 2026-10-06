using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Domain;

/// <summary>Avoir : seule façon de corriger une facture émise. Numéro AV-XXXXX-AAAA, par entité et par an.</summary>
public class Avoir : Entity
{
    public string Numero { get; set; } = "";
    public string Entite { get; set; } = "";
    public string? AncienNumero { get; set; }
    public Guid FactureId { get; set; }
    public Guid ProjetId { get; set; }
    public DateOnly DateAvoir { get; set; }
    public string Motif { get; set; } = "";
    /// <summary>Avoir total : il annule la facture et libère ses bons de livraison ou son acompte.</summary>
    public bool Total { get; set; }
    public decimal MontantHT { get; set; }
    public decimal TauxTva { get; set; }
    public decimal Tva { get; set; }
    public decimal TotalTTC { get; set; }
    public string EmisPar { get; set; } = "";

    // Signature électronique par mot de passe : obligatoire avant d'imprimer ou d'envoyer le document.
    public string? SignePar { get; set; }
    public DateTime? SigneLe { get; set; }
    /// <summary>Jeton aléatoire porté par le QR code de vérification.</summary>
    public string? Jeton { get; set; }

    // Un avoir ne se modifie pas ; il s'annule avec un motif (directeur, responsable administratif).
    public bool Annule { get; set; }
    public string? MotifAnnulation { get; set; }
    public string? AnnulePar { get; set; }
    public DateTime? AnnuleLe { get; set; }
}
