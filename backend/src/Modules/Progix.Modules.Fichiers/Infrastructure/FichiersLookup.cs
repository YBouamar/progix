using Microsoft.EntityFrameworkCore;
using Progix.SharedKernel;

namespace Progix.Modules.Fichiers.Infrastructure;

public class FichiersLookup(FichiersDbContext db) : IFichiersLookup
{
    public async Task<IReadOnlyList<string>> CategoriesAsync(string contexte, Guid cibleId, CancellationToken ct = default) =>
        await db.Fichiers.AsNoTracking().Where(f => f.Contexte == contexte && f.CibleId == cibleId && f.Complet && !f.Supprime && f.Categorie != null)
            .Select(f => f.Categorie!).Distinct().ToListAsync(ct);

    public Task<int> NombreAsync(string contexte, Guid cibleId, CancellationToken ct = default) =>
        db.Fichiers.CountAsync(f => f.Contexte == contexte && f.CibleId == cibleId && f.Complet && !f.Supprime, ct);
}
