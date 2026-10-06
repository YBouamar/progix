using Microsoft.EntityFrameworkCore;
using Progix.Modules.Devis.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Devis.Infrastructure;

public class DevisLookup(DevisDbContext db) : IDevisLookup
{
    public async Task<IReadOnlyList<DevisValide>> ValidesAsync(Guid projetId, CancellationToken ct = default)
    {
        var rows = await db.Devis.AsNoTracking().Include(d => d.Lignes)
            .Where(d => d.ProjetId == projetId && d.Etat == EtatsDevis.Valide).OrderByDescending(d => d.ValideLe).ToListAsync(ct);
        // La remise finale au m² s'applique à toute la surface : le prix net d'une dalle est son prix moins la remise.
        return rows.Select(d => new DevisValide(d.Id, d.Numero, d.Version, d.ValideLe ?? d.CreatedAt, d.NetHT,
            d.Lignes.ToDictionary(l => l.DalleId, l => Math.Max(0, l.PrixM2 - d.RemiseM2)))).ToList();
    }

    public async Task<IReadOnlyList<Guid>> ProjetsDuCommercialAsync(Guid userId, CancellationToken ct = default) =>
        await db.Devis.AsNoTracking().Where(d => d.CommercialId == userId && d.Etat != Domain.EtatsDevis.Annule).Select(d => d.ProjetId).Distinct().ToListAsync(ct);
}
