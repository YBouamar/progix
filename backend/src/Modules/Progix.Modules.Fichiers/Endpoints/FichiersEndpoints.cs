using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Fichiers.Domain;
using Progix.Modules.Fichiers.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Fichiers.Endpoints;

public record EnvoiInput(string Contexte, Guid CibleId, string Nom, long Taille, string? ContentType, string? Categorie);
public record MorceauInput(int Numero, string ETag);
public record FinEnvoiInput(List<MorceauInput> Morceaux);

public static class FichiersEndpoints
{
    public const long TailleMax = 1024L * 1024 * 1024;

    /// <summary>Taille des morceaux envoyés par le navigateur. Le stockage S3 exige au moins 5 Mo, sauf pour le dernier.</summary>
    public const int TailleMorceau = 8 * 1024 * 1024;

    private static readonly string[] Photos = ["image/jpeg", "image/png", "image/webp", "image/heic", "image/heif"];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/fichiers").WithTags("Fichiers").RequireAuthorization();

        g.MapGet("/", async (string contexte, Guid cibleId, string? categorie, FichiersDbContext db, IEnumerable<IContexteFichiers> contextes, ClaimsPrincipal user) =>
        {
            var ctx = contextes.FirstOrDefault(c => c.Contexte == contexte);
            if (ctx is null) return Results.NotFound();
            var ouvert = ctx.PeutDeposer(user) && await ctx.RefusAsync(cibleId, categorie) is null;
            var rows = await db.Fichiers.AsNoTracking().Where(f => f.Contexte == contexte && f.CibleId == cibleId && f.Complet && !f.Supprime
                    && (categorie == null || f.Categorie == categorie))
                .OrderByDescending(f => f.CreatedAt).ToListAsync();
            return Results.Ok(new
            {
                peutDeposer = ouvert, imagesSeulement = ctx.ImagesSeulement, tailleMax = TailleMax, tailleMorceau = TailleMorceau,
                fichiers = rows.Select(f => Dto(f, user, ouvert)),
            });
        });

