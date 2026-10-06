using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Domain;

public static class TypesCourrier
{
    public const string BordereauBct = "bordereau_bct";
    public const string BordereauBet = "bordereau_bet";
    public const string Attestation = "attestation";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [BordereauBct] = "Bordereau d'envoi au bureau de contrôle", [BordereauBet] = "Bordereau de dépôt au bureau d'études", [Attestation] = "Attestation de conformité",
    };

    /// <summary>Préfixe du numéro : les deux bordereaux partagent une série, les attestations ont la leur.</summary>
    public static string Prefixe(string type) => type == Attestation ? "ATT" : "BRD";
}

/// <summary>Courrier émis sur un projet : bordereau de plans ou attestation de conformité. Le contenu est figé à l'émission.</summary>
public class Courrier : Entity
{
    public string Numero { get; set; } = "";
    public string Entite { get; set; } = "";
    public string? AncienNumero { get; set; }
    public string Type { get; set; } = "";
    public Guid ProjetId { get; set; }
    /// <summary>Bureau destinataire d'un bordereau : sa numérotation se tient par bureau et par an.</summary>
    public Guid? DestinataireId { get; set; }
    public DateOnly DateCourrier { get; set; }
    public string Lieu { get; set; } = "Casablanca";

    public string Objet { get; set; } = "";
    public string? Entreprise { get; set; }
    public string? Ville { get; set; }
    public string? Bct { get; set; }
    public string? Bet { get; set; }
    /// <summary>Bordereau portant des plans modifiés.</summary>
    public bool Modification { get; set; }
    public List<LigneCourrier> Lignes { get; set; } = [];

    public string EmisPar { get; set; } = "";
    public string? SignePar { get; set; }
    public DateTime? SigneLe { get; set; }
    public string? Jeton { get; set; }

    public bool Annule { get; set; }
    public string? MotifAnnulation { get; set; }
    public string? AnnulePar { get; set; }
    public DateTime? AnnuleLe { get; set; }
}

/// <summary>Plan transmis (bordereau) ou dalle attestée (attestation).</summary>
public class LigneCourrier
{
    public Guid? DalleId { get; set; }
    public string? Reference { get; set; }
    /// <summary>Numéro de plan, saisi à la main.</summary>
    public string? NumeroPlan { get; set; }
    public string Designation { get; set; } = "";
    /// <summary>Nombre d'exemplaires, saisi à la main.</summary>
    public int Nombre { get; set; }
    public decimal? Surface { get; set; }
}
