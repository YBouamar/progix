using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets;

public class ProjetsLookup(ProjetsDbContext db) : IProjetsLookup, IProjetsStatut
{
    public Task PasserEnEtudeAsync(Guid projetId, CancellationToken ct = default) => Changer(projetId, StatutsProjet.Cree, StatutsProjet.EnEtude, ct);

    public Task RevenirCreeAsync(Guid projetId, CancellationToken ct = default) => Changer(projetId, StatutsProjet.EnEtude, StatutsProjet.Cree, ct);

    private Task Changer(Guid id, string de, string vers, CancellationToken ct) =>
        db.Projets.Where(p => p.Id == id && p.Statut == de)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Statut, vers).SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);

    public async Task<ProjetResume?> ProjetAsync(Guid id, bool avecDalles = false, CancellationToken ct = default)
    {
        var p = await db.Projets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        var dalles = avecDalles
            ? (await db.Dalles.AsNoTracking().Where(d => d.ProjetId == id).OrderBy(d => d.Numero).ToListAsync(ct))
                .Select(d => new DalleResume(d.Id, $"{p.Code}-{d.Suffixe}",
                    string.Join(" · ", new[] { d.Bloc, d.Immeuble, Dalle.NiveauLabel(d.Niveau) }.Where(s => !string.IsNullOrWhiteSpace(s))), d.Surface, d.EtapesFaites, d.Conforme)).ToList()
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

    private static ProjetResume Resume(Projet p, IReadOnlyList<DalleResume> dalles) =>
        new(p.Id, p.Code, p.Libelle, p.Entite, p.ClientId, p.TypeConstruction, p.Supprime, dalles, p.Bloque, p.Ville);
}
