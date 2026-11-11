using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

/// <summary>
/// Version des plans d'exécution d'une dalle : un plan (dwg), un modèle de calcul et d'autres fichiers éventuels.
/// La première version porte la lettre A ; chaque nouvelle version incrémente la lettre dans la référence de la dalle.
/// Chaque version est aussi un dossier d'exécution : EXE pour la première, MODIF pour les suivantes.
/// </summary>
public class VersionPlan : Entity
{
    public const string Contexte = "plan_execution";
    public const string Plan = "plan";
    public const string Modele = "modele";
    public const string Autre = "autre";

    public const string Fige = "Les plans sont figés depuis la validation du BCT.";

    public Guid DalleId { get; set; }
    public char Lettre { get; set; }
    public string? Commentaire { get; set; }
    public string CreePar { get; set; } = "";

    // Suivi du dossier d'exécution que porte cette version (voir ExecutionDalle).
    public string Etat { get; set; } = ExecutionDalle.EnCours;
    public DateTime? TermineLe { get; set; }
    public string? TerminePar { get; set; }
    /// <summary>Surface de la dalle relevée à la clôture, en m².</summary>
    public decimal? Surface { get; set; }
    /// <summary>Dessinateurs affectés à la clôture : identifiants séparés par des virgules, et leurs noms.</summary>
    public string? DessinateursIds { get; set; }
    public string? DessinateursNoms { get; set; }
    public string? ArretMotif { get; set; }
    /// <summary>Date d'envoi des plans.</summary>
    public DateOnly? EnvoyeLe { get; set; }
}
