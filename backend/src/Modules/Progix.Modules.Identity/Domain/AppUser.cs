using Microsoft.AspNetCore.Identity;

namespace Progix.Modules.Identity.Domain;

public class AppUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Dates d'arrivée et de départ dans l'entreprise : elles donnent les mois travaillés de l'évaluation des dessinateurs.</summary>
    public DateOnly? DateArrivee { get; set; }
    public DateOnly? DateDepart { get; set; }
}

public class AppRole : IdentityRole<Guid>
{
    public string Label { get; set; } = string.Empty;
    public bool VoitMontants { get; set; }

    /// <summary>
    /// Fonction de référence d'une fonction créée : l'une des douze fonctions d'origine, dont elle prend les validations et les signatures.
    /// Null pour une fonction d'origine.
    /// </summary>
    public string? Base { get; set; }
}

/// <summary>Niveau réglé d'une fonction sur un module, quand il s'écarte de celui de sa fonction de référence.</summary>
public class Habilitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoleId { get; set; }
    public string Module { get; set; } = string.Empty;
    public int Niveau { get; set; }
}
