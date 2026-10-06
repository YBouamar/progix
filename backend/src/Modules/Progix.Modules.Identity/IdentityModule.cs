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
                o.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<AppRole>()
            .AddErrorDescriber<ErreursIdentite>()
            .AddEntityFrameworkStores<IdentityModuleDbContext>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.AddScoped<TokenService>();
        services.AddScoped<IUtilisateursLookup, UtilisateursLookup>();

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
                        if (role is null || !context.Principal!.IsInRole(role)) context.Fail("Le rôle du compte a changé : reconnectez-vous.");
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.GestionUtilisateurs, p => p.RequireRole(Roles.Directeur, Roles.ChefProjet))
            .AddPolicy(Policies.Commercial, p => p.RequireRole(Policies.RolesCommercial))
            .AddPolicy(Policies.Devis, p => p.RequireRole(Policies.RolesDevis))
            .AddPolicy(Policies.Logistique, p => p.RequireRole(Policies.RolesLogistique))
            .AddPolicy(Policies.Direction, p => p.RequireRole(Policies.RolesDirection));
    }

    public void MapEndpoints(IEndpointRouteBuilder app) => AuthEndpoints.Map(app);

    public Task InitializeAsync(IServiceProvider services, CancellationToken ct) => IdentitySeeder.SeedAsync(services, ct);
}
