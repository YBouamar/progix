using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Progix.Modules.Partenaires.Endpoints;
using Progix.Modules.Partenaires.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Partenaires;

public class PartenairesModule : IModule
{
    public string Name => "Partenaires";

    public void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddScoped<IPartenairesLookup, PartenairesLookup>().AddDbContext<PartenairesDbContext>(o => o.UseNpgsql(
            configuration.GetConnectionString("Postgres"),
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations", PartenairesDbContext.Schema)));

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        PartenairesEndpoints.Map(app);
        ImportPartenaires.Map(app);
    }

    public async Task InitializeAsync(IServiceProvider services, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PartenairesDbContext>();
        await db.Database.MigrateAsync(ct);
        // « Autre » a été brièvement un type : c'est désormais une catégorie.
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE partenaires.partenaires SET \"Type\" = 'client_final', \"Nature\" = 'autre', \"RaisonSociale\" = COALESCE(\"RaisonSociale\", NULLIF(TRIM(CONCAT(\"Prenom\", ' ', \"Nom\")), '')) WHERE \"Type\" = 'autre'", ct);
    }
}
