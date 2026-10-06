using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Domain;

/// <summary>Contrat établi sur un devis validé. Numéro CTR-XXXXX-AAAA, par entité et par an. Il ne se modifie pas ; il s'annule avec un motif.</summary>
public class Contrat : Entity
{
    public string Numero { get; set; } = "";
    public string Entite { get; set; } = "";
    public string? AncienNumero { get; set; }
    public Guid ProjetId { get; set; }
    public Guid DevisId { get; set; }
    public string DevisNumero { get; set; } = "";
    public DateOnly DateContrat { get; set; }
    public string Lieu { get; set; } = "Casablanca";
    public string EmisPar { get; set; } = "";

    // Signature électronique par mot de passe : obligatoire avant d'imprimer ou d'envoyer le contrat.
    public string? SignePar { get; set; }
    public DateTime? SigneLe { get; set; }
    public string? Jeton { get; set; }

    public bool Annule { get; set; }
    public string? MotifAnnulation { get; set; }
    public string? AnnulePar { get; set; }
    public DateTime? AnnuleLe { get; set; }
}
