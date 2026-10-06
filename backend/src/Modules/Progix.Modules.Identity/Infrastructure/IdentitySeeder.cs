using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Progix.Modules.Identity.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Identity.Infrastructure;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<IdentityModuleDbContext>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Progix.Identity");
        await db.Database.MigrateAsync(ct);

        var roleManager = sp.GetRequiredService<RoleManager<AppRole>>();
        foreach (var def in Roles.All)
        {
            var role = await roleManager.FindByNameAsync(def.Code);
            if (role is null)
            {
                await roleManager.CreateAsync(new AppRole { Name = def.Code, Label = def.Label, VoitMontants = def.VoitMontants });
            }
            else if (role.Label != def.Label || role.VoitMontants != def.VoitMontants)
            {
                role.Label = def.Label;
                role.VoitMontants = def.VoitMontants;
                await roleManager.UpdateAsync(role);
            }
        }

        var config = sp.GetRequiredService<IConfiguration>();
        var userManager = sp.GetRequiredService<UserManager<AppUser>>();
        // Reprise des comptes créés quand l'adresse email servait d'identifiant : ils reçoivent un identifiant prénom.nom.
        foreach (var ancien in await userManager.Users.Where(u => u.UserName!.Contains("@")).ToListAsync(ct))
        {
            var racine = string.Join('.', ancien.UserName!.Split('@')[0].Split('.').Select(Application.Logins.Partie).Where(x => x.Length > 0));
            await userManager.SetUserNameAsync(ancien, await Application.Logins.LibreAsync(userManager, racine.Length == 0 ? "utilisateur" : racine));
        }
        var adminEmail = config["Seed:AdminEmail"] ?? "directeur@progix.local";
        var adminPassword = config["Seed:AdminPassword"] ?? "Progix#2026";
        // Le premier directeur se connecte toujours avec l'identifiant « directeur », quelle que soit son adresse.
        await EnsureUserAsync(userManager, logger, "Directeur PROGIX", adminEmail, adminPassword, Roles.Directeur, "directeur");

        if (config.GetValue("Seed:DemoUsers", false))
        {
            // Un compte de démonstration par rôle, pour tester l'adaptation de l'interface.
            foreach (var def in Roles.All.Where(r => r.Code != Roles.Directeur))
            {
                var email = def.Code.Replace('_', '.') + "@progix.local";
                await EnsureUserAsync(userManager, logger, def.Label + " (démo)", email, adminPassword, def.Code);
            }
        }
    }

    private static async Task EnsureUserAsync(UserManager<AppUser> users, ILogger logger, string fullName, string email, string password, string role, string? login = null)
    {
        if (await users.FindByEmailAsync(email) is not null) return;
        // Identifiant de connexion : la partie de l'adresse avant l'arobase (directeur, chef.projet...).
        if (login is not null && await users.FindByNameAsync(login) is not null) return;
        var user = new AppUser { UserName = login ?? email.Split('@')[0].ToLowerInvariant(), Email = email, FullName = fullName, EmailConfirmed = true };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            logger.LogError("Création du compte {Email} impossible : {Errors}", email, string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }
        await users.AddToRoleAsync(user, role);
        logger.LogInformation("Compte créé : {Email} ({Role})", email, role);
    }
}
