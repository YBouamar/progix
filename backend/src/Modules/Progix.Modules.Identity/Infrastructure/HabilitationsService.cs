using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Progix.SharedKernel;

namespace Progix.Modules.Identity.Infrastructure;

/// <summary>Fonction connue du contrôle des accès : sa fonction de référence et ses niveaux réglés.</summary>
public sealed record FonctionReglee(string Code, string Label, string Reference, bool Origine, IReadOnlyDictionary<string, int> Regles)
{
    /// <summary>Niveau effectif sur un module.</summary>
    public int Niveau(string module) => Habilitations.Niveau(Reference, module, Regles.TryGetValue(module, out var n) ? n : null);

    public Dictionary<string, int> Droits() => Habilitations.Modules.ToDictionary(m => m.Code, m => Niveau(m.Code));
}

/// <summary>Lecture des habilitations, gardées en mémoire et relues dès qu'un réglage change.</summary>
public class HabilitationsService(IdentityModuleDbContext db, IMemoryCache cache) : IHabilitations
{
    private const string Cle = "habilitations";

    public async Task<IReadOnlyDictionary<string, FonctionReglee>> FonctionsAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(Cle, out IReadOnlyDictionary<string, FonctionReglee>? connues) && connues is not null) return connues;
        var roles = await db.Roles.AsNoTracking().ToListAsync(ct);
        var regles = await db.Habilitations.AsNoTracking().ToListAsync(ct);
        var lues = roles.Where(r => r.Name is not null).ToDictionary(r => r.Name!, r => new FonctionReglee(
            r.Name!, r.Label, r.Base ?? r.Name!, r.Base is null,
            regles.Where(h => h.RoleId == r.Id).ToDictionary(h => h.Module, h => h.Niveau)));
        cache.Set(Cle, (IReadOnlyDictionary<string, FonctionReglee>)lues, TimeSpan.FromMinutes(10));
        return lues;
    }

    public void Oublier() => cache.Remove(Cle);

    /// <summary>Code de la fonction d'une personne : la revendication « fonction », sinon son rôle.</summary>
    public static string? Code(ClaimsPrincipal user) => user.FindFirst(Habilitations.ClaimFonction)?.Value ?? user.FindFirst(ClaimTypes.Role)?.Value;

    public async Task<int> NiveauAsync(ClaimsPrincipal user, string module, CancellationToken ct = default)
    {
        var code = Code(user);
        return code is not null && (await FonctionsAsync(ct)).TryGetValue(code, out var f) ? f.Niveau(module) : Habilitations.Aucun;
    }
}
