using Microsoft.AspNetCore.Identity;
using Progix.Modules.Identity.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Identity.Infrastructure;

public class UtilisateursLookup(UserManager<AppUser> users, RoleManager<AppRole> fonctions) : IUtilisateursLookup
{
    public async Task<bool> VerifierMotDePasseAsync(Guid userId, string motDePasse)
    {
        var u = await users.FindByIdAsync(userId.ToString());
        return u is { IsActive: true } && await users.CheckPasswordAsync(u, motDePasse);
    }

    public async Task<IReadOnlyList<UtilisateurFiche>> ParRolesAsync(IEnumerable<string> roles, CancellationToken ct = default)
    {
        var liste = new List<UtilisateurFiche>();
        // Une fonction créée compte pour sa fonction de référence : un « ingénieur stagiaire » reste un ingénieur.
        var creees = fonctions.Roles.Where(f => f.Base != null).Select(f => new { f.Name, f.Base }).ToList();
        foreach (var role in roles.Distinct())
            foreach (var code in creees.Where(f => f.Base == role).Select(f => f.Name!).Prepend(role))
                liste.AddRange((await users.GetUsersInRoleAsync(code)).Where(u => u.IsActive).Select(u => new UtilisateurFiche(u.Id, u.FullName, role)));
        return liste.OrderBy(u => u.Nom).ToList();
    }

    public async Task<IReadOnlyList<Presence>> PresencesAsync(IEnumerable<string> roles, CancellationToken ct = default)
    {
        var liste = new List<Presence>();
        var creees = fonctions.Roles.Where(f => f.Base != null).Select(f => new { f.Name, f.Base }).ToList();
        foreach (var role in roles.Distinct())
            foreach (var code in creees.Where(f => f.Base == role).Select(f => f.Name!).Prepend(role))
                liste.AddRange((await users.GetUsersInRoleAsync(code)).Select(u => new Presence(u.Id, u.FullName, role, u.IsActive, u.DateArrivee, u.DateDepart)));
        return liste.OrderBy(u => u.Nom).ToList();
    }
}
