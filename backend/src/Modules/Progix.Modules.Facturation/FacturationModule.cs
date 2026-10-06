using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Progix.Modules.Facturation.Endpoints;
using Progix.Modules.Facturation.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation;

/// <summary>Livraisons, factures et encaissements.</summary>
public class FacturationModule : IModule
{
    public string Name => "Facturation";

    public void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddScoped<IEncoursProjet, Infrastructure.EncoursFacturation>().AddScoped<IVerificationDocuments, Infrastructure.VerificationFacturation>().AddScoped<IReaffectationEntite, Infrastructure.NumerotationLivraisons>().AddDbContext<FacturationDbContext>(o => o.UseNpgsql(
            configuration.GetConnectionString("Postgres"),
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations", FacturationDbContext.Schema)));

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        LivraisonsEndpoints.Map(app);
        FacturesEndpoints.Map(app);
        EncaissementsEndpoints.Map(app);
        AvoirsEndpoints.Map(app);
        TresorerieEndpoints.Map(app);
        DepensesDalleEndpoints.Map(app);
        EntitesEndpoints.Map(app);
        PdfEndpoints.Map(app);
        TableauDeBordEndpoints.Map(app);
        ContratsEndpoints.Map(app);
        CourriersEndpoints.Map(app);
        FichesEndpoints.Map(app);
    }

    public async Task InitializeAsync(IServiceProvider services, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FacturationDbContext>();
        await db.Database.MigrateAsync(ct);
        Pdf.Polices.Installer();
        var codes = await db.Entites.Select(e => e.Code).ToListAsync(ct);
        db.Entites.AddRange(Domain.ParametresEntite.Defauts().Where(d => !codes.Contains(d.Code)));
        await db.SaveChangesAsync(ct);
        if (!await db.CategoriesDepense.AnyAsync(ct))
        {
            db.CategoriesDepense.AddRange(Domain.CategorieDepense.Defauts.Select((l, i) => new Domain.CategorieDepense { Libelle = l, Ordre = i }));
            await db.SaveChangesAsync(ct);
        }
        await Infrastructure.NumerotationLivraisons.ReprendreAsync(db, scope.ServiceProvider.GetRequiredService<IProjetsLookup>(), ct);
        await Infrastructure.NumerotationCourriers.ReprendreAsync(db, scope.ServiceProvider.GetRequiredService<IProjetsLookup>(), ct);
    }
}
