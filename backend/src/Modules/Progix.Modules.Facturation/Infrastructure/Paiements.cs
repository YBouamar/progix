using Microsoft.EntityFrameworkCore;
using Progix.Modules.Facturation.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Infrastructure;

/// <summary>Paiements par dalle : imputation sur les factures, montants dus, reprise des règlements d'avant le suivi par dalle.</summary>
public static class Paiements
{
    private static DateOnly Jour => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Renseigne le réglé des factures chargées (avec leurs avoirs) à partir des paiements de leurs projets.</summary>
    public static async Task ImputerAsync(this FacturationDbContext db, IReadOnlyCollection<Facture> factures, CancellationToken ct = default)
    {
        if (factures.Count == 0) return;
        var projets = factures.Select(f => f.ProjetId).Distinct().ToList();
        var encaissements = await db.Encaissements.AsNoTracking().Where(e => projets.Contains(e.ProjetId)).ToListAsync(ct);
        // Les factures d'un projet s'imputent ensemble : on recharge celles qui manquent pour respecter l'ordre d'imputation.
        var ids = factures.Select(f => f.Id).ToHashSet();
        var autres = await db.Factures.AsNoTracking().Include(f => f.Avoirs).Where(f => projets.Contains(f.ProjetId) && !ids.Contains(f.Id)).ToListAsync(ct);
        Imputation.Imputer([.. factures, .. autres], encaissements, Jour);
    }

    /// <summary>Montant TTC de chaque dalle du projet au devis validé le plus récent qui la contient ; les dalles hors devis n'y figurent pas.</summary>
    public static async Task<List<DalleDue>> DuesAsync(ProjetResume projet, IDevisLookup devis, CancellationToken ct = default)
    {
        var valides = await devis.ValidesAsync(projet.Id, ct);
        var dues = new List<DalleDue>();
        foreach (var d in projet.Dalles)
        {
            var v = valides.FirstOrDefault(x => x.PrixNet.ContainsKey(d.Id));
            if (v is null) continue;
            var ttcSurHt = v.NetHT > 0 ? v.TotalTTC / v.NetHT : 1;
            dues.Add(new DalleDue(d.Id, d.Reference, d.Emplacement, Math.Round(v.PrixNet[d.Id] * d.Surface * ttcSurHt, 2)));
        }
        return dues;
    }

    /// <summary>Situation de paiement des dalles d'un projet (chargé avec ses dalles).</summary>
    public static async Task<(List<SituationDalle> Dalles, List<Encaissement> Encaissements, List<Facture> Factures)> SituationAsync(
        FacturationDbContext db, ProjetResume projet, IDevisLookup devis, CancellationToken ct = default)
    {
        var factures = await db.Factures.AsNoTracking().Include(f => f.Avoirs).Where(f => f.ProjetId == projet.Id).ToListAsync(ct);
        var encaissements = await db.Encaissements.AsNoTracking().Where(e => e.ProjetId == projet.Id).OrderByDescending(e => e.DatePaiement).ThenByDescending(e => e.CreatedAt).ToListAsync(ct);
        Imputation.Imputer(factures, encaissements, Jour);
        return (Imputation.Situation(await DuesAsync(projet, devis, ct), factures, encaissements, Jour), encaissements, factures);
    }

    /// <summary>
    /// Reprise, une fois, des données d'avant le suivi par dalle : parts par dalle des factures, puis ventilation des
    /// règlements saisis sur une facture au prorata de ses parts (facture d'acompte : au prorata des dalles de son devis).
    /// </summary>
    public static async Task ReprendreAsync(FacturationDbContext db, IProjetsLookup projets, IDevisLookup devis, CancellationToken ct)
    {
        var sansParts = (await db.Factures.Include(f => f.Lignes).ToListAsync(ct)).Where(f => f.Parts.Count == 0).ToList();
        if (sansParts.Count == 0 && !await db.Encaissements.AnyAsync(e => e.ProjetId == Guid.Empty, ct)) return;
        var cache = new Dictionary<Guid, (ProjetResume? Projet, IReadOnlyList<DevisValide> Devis)>();
        async Task<(ProjetResume? Projet, IReadOnlyList<DevisValide> Devis)> Projet(Guid id)
        {
            if (!cache.TryGetValue(id, out var c)) cache[id] = c = (await projets.ProjetAsync(id, avecDalles: true, ct), await devis.ValidesAsync(id, ct));
            return c;
        }
        foreach (var f in sansParts)
        {
            if (f.Type == TypesFacture.Livraison) { f.Parts = Imputation.PartsLivraison(f); continue; }
            var (p, valides) = await Projet(f.ProjetId);
            var d = valides.FirstOrDefault(x => x.Id == f.DevisId);
            if (p is null || d is null) continue;
            var dalles = p.Dalles.Where(x => d.PrixNet.ContainsKey(x.Id)).ToList();
            f.Parts = Imputation.Repartir(f.TotalTTC, dalles.Select(x => (x.Id, d.PrixNet[x.Id] * x.Surface)).ToList())
                .Select(x => new PartFacture { DalleId = x.Cle, MontantTTC = x.Montant, Reference = dalles.First(v => v.Id == x.Cle).Reference }).ToList();
        }
        await db.SaveChangesAsync(ct);

        var anciens = await db.Encaissements.Where(e => e.ProjetId == Guid.Empty && e.FactureId != null).ToListAsync(ct);
        if (anciens.Count == 0) return;
        var idsFactures = anciens.Select(e => e.FactureId!.Value).Distinct().ToList();
        var factures = await db.Factures.AsNoTracking().Where(f => idsFactures.Contains(f.Id)).ToDictionaryAsync(f => f.Id, ct);
        foreach (var e in anciens)
        {
            if (!factures.TryGetValue(e.FactureId!.Value, out var f)) continue;
            e.ProjetId = f.ProjetId; e.Entite = f.Entite; e.ProjetCode = f.ProjetCode; e.ProjetLibelle = f.ProjetLibelle;
            e.ClientNom = f.ClientNom; e.ClientAdresse = f.ClientAdresse; e.ClientIdentifiants = f.ClientIdentifiants;
            e.Dalles = Imputation.Repartir(e.Montant, f.Parts.Select(x => (x.DalleId, x.MontantTTC)).ToList())
                .Select(x => new PaiementDalle { DalleId = x.Cle, Montant = x.Montant, Reference = f.Parts.First(v => v.DalleId == x.Cle).Reference }).ToList();
        }
        await db.SaveChangesAsync(ct);
    }
}
