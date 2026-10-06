using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

/// <summary>
/// Version des plans d'exécution d'une dalle : un plan (dwg), un modèle de calcul et d'autres fichiers éventuels.
/// La première version porte la lettre A ; chaque nouvelle version incrémente la lettre dans la référence de la dalle.
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
}
