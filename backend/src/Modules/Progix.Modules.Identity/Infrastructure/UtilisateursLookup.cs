using Microsoft.AspNetCore.Identity;
using Progix.Modules.Identity.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Identity.Infrastructure;

public class UtilisateursLookup(UserManager<AppUser> users) : IUtilisateursLookup
{
    public async Task<bool> VerifierMotDePasseAsync(Guid userId, string motDePasse)
    {
        var u = await users.FindByIdAsync(userId.ToString());
        return u is { IsActive: true } && await users.CheckPasswordAsync(u, motDePasse);
    }

    public async Task<IReadOnlyList<UtilisateurFiche>> ParRolesAsync(IEnumerable<string> roles, CancellationToken ct = default)
    {
        var liste = new List<UtilisateurFiche>();
        foreach (var role in roles.Distinct())
            liste.AddRange((await users.GetUsersInRoleAsync(role)).Where(u => u.IsActive).Select(u => new UtilisateurFiche(u.Id, u.FullName, role)));
        return liste.OrderBy(u => u.Nom).ToList();
    }
}
