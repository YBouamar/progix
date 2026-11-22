using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Progix.Modules.Negoce.Application;
using Progix.Modules.Negoce.Domain;
using Progix.Modules.Negoce.Endpoints;
using Progix.Modules.Negoce.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Negoce;

/// <summary>Ventes directes (poutrelles, hourdis, agglos...), achats et stock, avec les mêmes clients, projets et entités que la post-tension.</summary>
public class NegoceModule : IModule
{
    public string Name => "Négoce";

    public void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddScoped<DroitsNegoce>().AddScoped<Numeros>().AddScoped<GestionStock>()
            .AddScoped<IVerificationDocuments, VerificationNegoce>().AddScoped<IEspecesClient, EspecesNegoce>()
            .AddScoped<IContexteFichiers, FichiersFactureFournisseur>().AddScoped<IContexteFichiers, FichiersPaiementFournisseur>().AddScoped<IContexteFichiers, FichiersPaiementClient>()
            .AddDbContext<NegoceDbContext>(o => o.UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations", NegoceDbContext.Schema)));

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        CatalogueEndpoints.Map(app);
        GroupesEndpoints.Map(app);
        FournisseursEndpoints.Map(app);
        StockEndpoints.Map(app);
        SignaturesEndpoints.Map(app);
        AchatsEndpoints.Map(app);
        DevisEndpoints.Map(app);
        LivraisonsEndpoints.Map(app);
        FacturesEndpoints.Map(app);
    }

    public async Task InitializeAsync(IServiceProvider services, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NegoceDbContext>();
        await db.Database.MigrateAsync(ct);
        await DonneesDeReference.InstallerAsync(db, ct);
    }
}

/// <summary>Données de départ : unités et catégories usuelles, et le toron T15,7 (stocké en tonnes, 1,18 kg le mètre). Posées une seule fois.</summary>
public static class DonneesDeReference
{
    public static readonly (string Libelle, string Symbole)[] Unites =
        [("Pièce", "u"), ("Mètre", "m"), ("Mètre carré", "m²"), ("Mètre cube", "m³"), ("Tonne", "t"), ("Kilogramme", "kg"), ("Sac", "sac"), ("Palette", "palette")];

    public static readonly string[] Categories = ["Poutrelles", "Hourdis", "Agglos", "Post-tension", "Divers"];

    public static async Task InstallerAsync(NegoceDbContext db, CancellationToken ct)
    {
        if (!await db.Unites.AnyAsync(ct))
        {
            db.Unites.AddRange(Unites.Select(u => new Unite { Libelle = u.Libelle, Symbole = u.Symbole }));
            await db.SaveChangesAsync(ct);
        }
        if (!await db.Categories.AnyAsync(ct))
        {
            db.Categories.AddRange(Categories.Select((l, i) => new CategorieArticle { Libelle = l, Ordre = i + 1 }));
            await db.SaveChangesAsync(ct);
        }
        if (!await db.Articles.AnyAsync(ct))
        {
            var categorie = await db.Categories.FirstAsync(c => c.Libelle == "Post-tension", ct);
            var tonne = await db.Unites.FirstAsync(u => u.Libelle == "Tonne", ct);
            db.Articles.Add(new Article { Code = "TOR-T15.7", Designation = "Toron T15,7", CategorieId = categorie.Id, UniteId = tonne.Id, Origine = OriginesArticle.Achete, KgParMetre = 1.18m });
            await db.SaveChangesAsync(ct);
        }
    }
}
