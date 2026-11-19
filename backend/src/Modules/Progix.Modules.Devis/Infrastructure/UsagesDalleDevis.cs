using Microsoft.EntityFrameworkCore;
using Progix.SharedKernel;

namespace Progix.Modules.Devis.Infrastructure;

/// <summary>Une dalle chiffrée dans un devis (brouillon ou non) ne se supprime pas : le devis la référence.</summary>
public class UsagesDalleDevis(DevisDbContext db) : IUsagesDalle
{
    public async Task<string?> UsageAsync(Guid dalleId, CancellationToken ct = default)
    {
        var numero = await db.Lignes.AsNoTracking().Where(l => l.DalleId == dalleId)
            .Join(db.Devis.AsNoTracking(), l => l.DevisId, d => d.Id, (l, d) => d.Numero)
            .FirstOrDefaultAsync(ct);
        return numero is null ? null : $"elle figure dans le devis {numero}";
    }
}
