using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Progix.Modules.Devis.Endpoints;
using Progix.Modules.Devis.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Devis;

public class DevisModule : IModule
{
    public string Name => "Devis";

    public void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddScoped<IDevisLookup, Infrastructure.DevisLookup>().AddScoped<IContexteFichiers, Infrastructure.FichiersDevis>().AddScoped<Infrastructure.DevisDocuments>()
            .AddScoped<IDevisDocuments>(sp => sp.GetRequiredService<Infrastructure.DevisDocuments>()).AddScoped<IVerificationDocuments>(sp => sp.GetRequiredService<Infrastructure.DevisDocuments>()).AddScoped<IReaffectationEntite, Infrastructure.NumerotationDevis>().AddDbContext<DevisDbContext>(o => o.UseNpgsql(
            configuration.GetConnectionString("Postgres"),
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations", DevisDbContext.Schema)));

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        DevisEndpoints.Map(app);
        DevisEtatsEndpoints.Map(app);
        DevisImportEndpoints.Map(app);
    }

    public async Task InitializeAsync(IServiceProvider services, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevisDbContext>();
        await db.Database.MigrateAsync(ct);
        await Infrastructure.NumerotationDevis.ReprendreAsync(db, scope.ServiceProvider.GetRequiredService<IProjetsLookup>(), ct);
    }
}
