using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Identity.Domain;
using Progix.Modules.Identity.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Identity.Endpoints;

/// <summary>
/// Écran des habilitations : niveau d'accès de chaque fonction par module, et création de fonctions.
/// Réservé au directeur et au chef de projet ; les droits du directeur ne se règlent pas.
/// </summary>
public static class HabilitationsEndpoints
{
    public record FonctionInput(string? Label, string? Reference, Dictionary<string, int>? Droits);

    private static IResult Conflit(string message) => Results.Conflict(new { message });

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/habilitations").WithTags("Habilitations").RequireAuthorization(Policies.GestionUtilisateurs);

        g.MapGet("/", async (IdentityModuleDbContext db, HabilitationsService service, ClaimsPrincipal user) => Results.Ok(await VueAsync(db, service, user)));

        // Nouvelle fonction : elle prend les validations et les signatures de sa fonction de référence.
        g.MapPost("/", async (FonctionInput input, IdentityModuleDbContext db, RoleManager<AppRole> roles, HabilitationsService service, ClaimsPrincipal user) =>
        {
            var label = (input.Label ?? "").Trim();
            if (label.Length is < 2 or > 100) return Results.ValidationProblem(new Dictionary<string, string[]> { ["label"] = ["Le nom de la fonction compte de 2 à 100 caractères."] });
            if (Roles.Find(input.Reference ?? "") is not { } reference)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["reference"] = ["Choisissez la fonction de référence."] });
            if (reference.Code == Roles.Directeur) return Conflit("Une fonction ne peut pas prendre le directeur pour référence : ses droits ne se règlent pas.");
            if (await db.Roles.AnyAsync(r => r.Label.ToLower() == label.ToLower())) return Conflit("Une fonction porte déjà ce nom.");
            var role = new AppRole { Name = $"fonction_{Guid.NewGuid():N}"[..24], Label = label, VoitMontants = reference.VoitMontants, Base = reference.Code };
            var cree = await roles.CreateAsync(role);
            if (!cree.Succeeded) return Conflit(string.Join(" ", cree.Errors.Select(e => e.Description)));
            Regler(db, role, input.Droits);
            await db.SaveChangesAsync();
            service.Oublier();
            return Results.Ok(await VueAsync(db, service, user));
        });

        // Niveaux d'une fonction, et nom d'une fonction créée.
        g.MapPut("/{code}", async (string code, FonctionInput input, IdentityModuleDbContext db, HabilitationsService service, ClaimsPrincipal user) =>
        {
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == code);
            if (role is null) return Results.NotFound();
            if ((role.Base ?? role.Name) == Roles.Directeur) return Conflit("Les droits du directeur ne se règlent pas : il garde l'accès à tout.");
            if (role.Base is not null && input.Label is not null)
            {
                var label = input.Label.Trim();
                if (label.Length is < 2 or > 100) return Results.ValidationProblem(new Dictionary<string, string[]> { ["label"] = ["Le nom de la fonction compte de 2 à 100 caractères."] });
                if (await db.Roles.AnyAsync(r => r.Id != role.Id && r.Label.ToLower() == label.ToLower())) return Conflit("Une fonction porte déjà ce nom.");
                role.Label = label;
            }
            db.Habilitations.RemoveRange(await db.Habilitations.Where(h => h.RoleId == role.Id).ToListAsync());
            Regler(db, role, input.Droits);
            await db.SaveChangesAsync();
            service.Oublier();
            return Results.Ok(await VueAsync(db, service, user));
        });

        // Une fonction créée se supprime tant que personne ne la porte. Les douze fonctions d'origine restent.
        g.MapDelete("/{code}", async (string code, IdentityModuleDbContext db, RoleManager<AppRole> roles, HabilitationsService service, ClaimsPrincipal user) =>
        {
            var role = await roles.FindByNameAsync(code);
            if (role is null) return Results.NotFound();
            if (role.Base is null) return Conflit("Les fonctions d'origine ne se suppriment pas.");
            if (await db.UserRoles.AnyAsync(u => u.RoleId == role.Id)) return Conflit("Des utilisateurs portent cette fonction : donnez-leur une autre fonction avant de la supprimer.");
            db.Habilitations.RemoveRange(await db.Habilitations.Where(h => h.RoleId == role.Id).ToListAsync());
            await db.SaveChangesAsync();
            await roles.DeleteAsync(role);
            service.Oublier();
            return Results.Ok(await VueAsync(db, service, user));
        });
    }

    /// <summary>Enregistre les niveaux qui s'écartent de ceux de la fonction de référence, ramenés à son plafond.</summary>
    private static void Regler(IdentityModuleDbContext db, AppRole role, Dictionary<string, int>? droits)
    {
        var reference = role.Base ?? role.Name!;
        foreach (var m in Habilitations.Modules)
        {
            if (droits is null || !droits.TryGetValue(m.Code, out var demande)) continue;
            var niveau = Math.Clamp(demande, Habilitations.Aucun, Habilitations.Plafond(reference, m.Code));
            if (niveau != Habilitations.ParDefaut(reference, m.Code)) db.Habilitations.Add(new Habilitation { RoleId = role.Id, Module = m.Code, Niveau = niveau });
        }
    }

    private static async Task<object> VueAsync(IdentityModuleDbContext db, HabilitationsService service, ClaimsPrincipal user)
    {
        service.Oublier();
        var fonctions = await service.FonctionsAsync();
        var roles = await db.Roles.AsNoTracking().ToListAsync();
        var porteurs = await db.UserRoles.GroupBy(u => u.RoleId).Select(x => new { x.Key, Nb = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Nb);
        var ordre = Roles.All.Select((r, i) => (r.Code, i)).ToDictionary(x => x.Code, x => x.i);
        return new
        {
            modules = Habilitations.Modules.Select(m => new { code = m.Code, label = m.Label, aide = m.Aide, maximum = m.Maximum, accordable = m.Accordable }),
            references = Roles.All.Where(r => r.Code != Roles.Directeur).Select(r => new { code = r.Code, label = r.Label }),
            fonctions = roles.Where(r => r.Name is not null && fonctions.ContainsKey(r.Name)).Select(r => (Role: r, F: fonctions[r.Name!]))
                .OrderBy(x => x.F.Origine ? 0 : 1).ThenBy(x => x.F.Origine ? ordre.GetValueOrDefault(x.F.Code, 99) : 0).ThenBy(x => x.F.Label)
                .Select(x => new
                {
                    code = x.F.Code, label = x.F.Label, origine = x.F.Origine, reference = x.F.Reference, referenceLabel = Roles.Find(x.F.Reference)?.Label ?? x.F.Reference,
                    utilisateurs = porteurs.GetValueOrDefault(x.Role.Id), verrouillee = x.F.Reference == Roles.Directeur,
                    droits = x.F.Droits(),
                    parDefaut = Habilitations.Modules.ToDictionary(m => m.Code, m => Habilitations.ParDefaut(x.F.Reference, m.Code)),
                    plafonds = Habilitations.Modules.ToDictionary(m => m.Code, m => x.F.Reference == Roles.Directeur ? m.Maximum : Habilitations.Plafond(x.F.Reference, m.Code)),
                }),
        };
    }
}
