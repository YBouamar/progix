using Hangfire;
using Hangfire.PostgreSql;
using Progix.Modules.Identity;
using Progix.Modules.Partenaires;
using Progix.Modules.Projets;
using Progix.Modules.Devis;
using Progix.Modules.Fichiers;
using Progix.Modules.Facturation;
using Progix.SharedKernel;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var postgres = config.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres est obligatoire.");

// Monolithe modulaire : chaque module métier s'enregistre ici.
IModule[] modules = [new IdentityModule(), new PartenairesModule(), new ProjetsModule(), new DevisModule(), new FichiersModule(), new FacturationModule()];
foreach (var m in modules) m.AddServices(builder.Services, config);

builder.Services.AddStackExchangeRedisCache(o =>
{
    o.Configuration = config.GetConnectionString("Redis");
    o.InstanceName = "progix:";
});

builder.Services.AddHangfire(h => h
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(postgres), new PostgreSqlStorageOptions { SchemaName = "hangfire" }));
builder.Services.AddHangfireServer();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "PROGIX API", Version = "v1" });
    // Plusieurs modules ont des types de même nom : le nom complet les distingue.
    o.CustomSchemaIds(t => t.FullName!.Replace("Progix.Modules.", "").Replace("+", "."));
});
builder.Services.AddProblemDetails();

var app = builder.Build();

// Migrations et données de référence, avec reprise tant que PostgreSQL démarre.
for (var essai = 1; ; essai++)
{
    try
    {
        foreach (var m in modules) await m.InitializeAsync(app.Services, CancellationToken.None);
        break;
    }
    catch (Exception ex) when (essai < 15)
    {
        app.Logger.LogWarning("Base de données indisponible (essai {Essai}) : {Message}", essai, ex.Message);
        await Task.Delay(TimeSpan.FromSeconds(3));
    }
}

app.UseExceptionHandler();
// La documentation de l'API n'est publiée qu'en développement.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o => o.DocumentTitle = "PROGIX API");
}
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", modules = modules.Select(m => m.Name) })).WithTags("Système");
foreach (var m in modules) m.MapEndpoints(app);

// Tâche quotidienne : efface du stockage les fichiers supprimés depuis plus d'un mois.
app.Services.GetRequiredService<IRecurringJobManager>()
    .AddOrUpdate<Progix.Modules.Fichiers.Infrastructure.PurgeFichiers>("purge-fichiers", j => j.ExecuterAsync(), Cron.Daily(3));

if (app.Environment.IsDevelopment())
    app.MapHangfireDashboard("/hangfire", new DashboardOptions { Authorization = [] });

app.Run();
