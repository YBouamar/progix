using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Progix.Modules.Fichiers.Endpoints;
using Progix.Modules.Fichiers.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Fichiers;

public class FichiersModule : IModule
{
    public string Name => "Fichiers";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FichiersDbContext>(o => o.UseNpgsql(
            configuration.GetConnectionString("Postgres"),
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations", FichiersDbContext.Schema)));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.Section));
        services.AddSingleton<Stockage>();
        services.AddScoped<PurgeFichiers>();
        services.AddScoped<IFichiersLookup, FichiersLookup>();

    }

    public void MapEndpoints(IEndpointRouteBuilder app) => FichiersEndpoints.Map(app);

    public async Task InitializeAsync(IServiceProvider services, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<FichiersDbContext>().Database.MigrateAsync(ct);
        try
        {
            await services.GetRequiredService<Stockage>().PreparerAsync(ct);
        }
        catch (Exception ex)
        {
            // L'application démarre quand même : seuls les dépôts de fichiers échoueront tant que le stockage est absent.
            services.GetRequiredService<ILoggerFactory>().CreateLogger("Fichiers")
                .LogWarning("Stockage de fichiers indisponible au démarrage : {Message}", ex.Message);
        }
    }
}
