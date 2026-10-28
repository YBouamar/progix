using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets;

public class ProjetsLookup(ProjetsDbContext db, IDevisLookup devis) : IProjetsLookup, IProjetsStatut
{
    public async Task<string?> ModifierSurfaceDalleAsync(Guid projetId, Guid dalleId, decimal surface, CancellationToken ct = default)
    {
        var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projetId, ct);
        var d = await db.Dalles.FirstOrDefaultAsync(x => x.Id == dalleId && x.ProjetId == projetId, ct);
        if (projet is null || d is null) return "Dalle introuvable dans ce projet.";
        if (projet.Supprime) return "Un projet supprimé est en lecture seule.";
        surface = Math.Round(surface, 2);
        if (surface <= 0) return $"Dalle {projet.Code}-{d.Suffixe} : la surface doit être supérieure à 0.";
        if (surface == d.Surface) return null;
        // Même règle que sur la fiche des dalles : une surface chiffrée dans un devis envoyé, expiré ou validé impose d'abord une nouvelle version de ce devis.
        if (await devis.DevisSurAutreSurfaceAsync(d.Id, surface, ct) is { } engage)
            return $"Dalle {projet.Code}-{d.Suffixe} : sa surface est chiffrée dans le devis {engage}. Créez d'abord une nouvelle version de ce devis.";
        d.Surface = surface;
        d.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return null;
    }

    public Task PasserEnEtudeAsync(Guid projetId, CancellationToken ct = default) => Changer(projetId, StatutsProjet.Cree, StatutsProjet.EnEtude, ct);

    public Task RevenirCreeAsync(Guid projetId, CancellationToken ct = default) => Changer(projetId, StatutsProjet.EnEtude, StatutsProjet.Cree, ct);

    private Task Changer(Guid id, string de, string vers, CancellationToken ct) =>
        db.Projets.Where(p => p.Id == id && p.Statut == de)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Statut, vers).SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);

    public async Task<ProjetResume?> ProjetAsync(Guid id, bool avecDalles = false, CancellationToken ct = default)
    {
        var p = await db.Projets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        var commandes = avecDalles
            ? await (from c in db.CommandesUsine.AsNoTracking() join d in db.Dalles.AsNoTracking() on c.DalleId equals d.Id
                     where d.ProjetId == id && c.Surface != null select new { c.DalleId, c.Surface }).ToDictionaryAsync(x => x.DalleId, x => x.Surface, ct)
            : new Dictionary<Guid, decimal?>();
        var dalles = avecDalles
            ? (await db.Dalles.AsNoTracking().Where(d => d.ProjetId == id).OrderBy(d => d.Numero).ToListAsync(ct))
                .Select(d => new DalleResume(d.Id, $"{p.Code}-{d.Suffixe}",
                    string.Join(" · ", new[] { d.Bloc, d.Immeuble, Dalle.NiveauLabel(d.Niveau) }.Where(s => !string.IsNullOrWhiteSpace(s))), d.Surface, d.EtapesFaites, d.Conforme,
                    d.Adapt is null ? null : new AdaptResume(d.Adapt.RatioActif, d.Adapt.RatioPassif,
                        d.Adapt.Systemes.Select(s => new SystemeAdaptResume(s.Torons, s.Cables, s.LongueurTorons)).ToList()),
                    commandes.GetValueOrDefault(d.Id))).ToList()
            : [];
        return Resume(p, dalles);
    }

    public async Task<IReadOnlyList<Intervenant>> IntervenantsAsync(Guid projetId, CancellationToken ct = default)
    {
        var rows = await db.ProjetPartenaires.AsNoTracking().Where(x => x.ProjetId == projetId).OrderBy(x => x.CreatedAt).ToListAsync(ct);
        return RolesPartenaire.All.SelectMany(r => rows.Where(x => x.Role == r.Code).Select(x => new Intervenant(r.Code, r.Label, x.PartenaireId))).ToList();
    }

    public Task<bool> EstChefChantierAsync(Guid projetId, Guid userId, CancellationToken ct = default) =>
        db.Affectations.AnyAsync(a => a.ProjetId == projetId && a.UserId == userId && a.Fonction == Fonctions.ChefChantier && a.Fin == null, ct);

    public async Task<IReadOnlyDictionary<Guid, ProjetResume>> ProjetsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var liste = ids.Distinct().ToList();
        if (liste.Count == 0) return new Dictionary<Guid, ProjetResume>();
        var rows = await db.Projets.AsNoTracking().Where(p => liste.Contains(p.Id)).ToListAsync(ct);
        return rows.ToDictionary(p => p.Id, p => Resume(p, []));
    }

    public async Task<IReadOnlyList<Guid>> RechercherAsync(string texte, CancellationToken ct = default)
    {
        var like = $"%{texte.Trim()}%";
        return await db.Projets.AsNoTracking()
            .Where(p => EF.Functions.ILike(p.Code, like) || EF.Functions.ILike(p.Libelle, like))
            .Select(p => p.Id).Take(200).ToListAsync(ct);
    }

    public Task DefinirCommercialSiAbsentAsync(Guid projetId, Guid commercialId, string commercialNom, CancellationToken ct = default) =>
        db.Projets.Where(p => p.Id == projetId && !p.CommercialRepris && p.CommercialId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.CommercialId, commercialId).SetProperty(p => p.CommercialNom, commercialNom).SetProperty(p => p.CommercialRepris, true), ct);

    private static ProjetResume Resume(Projet p, IReadOnlyList<DalleResume> dalles) =>
        new(p.Id, p.Code, p.Libelle, p.Entite, p.ClientId, p.TypeConstruction, p.Supprime, dalles, p.Bloque, p.Ville,
            p.CommercialId, p.CommercialNom, TypesConstruction.Libelle(p.TypeConstruction));
}
