using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Facturation.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Infrastructure;

/// <summary>Exemplaire du contrat signé et cacheté par le client (PDF ou image), déposé une fois le contrat validé par le client.</summary>
public class FichiersContratSigne(FacturationDbContext db) : IContexteFichiers
{
    public const string Nom = "contrat_signe";
    public string Contexte => Nom;
    public IReadOnlyList<string> Categories => [];
    public bool PeutDeposer(ClaimsPrincipal user) => Policies.RolesCommercial.Any(user.IsInRole);

    public async Task<string?> RefusAsync(Guid cibleId, string? categorie, CancellationToken ct = default)
    {
        var c = await db.Contrats.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cibleId, ct);
        if (c is null) return "Contrat introuvable.";
        if (c.Annule) return "Ce contrat est annulé.";
        return c.Statut == StatutsContrat.ValideClient ? null : "Le contrat signé par le client se dépose une fois le contrat marqué « validé par le client ».";
    }
}
