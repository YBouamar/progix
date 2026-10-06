using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

/// <summary>Rôles de partenaire sur un projet. Le code est aussi le type attendu du partenaire.</summary>
public static class RolesPartenaire
{
    public static readonly IReadOnlyList<(string Code, string Label)> All =
    [
        ("bet", "Bureau d'études"),
        ("bct", "Bureau de contrôle"),
        ("architecte", "Architecte"),
        ("laboratoire", "Laboratoire"),
    ];
}

public class ProjetPartenaire : Entity
{
    public Guid ProjetId { get; set; }
    public string Role { get; set; } = "";
    public Guid PartenaireId { get; set; }
}
