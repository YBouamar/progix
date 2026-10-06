using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Facturation.Domain;
using Progix.Modules.Facturation.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Endpoints;

public record EntiteInput(string? RaisonSociale, string? FormeJuridique, decimal? Capital, string? Siege, string? Ville, string? Telephone, string? Email,
    string? Ice, string? IdentifiantFiscal, string? Rc, string? TribunalRc, string? Patente, string? Cnss, string? Banque, string? Rib);

public static class EntitesEndpoints
{
    private const int TailleMaxLogo = 1024 * 1024;
    private static readonly string[] TypesLogo = ["image/png", "image/jpeg"];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/parametres/entites").WithTags("Paramétrage").RequireAuthorization();

        g.MapGet("/", async (FacturationDbContext db, ClaimsPrincipal user) =>
            Results.Ok(new { modifiable = Peut(user), entites = (await db.Entites.AsNoTracking().OrderBy(e => e.Code).ToListAsync()).Select(Dto) }));

        g.MapPut("/{code}", async (string code, EntiteInput input, FacturationDbContext db, ClaimsPrincipal user) =>
        {
            if (!Peut(user)) return Results.Forbid();
            var e = await db.Entites.FirstOrDefaultAsync(x => x.Code == code);
            if (e is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(input.RaisonSociale)) return Conflit("La raison sociale est obligatoire.");
            var ice = Net(input.Ice);
            if (ice is not null && (ice.Length != 15 || !ice.All(char.IsDigit))) return Conflit("L'ICE compte exactement 15 chiffres.");
            var rib = Net(input.Rib)?.Replace(" ", "");
            if (rib is not null && (rib.Length != 24 || !rib.All(char.IsDigit))) return Conflit("Le RIB compte exactement 24 chiffres.");
            if (input.Capital is < 0) return Conflit("Le capital ne peut pas être négatif.");
            e.RaisonSociale = input.RaisonSociale.Trim(); e.FormeJuridique = Net(input.FormeJuridique); e.Capital = input.Capital; e.Siege = Net(input.Siege); e.Ville = Net(input.Ville);
            e.Telephone = Net(input.Telephone); e.Email = Net(input.Email); e.Ice = ice; e.IdentifiantFiscal = Net(input.IdentifiantFiscal); e.Rc = Net(input.Rc);
            e.TribunalRc = Net(input.TribunalRc); e.Patente = Net(input.Patente); e.Cnss = Net(input.Cnss); e.Banque = Net(input.Banque); e.Rib = rib;
            e.ModifiePar = user.UserName(); e.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(Dto(e));
        });

        g.MapGet("/{code}/logo", async (string code, FacturationDbContext db) =>
        {
            var e = await db.Entites.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code);
            return e?.Logo is null ? Results.NotFound() : Results.File(e.Logo, e.LogoType ?? "image/png");
        });

        g.MapPost("/{code}/logo", async (string code, IFormFile fichier, FacturationDbContext db, ClaimsPrincipal user) =>
        {
            if (!Peut(user)) return Results.Forbid();
            var e = await db.Entites.FirstOrDefaultAsync(x => x.Code == code);
            if (e is null) return Results.NotFound();
            if (!TypesLogo.Contains(fichier.ContentType)) return Conflit("Le logo est une image PNG ou JPEG.");
            if (fichier.Length is 0 or > TailleMaxLogo) return Conflit("Le logo pèse au plus 1 Mo.");
            using var ms = new MemoryStream();
            await fichier.CopyToAsync(ms);
            e.Logo = ms.ToArray(); e.LogoType = fichier.ContentType; e.ModifiePar = user.UserName(); e.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(Dto(e));
        }).DisableAntiforgery();

        g.MapDelete("/{code}/logo", async (string code, FacturationDbContext db, ClaimsPrincipal user) =>
        {
            if (!Peut(user)) return Results.Forbid();
            var e = await db.Entites.FirstOrDefaultAsync(x => x.Code == code);
            if (e is null) return Results.NotFound();
            e.Logo = null; e.LogoType = null; e.ModifiePar = user.UserName(); e.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(Dto(e));
        });
    }

    /// <summary>Identité des entités : responsable administratif, chef de projet, directeur.</summary>
    private static bool Peut(ClaimsPrincipal u) => u.IsInRole(Roles.ResponsableAdministratif) || u.IsInRole(Roles.ChefProjet) || u.IsInRole(Roles.Directeur);
    private static string? Net(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static IResult Conflit(string message) => Results.Conflict(new { message });

    private static object Dto(ParametresEntite e) => new
    {
        code = e.Code, raisonSociale = e.RaisonSociale, formeJuridique = e.FormeJuridique, capital = e.Capital, siege = e.Siege, ville = e.Ville, telephone = e.Telephone, email = e.Email,
        ice = e.Ice, identifiantFiscal = e.IdentifiantFiscal, rc = e.Rc, tribunalRc = e.TribunalRc, patente = e.Patente, cnss = e.Cnss, banque = e.Banque, rib = e.Rib,
        logo = e.Logo is not null, modifiePar = e.ModifiePar, modifieLe = e.UpdatedAt,
    };
}
