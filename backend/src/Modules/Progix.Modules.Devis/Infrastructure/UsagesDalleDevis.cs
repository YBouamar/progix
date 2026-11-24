using Microsoft.EntityFrameworkCore;
using Progix.Modules.Devis.Domain;
using Progix.Modules.Devis.Endpoints;
using Progix.SharedKernel;

namespace Progix.Modules.Devis.Infrastructure;

/// <summary>
/// Une dalle chiffrée dans un devis déjà validé ne se supprime pas. Dans un devis brouillon, envoyé ou expiré, elle est retirée
/// (voir <see cref="RetraitDalle"/>) au moment de la suppression ; un devis rejeté garde sa ligne.
/// </summary>
public class UsagesDalleDevis(DevisDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires) : IUsagesDalle
{
    public async Task<string?> UsageAsync(Guid dalleId, CancellationToken ct = default)
    {
        var devis = await db.Lignes.AsNoTracking().Where(l => l.DalleId == dalleId)
            .Join(db.Devis.AsNoTracking(), l => l.DevisId, d => d.Id, (l, d) => new { d.Numero, d.Version, d.Etat }).ToListAsync(ct);
        var bloquant = devis.FirstOrDefault(d => RetraitDalle.Bloque(d.Etat));
        return bloquant is null ? null : $"elle figure dans le devis {bloquant.Numero} v{bloquant.Version}, déjà validé ({EtatsDevis.Labels[bloquant.Etat].ToLowerInvariant()})";
    }

    public async Task RetirerAsync(Guid dalleId, string par, CancellationToken ct = default)
    {
        var ids = await db.Lignes.AsNoTracking().Where(l => l.DalleId == dalleId).Select(l => l.DevisId).Distinct().ToListAsync(ct);
        if (ids.Count == 0) return;
        var grille = (await db.ParametresAsync()).Forfaits;
        foreach (var d in (await db.Devis.Include(x => x.Lignes).Where(x => ids.Contains(x.Id)).ToListAsync(ct)).Where(x => RetraitDalle.Modifiable(x.Etat)))
        {
            var projet = await projets.ProjetAsync(d.ProjetId, avecDalles: false);
            if (projet is null) continue;
            var etatAvant = EtatsDevis.Labels[d.Etat];
            if (!RetraitDalle.Retirer(d, dalleId, grille, await DevisEndpoints.Exonere(projet, partenaires), projet.ForfaitImpose)) continue;
            d.UpdatedAt = DateTime.UtcNow;
            db.Add(new EvenementDevis { DevisId = d.Id, Quoi = etatAvant == EtatsDevis.Labels[EtatsDevis.Brouillon]
                ? "Une dalle supprimée du projet a été retirée du devis : prix recalculé, relecture à refaire"
                : $"Une dalle supprimée du projet a été retirée du devis ({etatAvant.ToLowerInvariant()}) : il redevient brouillon, à relire, signer et renvoyer", Qui = par });
        }
        await db.SaveChangesAsync(ct);
    }
}
