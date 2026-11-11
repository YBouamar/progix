using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Progix.Modules.Identity.Application;
using Progix.Modules.Identity.Domain;
using Progix.Modules.Identity.Endpoints;
using Progix.Modules.Identity.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Identity;

public class IdentityModule : IModule
{
    public string Name => "Identity";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityModuleDbContext>(o => o.UseNpgsql(
            configuration.GetConnectionString("Postgres"),
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations", IdentityModuleDbContext.Schema)));

        services.AddIdentityCore<AppUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 8;
                // Règle en service en production : ni majuscule ni symbole exigés.
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<AppRole>()
            .AddErrorDescriber<ErreursIdentite>()
            .AddEntityFrameworkStores<IdentityModuleDbContext>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.AddScoped<TokenService>();
        services.AddScoped<IUtilisateursLookup, UtilisateursLookup>();
        services.AddMemoryCache();
        services.AddScoped<HabilitationsService>();
        services.AddScoped<IHabilitations>(sp => sp.GetRequiredService<HabilitationsService>());

        var jwt = configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        if (jwt.Key.Length < 32)
            throw new InvalidOperationException("Jwt:Key doit contenir au moins 32 caractères.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = System.Security.Claims.ClaimTypes.Name,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                };
                // Le jeton voyage dans un cookie HttpOnly, jamais accessible au JavaScript du navigateur.
                o.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrEmpty(context.Token) && context.Request.Cookies.TryGetValue(JwtOptions.CookieName, out var cookie))
                            context.Token = cookie;
                        return Task.CompletedTask;
                    },
                    // Un compte désactivé, ou dont le rôle a changé, perd sa session tout de suite, sans attendre l'expiration du jeton.
                    OnTokenValidated = async context =>
                    {
                        var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
                        var id = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        var user = id is null ? null : await users.FindByIdAsync(id);
                        if (user is null || !user.IsActive) { context.Fail("Compte désactivé."); return; }
                        var role = (await users.GetRolesAsync(user)).FirstOrDefault();
                        if (role is null || !context.Principal!.IsInRole(role)) { context.Fail("Le rôle du compte a changé : reconnectez-vous."); return; }
                        // La fonction de la personne, et pour une fonction créée le rôle de sa fonction de référence : les règles
                        // de validation et de signature s'appuient sur les douze fonctions d'origine.
                        var fonctions = await context.HttpContext.RequestServices.GetRequiredService<HabilitationsService>().FonctionsAsync();
                        var identite = (System.Security.Claims.ClaimsIdentity)context.Principal.Identity!;
                        identite.AddClaim(new System.Security.Claims.Claim(Habilitations.ClaimFonction, role));
                        if (fonctions.TryGetValue(role, out var f) && !f.Origine)
                            identite.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, f.Reference));
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.GestionUtilisateurs, p => p.RequireRole(Roles.Directeur, Roles.ChefProjet))
            // Modules accordables : les fonctions d'origine prévues, ou toute fonction dont le niveau réglé suffit.
            .AddPolicy(Policies.Commercial, p => p.RequireAuthenticatedUser().RequireAssertion(c => Habilitations.AccordeAsync(c.User, c.Resource, Policies.RolesCommercial)))
            .AddPolicy(Policies.Devis, p => p.RequireAuthenticatedUser().RequireAssertion(c => Habilitations.AccordeAsync(c.User, c.Resource, Policies.RolesDevis)))
            .AddPolicy(Policies.Logistique, p => p.RequireAuthenticatedUser().RequireAssertion(c => Habilitations.AccordeAsync(c.User, c.Resource, Policies.RolesLogistique)))
            .AddPolicy(Policies.Direction, p => p.RequireRole(Policies.RolesDirection));
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        AuthEndpoints.Map(app);
        HabilitationsEndpoints.Map(app);
    }

    public Task InitializeAsync(IServiceProvider services, CancellationToken ct) => IdentitySeeder.SeedAsync(services, ct);
}
