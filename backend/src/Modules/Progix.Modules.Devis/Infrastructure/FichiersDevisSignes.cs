using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Devis.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Devis.Infrastructure;

/// <summary>
/// Devis signé et cacheté par le client (PDF ou image), déposé une fois le devis validé.
/// Dépôt par les fonctions qui valident un devis : responsable administratif, chef de projet, directeur.
/// </summary>
public class FichiersDevisSignes(DevisDbContext db) : IContexteFichiers
{
    public string Contexte => IDevisDocuments.ContexteSigne;
    public IReadOnlyList<string> Categories => [];
    public bool PeutDeposer(ClaimsPrincipal user) => Endpoints.DevisEtatsEndpoints.PeutSigner(user);

    public async Task<string?> RefusAsync(Guid cibleId, string? categorie, CancellationToken ct = default)
    {
        var d = await db.Devis.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cibleId, ct);
        if (d is null) return "Devis introuvable.";
        return d.Etat == EtatsDevis.Valide ? null : "Le devis signé par le client se dépose une fois le devis validé.";
    }
}