        // Un fichier s'envoie en trois temps : ouverture, morceaux de 8 Mo, clôture. Rien de volumineux ne transite en une seule requête.
        g.MapPost("/envois", async (EnvoiInput input, FichiersDbContext db, Stockage stockage, IEnumerable<IContexteFichiers> contextes, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var ctx = contextes.FirstOrDefault(c => c.Contexte == input.Contexte);
            if (ctx is null) return Results.NotFound();
            if (!ctx.PeutDeposer(user)) return Results.Forbid();
            var categorie = string.IsNullOrWhiteSpace(input.Categorie) ? null : input.Categorie;
            if (await ctx.RefusAsync(input.CibleId, categorie, ct) is { } refus) return Conflit(refus);
            if (ctx.ImagesSeulement && !Photos.Contains(input.ContentType ?? "")) return Conflit("Seules les photos sont acceptées ici (JPEG, PNG, WebP, HEIC).");
            if (ctx.Categories.Count > 0 ? categorie is null || !ctx.Categories.Contains(categorie) : categorie is not null) return Conflit("Catégorie de fichier inconnue.");
            var nom = Path.GetFileName(input.Nom ?? "").Trim();
            if (nom.Length == 0) return Conflit("Le nom du fichier est obligatoire.");
            if (input.Taille <= 0) return Conflit("Un fichier vide ne peut pas être déposé.");
            if (input.Taille > TailleMax) return Conflit("Le fichier dépasse la taille maximale de 1 Go.");

            var f = new Fichier
            {
                Contexte = input.Contexte, CibleId = input.CibleId, Categorie = categorie, Nom = nom.Length > 255 ? nom[^255..] : nom, Taille = input.Taille,
                ContentType = string.IsNullOrWhiteSpace(input.ContentType) ? "application/octet-stream" : input.ContentType,
                AjouteParId = user.UserId(), AjoutePar = user.UserName(),
            };
            f.Cle = $"{f.Contexte}/{f.CibleId:N}/{f.Id:N}";
            f.EnvoiId = await stockage.CommencerEnvoiAsync(f.Cle, f.ContentType, ct);
            db.Fichiers.Add(f);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { id = f.Id });
        });

        g.MapPut("/envois/{id:guid}/morceaux/{numero:int}", async (Guid id, int numero, HttpRequest request, FichiersDbContext db, Stockage stockage,
            ClaimsPrincipal user, CancellationToken ct) =>
        {
            var f = await db.Fichiers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.Complet, ct);
            if (f is null || f.EnvoiId is null) return Results.NotFound();
            if (f.AjouteParId != user.UserId()) return Results.Forbid();
            if (numero < 1 || numero > 10000 || request.ContentLength is null or <= 0 or > TailleMorceau) return Conflit("Morceau invalide.");
            using var tampon = new MemoryStream((int)request.ContentLength.Value);
            await request.Body.CopyToAsync(tampon, ct);
            tampon.Position = 0;
            return Results.Ok(new { numero, eTag = await stockage.EnvoyerMorceauAsync(f.Cle, f.EnvoiId, numero, tampon, ct) });
        });

        g.MapPost("/envois/{id:guid}/terminer", async (Guid id, FinEnvoiInput input, FichiersDbContext db, Stockage stockage, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var f = await db.Fichiers.FirstOrDefaultAsync(x => x.Id == id && !x.Complet, ct);
            if (f is null || f.EnvoiId is null) return Results.NotFound();
            if (f.AjouteParId != user.UserId()) return Results.Forbid();
            if (input.Morceaux is null || input.Morceaux.Count == 0) return Conflit("Aucun morceau reçu.");
            await stockage.TerminerEnvoiAsync(f.Cle, f.EnvoiId, input.Morceaux.Select(m => (m.Numero, m.ETag)), ct);
            f.Complet = true;
            f.EnvoiId = null;
            await db.SaveChangesAsync(ct);
            return Results.Ok(Dto(f, user, true));
        });

        g.MapGet("/{id:guid}/contenu", async (Guid id, FichiersDbContext db, Stockage stockage, CancellationToken ct) =>
        {
            var f = await db.Fichiers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Complet && !x.Supprime, ct);
            if (f is null) return Results.NotFound();
            return Results.Stream(await stockage.LireAsync(f.Cle, ct), f.ContentType, f.Nom);
        });

        // Suppression : le fichier quitte la liste, son contenu est effacé du stockage un mois plus tard.
        g.MapDelete("/{id:guid}", async (Guid id, FichiersDbContext db, IEnumerable<IContexteFichiers> contextes, ClaimsPrincipal user) =>
        {
            var f = await db.Fichiers.FirstOrDefaultAsync(x => x.Id == id && x.Complet && !x.Supprime);
            if (f is null) return Results.NotFound();
            if (!PeutSupprimer(f, user)) return Results.Forbid();
            if (contextes.FirstOrDefault(c => c.Contexte == f.Contexte) is { } ctx && await ctx.RefusAsync(f.CibleId, f.Categorie) is { } refus) return Conflit(refus);
            f.Supprime = true;
            f.SupprimePar = user.UserName();
            f.SupprimeLe = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static IResult Conflit(string message) => Results.Conflict(new { message });

    private static bool PeutSupprimer(Fichier f, ClaimsPrincipal user) =>
        f.AjouteParId == user.UserId() || user.IsInRole(Roles.Directeur) || user.IsInRole(Roles.ChefProjet);

    private static object Dto(Fichier f, ClaimsPrincipal user, bool ouvert) => new
    {
        id = f.Id, nom = f.Nom, taille = f.Taille, contentType = f.ContentType, ajoutePar = f.AjoutePar, date = f.CreatedAt,
        categorie = f.Categorie, peutSupprimer = ouvert && PeutSupprimer(f, user),
    };
}
