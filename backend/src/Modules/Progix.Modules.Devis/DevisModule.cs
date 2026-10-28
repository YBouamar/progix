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
        // Reprise : le commercial se choisit désormais sur le projet. Celui du devis le plus récent (non rejeté) devient celui du projet qui n'en a pas.
        var statut = scope.ServiceProvider.GetRequiredService<IProjetsStatut>();
        var commerciaux = await db.Devis.AsNoTracking().Where(d => d.CommercialId != null && d.Etat != Domain.EtatsDevis.Annule)
            .OrderByDescending(d => d.CreatedAt).Select(d => new { d.ProjetId, d.CommercialId, d.CommercialNom }).ToListAsync(ct);
        foreach (var c in commerciaux.DistinctBy(c => c.ProjetId))
            await statut.DefinirCommercialSiAbsentAsync(c.ProjetId, c.CommercialId!.Value, c.CommercialNom ?? "", ct);
    }
}
