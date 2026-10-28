using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Devis.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Devis.Infrastructure;

/// <summary>Plans de principe joints à un devis : déposés tant que le devis est en brouillon, par les rôles qui voient les devis.</summary>
public class FichiersDevis(DevisDbContext db) : IContexteFichiers
{
    public const string Nom = "plan_principe";
    public string Contexte => Nom;
    public IReadOnlyList<string> Categories => [];
    // L'administrateur consulte les plans de principe sans en déposer.
    public bool PeutDeposer(ClaimsPrincipal user) => Policies.RolesDevis.Any(user.IsInRole) && Endpoints.DevisEtatsEndpoints.PeutEcrire(user);

    public async Task<string?> RefusAsync(Guid cibleId, string? categorie, CancellationToken ct = default)
    {
        var d = await db.Devis.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cibleId, ct);
        if (d is null) return "Devis introuvable.";
        return d.Etat == EtatsDevis.Brouillon ? null : "Les plans de principe se déposent tant que le devis est en brouillon. Créez une nouvelle version pour les changer.";
    }
}
