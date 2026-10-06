using Microsoft.AspNetCore.Identity;

namespace Progix.Modules.Identity.Domain;

public class AppUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}

public class AppRole : IdentityRole<Guid>
{
    public string Label { get; set; } = string.Empty;
    public bool VoitMontants { get; set; }
}
