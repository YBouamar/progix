using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

public record DalleInput(string? Bloc, string? Immeuble, int Niveau, string? Detail, decimal Surface);

public static class DallesEndpoints
{
    public const int NiveauMin = -5;
    public const int NiveauMax = 40;

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/projets/{id:guid}/dalles").WithTags("Dalles").RequireAuthorization();

        g.MapGet("/", async (Guid id, ProjetsDbContext db) =>
        {
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (projet is null) return Results.NotFound();
            var dalles = await db.Dalles.AsNoTracking().Where(d => d.ProjetId == id).OrderBy(d => d.Numero).ToListAsync();
            return Results.Ok(new
            {
                niveaux = Enumerable.Range(NiveauMin, NiveauMax - NiveauMin + 1).Select(n => new { valeur = n, label = Dalle.NiveauLabel(n) }),
                dalles = dalles.Select(d => Dto(d, projet.Code)),
            });
        });

        g.MapPost("/", async (Guid id, DalleInput input, ProjetsDbContext db) =>
        {
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (projet is null) return Results.NotFound();
            if (projet.Supprime) return ProjetsEndpoints.Conflit("Un projet supprimé est en lecture seule.");
            var erreurs = Valider(input);
            if (erreurs.Count > 0) return Results.ValidationProblem(erreurs);

            await using var tx = await db.Database.BeginTransactionAsync();
            var d = new Dalle { ProjetId = id, Numero = await db.NumeroSuivantAsync($"dalle:{id:N}") };
            Appliquer(d, input);
            db.Dalles.Add(d);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(Dto(d, projet.Code));
        }).RequireAuthorization(Policies.Commercial);

        g.MapPut("/{did:guid}", async (Guid id, Guid did, DalleInput input, ProjetsDbContext db) =>
        {
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            var d = await db.Dalles.FirstOrDefaultAsync(x => x.Id == did && x.ProjetId == id);
            if (projet is null || d is null) return Results.NotFound();
            if (projet.Supprime) return ProjetsEndpoints.Conflit("Un projet supprimé est en lecture seule.");
            var erreurs = Valider(input);
            if (erreurs.Count > 0) return Results.ValidationProblem(erreurs);
            Appliquer(d, input);
            d.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(Dto(d, projet.Code));
        }).RequireAuthorization(Policies.Commercial);
    }

    private static Dictionary<string, string[]> Valider(DalleInput i)
    {
        var e = new Dictionary<string, string[]>();
        if (i.Surface <= 0) e["surface"] = ["La surface doit être supérieure à 0."];
        if (i.Niveau < NiveauMin || i.Niveau > NiveauMax) e["niveau"] = ["Niveau inconnu."];
        return e;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static void Appliquer(Dalle d, DalleInput i)
    {
        d.Bloc = Clean(i.Bloc);
        d.Immeuble = Clean(i.Immeuble);
        d.Niveau = i.Niveau;
        d.Detail = Clean(i.Detail);
        d.Surface = Math.Round(i.Surface, 2);
    }

    internal static object Dto(Dalle d, string codeProjet) => new
    {
        id = d.Id, reference = $"{codeProjet}-{d.Suffixe}", numero = d.Numero, version = d.Version.ToString(),
        produit = Dalle.Produit, bloc = d.Bloc, immeuble = d.Immeuble,
        niveau = d.Niveau, niveauLabel = Dalle.NiveauLabel(d.Niveau), detail = d.Detail, surface = d.Surface,
        dessinateurId = d.DessinateurId, dessinateur = d.DessinateurNom,
        etapesFaites = d.EtapesFaites,
        etape = d.EtapesFaites >= Etapes.Total ? "Réalisée" : d.EtapesFaites == 0 ? "Non commencée" : $"{d.EtapesFaites + 1} · {Etapes.Get(d.EtapesFaites + 1).Label}",
    };
}
