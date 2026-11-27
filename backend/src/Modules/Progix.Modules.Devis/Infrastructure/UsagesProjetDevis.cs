using Microsoft.EntityFrameworkCore;
using Progix.Modules.Devis.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Devis.Infrastructure;

/// <summary>Un projet dont un devis a quitté l'état brouillon (envoyé, validé, expiré, rejeté, remplacé) est engagé.</summary>
public class UsagesProjetDevis(DevisDbContext db) : IUsagesProjet
{
    public async Task<string?> UsageAsync(Guid projetId, CancellationToken ct = default) =>
        await db.Devis.AnyAsync(d => d.ProjetId == projetId && d.Etat != EtatsDevis.Brouillon, ct) ? "un devis a déjà été envoyé au client" : null;
}
