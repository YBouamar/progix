using Microsoft.EntityFrameworkCore;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Infrastructure;

/// <summary>Une dalle livrée, facturée, payée ou citée dans un document émis ne se supprime pas.</summary>
public class UsagesDalleFacturation(FacturationDbContext db) : IUsagesDalle
{
    public async Task<string?> UsageAsync(Guid dalleId, CancellationToken ct = default)
    {
        if (await db.LignesLivraison.AnyAsync(l => l.DalleId == dalleId, ct)) return "elle figure sur un bon de livraison";
        if (await db.LignesFacture.AnyAsync(l => l.DalleId == dalleId, ct)) return "elle figure sur une facture";
        if (await db.Encaissements.AnyAsync(e => e.Dalles.Any(x => x.DalleId == dalleId), ct)) return "un règlement lui est affecté";
        if (await db.Depenses.AnyAsync(d => d.DalleId == dalleId, ct)) return "une dépense lui est rattachée";
        if (await db.Courriers.AnyAsync(c => c.Lignes.Any(l => l.DalleId == dalleId), ct)) return "elle est citée dans un courrier";
        if (await db.SignaturesFiches.AnyAsync(s => s.DalleId == dalleId, ct)) return "une fiche de chantier signée la concerne";
        return null;
    }
}
