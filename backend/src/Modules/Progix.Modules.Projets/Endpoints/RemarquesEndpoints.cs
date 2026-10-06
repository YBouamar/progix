using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

public record RemarqueInput(string? Texte);

public static class RemarquesEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/projets/{id:guid}/remarques").WithTags("Projets").RequireAuthorization();

        g.MapGet("/", async (Guid id, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var moi = user.UserId();
            var rows = await db.Remarques.AsNoTracking().Include(r => r.Traces)
                .Where(r => r.ProjetId == id).OrderByDescending(r => r.CreatedAt).ToListAsync();
            return Results.Ok(rows.Select(r => Dto(r, moi)));
        });

        g.MapPost("/", async (Guid id, RemarqueInput input, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            if (string.IsNullOrWhiteSpace(input.Texte)) return TexteRequis();
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (projet is null) return Results.NotFound();
            if (projet.Supprime) return ProjetsEndpoints.Conflit("Un projet supprimé est en lecture seule.");
            var r = new Remarque
            {
                ProjetId = id, AuteurId = user.UserId(), AuteurNom = user.UserName(), AuteurRole = user.RoleLabel(), Texte = input.Texte.Trim(),
            };
            db.Remarques.Add(r);
            await db.SaveChangesAsync();
            return Results.Ok(Dto(r, r.AuteurId));
        });

        g.MapPut("/{rid:guid}", async (Guid id, Guid rid, RemarqueInput input, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            if (string.IsNullOrWhiteSpace(input.Texte)) return TexteRequis();
            var r = await db.Remarques.Include(x => x.Traces).FirstOrDefaultAsync(x => x.Id == rid && x.ProjetId == id);
            if (r is null || r.Supprime) return Results.NotFound();
            if (r.AuteurId != user.UserId()) return Results.Forbid();
            var texte = input.Texte.Trim();
            if (texte != r.Texte)
            {
                db.RemarqueTraces.Add(new RemarqueTrace { RemarqueId = r.Id, Action = "modification", AncienTexte = r.Texte, Par = user.UserName() });
                r.Texte = texte;
                r.ModifiePar = user.UserName();
                r.ModifieLe = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            return Results.Ok(Dto(r, r.AuteurId));
        });

        g.MapDelete("/{rid:guid}", async (Guid id, Guid rid, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var r = await db.Remarques.Include(x => x.Traces).FirstOrDefaultAsync(x => x.Id == rid && x.ProjetId == id);
            if (r is null || r.Supprime) return Results.NotFound();
            if (r.AuteurId != user.UserId()) return Results.Forbid();
            db.RemarqueTraces.Add(new RemarqueTrace { RemarqueId = r.Id, Action = "suppression", AncienTexte = r.Texte, Par = user.UserName() });
            r.Supprime = true;
            r.SupprimePar = user.UserName();
            r.SupprimeLe = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(Dto(r, r.AuteurId));
        });
    }

    private static IResult TexteRequis() =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["texte"] = ["Le texte est obligatoire."] });

    private static object Dto(Remarque r, Guid moi) => new
    {
        id = r.Id, auteur = r.AuteurNom, role = r.AuteurRole, date = r.CreatedAt,
        texte = r.Supprime ? null : r.Texte,
        supprime = r.Supprime, supprimePar = r.SupprimePar, supprimeLe = r.SupprimeLe,
        modifiePar = r.ModifiePar, modifieLe = r.ModifieLe,
        mienne = r.AuteurId == moi,
        historique = r.Traces.OrderByDescending(t => t.CreatedAt)
            .Select(t => new { action = t.Action, ancienTexte = t.AncienTexte, par = t.Par, date = t.CreatedAt }),
    };
}
