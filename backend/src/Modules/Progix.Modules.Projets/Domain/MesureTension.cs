using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

/// <summary>
/// Mesure d'un câble à la mise en tension, d'après la fiche de l'entreprise : lectures à 25 % et à 100 % de la force, côté A et côté B.
/// Allongement d'un côté = lecture à 100 % moins lecture à 25 % ; allongement du câble = côté A plus côté B.
/// Tolérance EN 13670 : ± 15 % de l'allongement théorique Ao pour un câble seul, ± 5 % pour le groupe de câbles de la dalle.
/// </summary>
public class MesureTension : Entity
{
    // Câble seul : entre 0,85 et 1,15 fois Ao.
    public const decimal BorneBasse = 0.85m;
    public const decimal BorneHaute = 1.15m;
    // Groupe : entre 0,95 et 1,05 fois la somme des Ao.
    public const decimal GroupeBas = 0.95m;
    public const decimal GroupeHaut = 1.05m;

    public Guid DalleId { get; set; }
    public int Ordre { get; set; }
    public string Cable { get; set; } = "";
    /// <summary>Longueur du câble d'ancrage à ancrage, en mètres. Sert à calculer l'allongement théorique.</summary>
    public decimal? Longueur { get; set; }
    /// <summary>Allongement théorique Ao, en millimètres.</summary>
    public decimal Theorique { get; set; }
    public decimal? A25 { get; set; }
    public decimal? A100 { get; set; }
    /// <summary>Côté B : renseigné seulement si le câble est tendu des deux côtés.</summary>
    public decimal? B25 { get; set; }
    public decimal? B100 { get; set; }

    public decimal? DeltaA => A100 - A25;
    public decimal? DeltaB => B100 - B25;
    /// <summary>Un côté B commencé doit être complet ; laissé vide, il compte pour zéro.</summary>
    public bool CoteBIncomplet => (B25 is null) != (B100 is null);
    public decimal? Mesure => DeltaA is decimal a && !CoteBIncomplet ? a + (DeltaB ?? 0) : null;
    /// <summary>Moyenne du tendon : moyenne des deux côtés, ou le côté A seul si le câble est tendu d'un seul côté.</summary>
    public decimal? Moyenne => Mesure is null ? null : DeltaB is decimal b ? Math.Round((DeltaA!.Value + b) / 2, 1) : DeltaA;
    public decimal Min => Math.Round(Theorique * BorneBasse, 1);
    public decimal Max => Math.Round(Theorique * BorneHaute, 1);
    public decimal? Ecart => Mesure is decimal m && Theorique > 0 ? Math.Round((m - Theorique) / Theorique * 100, 1) : null;
    public bool? Conforme => Mesure is decimal m && Theorique > 0 ? m >= Theorique * BorneBasse && m <= Theorique * BorneHaute : null;
}

/// <summary>En-tête de la fiche de mise en tension d'une dalle : force appliquée, CR et CMP, saisis à la main.</summary>
public class FicheTension : Entity
{
    public Guid DalleId { get; set; }
    public decimal? ForceKn { get; set; }
    public string? Cr { get; set; }
    public string? Cmp { get; set; }
}

/// <summary>
/// Constantes du calcul de l'allongement théorique : allongement = P × L / (A × E), diminué des pertes par frottement.
/// Valeurs par défaut : toron Y1860S7 de 15,7 mm (150 mm², E = 195 000 MPa), force de 221 kN soit 0,9 × fp0,1k (EN 1992-1-1).
/// </summary>
public class ParametresTension
{
    public int Id { get; set; } = 1;
    public decimal ForceKn { get; set; } = 221m;
    public decimal SectionMm2 { get; set; } = 150m;
    public decimal ModuleMpa { get; set; } = 195000m;
    public decimal PertesPct { get; set; } = 5m;
    public string? ModifiePar { get; set; }
    public DateTime? ModifieLe { get; set; }

    /// <summary>Allongement théorique en millimètres par mètre de câble.</summary>
    public decimal MmParMetre => SectionMm2 > 0 && ModuleMpa > 0
        ? Math.Round(ForceKn * 1000m * (1 - PertesPct / 100m) / (SectionMm2 * ModuleMpa) * 1000m, 3) : 0;
}
