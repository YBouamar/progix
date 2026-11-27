using Microsoft.EntityFrameworkCore;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Infrastructure;

/// <summary>Un projet qui a un contrat, un bon de livraison, une facture ou un paiement est engagé.</summary>
public class UsagesProjetFacturation(FacturationDbContext db) : IUsagesProjet
{
    public async Task<string?> UsageAsync(Guid projetId, CancellationToken ct = default)
    {
        if (await db.Contrats.AnyAsync(c => c.ProjetId == projetId, ct)) return "il a un contrat";
        if (await db.BonsLivraison.AnyAsync(b => b.ProjetId == projetId, ct)) return "il a un bon de livraison";
        if (await db.Factures.AnyAsync(f => f.ProjetId == projetId, ct)) return "il a une facture";
        if (await db.Encaissements.AnyAsync(e => e.ProjetId == projetId, ct)) return "il a un paiement";
        return null;
    }
}
