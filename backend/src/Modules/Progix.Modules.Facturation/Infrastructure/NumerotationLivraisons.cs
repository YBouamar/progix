using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Infrastructure;

/// <summary>Numérotation des bons de livraison par entité et par an, et renumérotation quand un projet change d'entité.</summary>
public partial class NumerotationLivraisons(FacturationDbContext db) : IReaffectationEntite
{
    [GeneratedRegex(@"^BL-(\d{5})-(\d{4})$")]
    private static partial Regex Format();

    public async Task ReaffecterAsync(ProjetResume projet, string ancienneEntite, string par, CancellationToken ct = default)
    {
        var bons = await db.BonsLivraison.Include(b => b.Lignes).Where(b => b.ProjetId == projet.Id).OrderBy(b => b.DateLivraison).ThenBy(b => b.Numero).ToListAsync(ct);
        foreach (var b in bons)
        {
            var annee = b.DateLivraison.Year;
            b.AncienNumero = $"{b.Numero} ({ancienneEntite})";
            b.Numero = $"BL-{await db.NumeroSuivantAsync($"bl:{projet.Entite}:{annee}", ct):00000}-{annee}";
            b.Entite = projet.Entite;
            foreach (var l in b.Lignes)
                if (projet.Dalles.FirstOrDefault(x => x.Id == l.DalleId) is { } dalle) l.Reference = l.Reference.Replace(l.Reference[..l.Reference.LastIndexOf('-')], dalle.Reference[..dalle.Reference.LastIndexOf('-')]);
        }
        // Les factures du projet passent elles aussi dans la numérotation de la nouvelle entité.
        var factures = await db.Factures.Where(f => f.ProjetId == projet.Id).OrderBy(f => f.DateFacture).ThenBy(f => f.Numero).ToListAsync(ct);
        foreach (var f in factures)
        {
            var annee = f.DateFacture.Year;
            f.AncienNumero = $"{f.Numero} ({ancienneEntite})";
            f.Numero = $"FAC-{await db.NumeroSuivantAsync($"facture:{projet.Entite}:{annee}", ct):00000}-{annee}";
            f.Entite = projet.Entite;
            f.ProjetCode = projet.Code;
        }
        // Les dépenses de dalle suivent le projet : entité, code et références.
        var depenses = await db.Depenses.Where(x => x.ProjetId == projet.Id).ToListAsync(ct);
        foreach (var x in depenses)
        {
            x.Entite = projet.Entite;
            x.ProjetCode = projet.Code;
            if (projet.Dalles.FirstOrDefault(v => v.Id == x.DalleId) is { } dalle) x.DalleReference = dalle.Reference;
        }
        var idsFactures = factures.Select(f => f.Id).ToList();
        var recus = await db.Encaissements.Where(x => x.NumeroRecu != null && idsFactures.Contains(x.FactureId)).OrderBy(x => x.DatePaiement).ThenBy(x => x.NumeroRecu).ToListAsync(ct);
        foreach (var x in recus)
        {
            var annee = x.DatePaiement.Year;
            x.AncienNumeroRecu = $"{x.NumeroRecu} ({ancienneEntite})";
            x.NumeroRecu = x.Remboursement
                ? $"RMB-{await db.NumeroSuivantAsync($"remboursement:{projet.Entite}:{annee}", ct):00000}-{annee}"
                : $"REC-{await db.NumeroSuivantAsync($"recu:{projet.Entite}:{annee}", ct):00000}-{annee}";
        }
        var contrats = await db.Contrats.Where(c => c.ProjetId == projet.Id).OrderBy(c => c.DateContrat).ThenBy(c => c.Numero).ToListAsync(ct);
        foreach (var c in contrats)
        {
            var annee = c.DateContrat.Year;
            c.AncienNumero = $"{c.Numero} ({ancienneEntite})";
            c.Numero = $"CTR-{await db.NumeroSuivantAsync($"contrat:{projet.Entite}:{annee}", ct):00000}-{annee}";
            c.Entite = projet.Entite;
        }
        var courriers = await db.Courriers.Where(c => c.ProjetId == projet.Id).OrderBy(c => c.DateCourrier).ThenBy(c => c.Numero).ToListAsync(ct);
        foreach (var c in courriers)
        {
            c.Entite = projet.Entite;
            if (c.Type != Domain.TypesCourrier.Attestation) continue;
            c.AncienNumero = $"{c.Numero} ({ancienneEntite})";
            c.Numero = await NumerotationCourriers.SuivantAsync(db, c, ct);
        }
        var avoirs = await db.Avoirs.Where(a => a.ProjetId == projet.Id).OrderBy(a => a.DateAvoir).ThenBy(a => a.Numero).ToListAsync(ct);
        foreach (var a in avoirs)
        {
            var annee = a.DateAvoir.Year;
            a.AncienNumero = $"{a.Numero} ({ancienneEntite})";
            a.Numero = $"AV-{await db.NumeroSuivantAsync($"avoir:{projet.Entite}:{annee}", ct):00000}-{annee}";
            a.Entite = projet.Entite;
        }
        await db.SaveChangesAsync(ct);
    }

    public static async Task ReprendreAsync(FacturationDbContext db, IProjetsLookup projets, CancellationToken ct)
    {
        var sans = await db.BonsLivraison.Where(b => b.Entite == "").ToListAsync(ct);
        if (sans.Count > 0)
        {
            var infos = await projets.ProjetsAsync(sans.Select(b => b.ProjetId), ct);
            foreach (var b in sans) b.Entite = infos.GetValueOrDefault(b.ProjetId)?.Entite ?? Entites.Apt;
            await db.SaveChangesAsync(ct);
        }
        var numeros = await db.BonsLivraison.AsNoTracking().Select(b => new { b.Entite, b.Numero }).ToListAsync(ct);
        foreach (var g in numeros.Select(n => new { n.Entite, M = Format().Match(n.Numero) }).Where(x => x.M.Success).GroupBy(x => (x.Entite, Annee: x.M.Groups[2].Value)))
            await db.RecalerAsync($"bl:{g.Key.Entite}:{g.Key.Annee}", g.Max(x => int.Parse(x.M.Groups[1].Value)), ct);
    }
}
