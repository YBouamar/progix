using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Progix.Modules.Projets.Endpoints;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets;

public class ProjetsModule : IModule
{
    public string Name => "Projets";

    public void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddScoped<IContexteFichiers, FichiersProjet>().AddScoped<IContexteFichiers, FichiersPlans>().AddScoped<IContexteFichiers, FichiersPhotos>().AddScoped<IContexteFichiers, FichiersRapport>().AddScoped<IProjetsStatut, ProjetsLookup>().AddScoped<IProjetsLookup, ProjetsLookup>().AddScoped<IFichesChantier, FichesChantier>().AddDbContext<ProjetsDbContext>(o => o.UseNpgsql(
            configuration.GetConnectionString("Postgres"),
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations", ProjetsDbContext.Schema)));

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        ProjetsEndpoints.Map(app);
        ProjetPartenairesEndpoints.Map(app);
        RemarquesEndpoints.Map(app);
        DallesEndpoints.Map(app);
        EquipeEndpoints.Map(app);
        SuiviEndpoints.Map(app);
        VersionsEndpoints.Map(app);
        TensionEndpoints.Map(app);
        InjectionEndpoints.Map(app);
        CommandeEndpoints.Map(app);
        LogistiqueEndpoints.Map(app);
        TableauDeBordEndpoints.Map(app);
        RepartitionEndpoints.Map(app);
        AutoControleEndpoints.Map(app);
        PerformanceEndpoints.Map(app);
        RapportsEcrasementEndpoints.Map(app);
    }

    public async Task InitializeAsync(IServiceProvider services, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjetsDbContext>();
        await db.Database.MigrateAsync(ct);
        // Commandes usine d'avant le modèle « un tendon par ligne » : converties une fois, avec leurs fiches de tension et d'injection.
        await Infrastructure.RepriseTendons.ExecuterAsync(db, ct);
        // Accessoires de commande proposés au premier démarrage, modifiables ensuite dans le Paramétrage.
        if (!await db.Accessoires.AnyAsync(ct))
        {
            db.Accessoires.AddRange(Domain.Accessoire.ParDefaut.Select((a, i) => new Domain.Accessoire { Libelle = a.Libelle, Unite = a.Unite, Ordre = i }));
            await db.SaveChangesAsync(ct);
        }
        else
        {
            // Les accessoires créés avant l'ajout de l'unité reçoivent leur unité par défaut.
            var sansUnite = await db.Accessoires.Where(a => a.Unite == null).ToListAsync(ct);
            foreach (var a in sansUnite)
                if (Domain.Accessoire.ParDefaut.FirstOrDefault(d => d.Libelle == a.Libelle) is { Unite: not null } d) a.Unite = d.Unite;
            await db.SaveChangesAsync(ct);
        }
    }
}
