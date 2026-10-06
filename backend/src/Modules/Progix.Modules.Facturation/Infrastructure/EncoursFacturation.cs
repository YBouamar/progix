using Microsoft.EntityFrameworkCore;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Infrastructure;

/// <summary>Reste dû sur les factures d'un projet, effets non échus compris.</summary>
public class EncoursFacturation(FacturationDbContext db) : IEncoursProjet
{
    public async Task<EncoursProjet> EncoursAsync(Guid projetId, CancellationToken ct = default)
    {
        var factures = await db.Factures.AsNoTracking().Include(f => f.Encaissements).Include(f => f.Avoirs)
            .Where(f => f.ProjetId == projetId).ToListAsync(ct);
        var dues = factures.Where(f => !f.Annulee && f.Reste > 0).ToList();
        var jour = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(1));
        var echues = dues.Where(f => f.Echeance < jour).ToList();
        return new EncoursProjet(dues.Count, echues.Count, dues.Sum(f => f.Reste), echues.Sum(f => f.Reste));
    }
}
