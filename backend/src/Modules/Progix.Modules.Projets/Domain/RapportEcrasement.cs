using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

/// <summary>
/// Rapport d'écrasement du laboratoire pour une dalle : la résistance mesurée et l'avis de conformité.
/// Un rapport ne se modifie pas ; un rapport non conforme reste dans l'historique et un nouveau rapport le remplace.
/// </summary>
public class RapportEcrasement : Entity
{
    public Guid DalleId { get; set; }
    public int Numero { get; set; }
    /// <summary>Résistance mesurée, en MPa. Vide pour un rapport repris d'avant la saisie de la valeur.</summary>
    public decimal? Valeur { get; set; }
    public bool Conforme { get; set; }
    public string? Remarque { get; set; }
    public string SaisiPar { get; set; } = "";
}
