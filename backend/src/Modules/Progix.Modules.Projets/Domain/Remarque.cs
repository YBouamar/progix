using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

/// <summary>Remarque signée et datée. Toute modification ou suppression reste tracée.</summary>
public class Remarque : Entity
{
    public Guid ProjetId { get; set; }
    public Guid AuteurId { get; set; }
    public string AuteurNom { get; set; } = "";
    public string AuteurRole { get; set; } = "";
    public string Texte { get; set; } = "";
    public string? ModifiePar { get; set; }
    public DateTime? ModifieLe { get; set; }
    public bool Supprime { get; set; }
    public string? SupprimePar { get; set; }
    public DateTime? SupprimeLe { get; set; }
    public List<RemarqueTrace> Traces { get; set; } = [];
}

public class RemarqueTrace : Entity
{
    public Guid RemarqueId { get; set; }
    /// <summary>« modification » ou « suppression ».</summary>
    public string Action { get; set; } = "";
    public string AncienTexte { get; set; } = "";
    public string Par { get; set; } = "";
}
