using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Identity.Application;
using Progix.Modules.Identity.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Identity.Endpoints;

public static class AuthEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Authentification");

        auth.MapPost("/login", async (LoginRequest request, UserManager<AppUser> users, RoleManager<AppRole> roles, TokenService tokens, HttpContext http, IConfiguration config) =>
        {
            var user = await users.FindByNameAsync((request.Login ?? "").Trim().ToLowerInvariant());
            // Cinq mots de passe faux de suite bloquent le compte quelques minutes : une protection contre les essais en série.
            if (user is not null && await users.IsLockedOutAsync(user))
                return Results.Problem("Trop d'essais : ce compte est bloqué quelques minutes. Réessayez plus tard.", statusCode: StatusCodes.Status429TooManyRequests, title: "compte_bloque");
            if (user is null || !await users.CheckPasswordAsync(user, request.Password))
            {
                if (user is not null) await users.AccessFailedAsync(user);
                return Results.Problem("Identifiant ou mot de passe incorrect.", statusCode: StatusCodes.Status401Unauthorized, title: "identifiants_invalides");
            }
            await users.ResetAccessFailedCountAsync(user);
            if (!user.IsActive)
                return Results.Problem("Compte désactivé. Adressez-vous au directeur pour le réactiver.", statusCode: StatusCodes.Status403Forbidden, title: "compte_desactive");

            var roleCode = (await users.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty;
            var (token, expiresAt) = tokens.Create(user, roleCode);
            http.Response.Cookies.Append(JwtOptions.CookieName, token, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                // Derrière le serveur frontal, la requête arrive en HTTP : l'adresse publique dit si le site est servi en HTTPS.
                Secure = http.Request.IsHttps || (config["App:PublicUrl"] ?? "").StartsWith("https://", StringComparison.OrdinalIgnoreCase),
                Expires = expiresAt,
                Path = "/",
            });
            user.LastLoginAt = DateTime.UtcNow;
            await users.UpdateAsync(user);
            return Results.Ok(await ToDtoAsync(user, roleCode, roles));
        }).AllowAnonymous();

        auth.MapPost("/logout", (HttpContext http) =>
        {
            http.Response.Cookies.Delete(JwtOptions.CookieName, new CookieOptions { Path = "/" });
            return Results.NoContent();
        }).AllowAnonymous();

        auth.MapGet("/me", async (ClaimsPrincipal principal, UserManager<AppUser> users, RoleManager<AppRole> roles) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null || !user.IsActive) return Results.Unauthorized();
            var roleCode = (await users.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty;
            return Results.Ok(await ToDtoAsync(user, roleCode, roles));
        }).RequireAuthorization();

        auth.MapPut("/me", async (UpdateProfileRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, RoleManager<AppRole> roles) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.FullName))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["fullName"] = ["Le nom complet est obligatoire."] });
            user.FullName = request.FullName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
            await users.UpdateAsync(user);
            var roleCode = (await users.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty;
            return Results.Ok(await ToDtoAsync(user, roleCode, roles));
        }).RequireAuthorization();

        auth.MapPost("/change-password", async (ChangePasswordRequest request, ClaimsPrincipal principal, UserManager<AppUser> users) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            return result.Succeeded
                ? Results.NoContent()
                : Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = result.Errors.Select(e => e.Description).ToArray() });
        }).RequireAuthorization();

        app.MapGet("/api/roles", () => Roles.All.Select(r => new RoleDto(r.Code, r.Label, r.VoitMontants)))
            .WithTags("Utilisateurs").RequireAuthorization();

        var admin = app.MapGroup("/api/users").WithTags("Utilisateurs").RequireAuthorization(Policies.GestionUtilisateurs);

        admin.MapGet("/", async (UserManager<AppUser> users, RoleManager<AppRole> roles) =>
        {
            var list = await users.Users.OrderBy(u => u.FullName).ToListAsync();
            var result = new List<UserDto>();
            foreach (var u in list)
            {
                var roleCode = (await users.GetRolesAsync(u)).FirstOrDefault() ?? string.Empty;
                result.Add(await ToDtoAsync(u, roleCode, roles));
            }
            return Results.Ok(result);
        });

        // Identifiant qui sera attribué pour ce prénom et ce nom, homonymes compris.
        admin.MapGet("/login-propose", async (string? prenom, string? nom, UserManager<AppUser> users) =>
            Results.Ok(new { login = Logins.Partie(prenom).Length == 0 || Logins.Partie(nom).Length == 0 ? "" : await Logins.LibreAsync(users, Logins.Base(prenom, nom)) }));

        admin.MapPost("/", async (CreateUserRequest request, UserManager<AppUser> users, RoleManager<AppRole> roles, ClaimsPrincipal principal) =>
        {
            if (request.Role == Roles.Directeur && !principal.IsInRole(Roles.Directeur)) return Results.Problem("Seul un directeur crée ou modifie un compte de directeur.", statusCode: StatusCodes.Status403Forbidden, title: "action_interdite");
            if (Roles.Find(request.Role) is null)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["Rôle inconnu."] });
            if (Logins.Partie(request.Prenom).Length == 0 || Logins.Partie(request.Nom).Length == 0)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["nom"] = ["Le prénom et le nom sont obligatoires : ils forment l'identifiant de connexion."] });
            if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@')) return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Adresse email invalide."] });
            if (await users.FindByEmailAsync(request.Email.Trim()) is not null)
                return Results.Problem("Un utilisateur porte déjà cette adresse email.", statusCode: StatusCodes.Status409Conflict, title: "doublon");
            var login = await Logins.LibreAsync(users, Logins.Base(request.Prenom, request.Nom));
            var user = new AppUser { UserName = login, Email = request.Email.Trim(), FullName = $"{request.Prenom.Trim()} {request.Nom.Trim()}", EmailConfirmed = true };
            var created = await users.CreateAsync(user, request.Password);
            if (!created.Succeeded)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = created.Errors.Select(e => e.Description).ToArray() });
            await users.AddToRoleAsync(user, request.Role);
            return Results.Created($"/api/users/{user.Id}", await ToDtoAsync(user, request.Role, roles));
        });

        admin.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, UserManager<AppUser> users, RoleManager<AppRole> roles, ClaimsPrincipal principal) =>
        {
            var user = await users.FindByIdAsync(id.ToString());
            if (user is null) return Results.NotFound();
            if (Roles.Find(request.Role) is null)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["Rôle inconnu."] });
            var self = users.GetUserId(principal) == user.Id.ToString();
            var actuel = (await users.GetRolesAsync(user)).FirstOrDefault();
            if (!principal.IsInRole(Roles.Directeur) && (actuel == Roles.Directeur || request.Role == Roles.Directeur)) return Results.Problem("Seul un directeur crée ou modifie un compte de directeur.", statusCode: StatusCodes.Status403Forbidden, title: "action_interdite");
            if (self && (!request.IsActive || request.Role != actuel))
                return Results.Problem("Vous ne pouvez pas désactiver votre propre compte ni changer votre propre rôle.", statusCode: StatusCodes.Status409Conflict, title: "action_interdite");
            if (string.IsNullOrWhiteSpace(request.FullName)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["fullName"] = ["Le nom est obligatoire."] });
            var email = (request.Email ?? "").Trim();
            if (!email.Contains('@')) return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Adresse email invalide."] });
            if (await users.FindByEmailAsync(email) is { } autre && autre.Id != user.Id)
                return Results.Problem("Un utilisateur porte déjà cette adresse email.", statusCode: StatusCodes.Status409Conflict, title: "doublon");
            // L'identifiant de connexion, lui, ne change jamais.
            user.Email = email; user.NormalizedEmail = users.NormalizeEmail(email);
            user.FullName = request.FullName.Trim();
            user.IsActive = request.IsActive;
            await users.UpdateAsync(user);
            var current = await users.GetRolesAsync(user);
            if (!current.Contains(request.Role))
            {
                await users.RemoveFromRolesAsync(user, current);
                await users.AddToRoleAsync(user, request.Role);
            }
            return Results.Ok(await ToDtoAsync(user, request.Role, roles));
        });

        // Le directeur redonne un mot de passe à un compte : l'ancien n'est pas demandé.
        admin.MapPost("/{id:guid}/reset-password", async (Guid id, ResetPasswordRequest request, UserManager<AppUser> users, ClaimsPrincipal principal) =>
        {
            var user = await users.FindByIdAsync(id.ToString());
            if (user is null) return Results.NotFound();
            if (!principal.IsInRole(Roles.Directeur) && await users.IsInRoleAsync(user, Roles.Directeur)) return Results.Problem("Seul un directeur crée ou modifie un compte de directeur.", statusCode: StatusCodes.Status403Forbidden, title: "action_interdite");
            // Mêmes règles de mot de passe qu'à la création du compte.
            var erreurs = new List<string>();
            foreach (var v in users.PasswordValidators)
            {
                var r = await v.ValidateAsync(users, user, request.NewPassword ?? "");
                if (!r.Succeeded) erreurs.AddRange(r.Errors.Select(e => e.Description));
            }
            if (erreurs.Count > 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = erreurs.ToArray() });
            user.PasswordHash = users.PasswordHasher.HashPassword(user, request.NewPassword!);
            await users.UpdateSecurityStampAsync(user);
            return Results.NoContent();
        });
    }

    private static async Task<UserDto> ToDtoAsync(AppUser user, string roleCode, RoleManager<AppRole> roles)
    {
        var role = string.IsNullOrEmpty(roleCode) ? null : await roles.FindByNameAsync(roleCode);
        return new UserDto(user.Id, user.FullName, user.Email ?? string.Empty, roleCode, role?.Label ?? roleCode, role?.VoitMontants ?? false, user.IsActive, user.PhoneNumber, user.LastLoginAt, user.UserName ?? "");
    }
}
