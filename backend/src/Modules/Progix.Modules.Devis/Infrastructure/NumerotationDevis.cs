using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Devis.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Devis.Infrastructure;

/// <summary>Numérotation des devis par entité et par an, et renumérotation quand un projet change d'entité.</summary>
public partial class NumerotationDevis(DevisDbContext db) : IReaffectationEntite
{
    [GeneratedRegex(@"^DEV-(\d{5})-(\d{4})$")]
    private static partial Regex Format();

    public async Task ReaffecterAsync(ProjetResume projet, string ancienneEntite, string par, CancellationToken ct = default)
    {
        var devis = await db.Devis.Include(d => d.Lignes).Where(d => d.ProjetId == projet.Id).OrderBy(d => d.CreatedAt).ToListAsync(ct);
        // Toutes les versions d'un même devis gardent un numéro commun.
        foreach (var groupe in devis.GroupBy(d => d.Numero))
        {
            var annee = groupe.Min(d => d.DateDevis).Year;
            var numero = $"DEV-{await db.NumeroSuivantAsync($"devis:{projet.Entite}:{annee}", ct):00000}-{annee}";
            foreach (var d in groupe)
            {
                db.Evenements.Add(new EvenementDevis { DevisId = d.Id, Quoi = $"Projet réaffecté de {ancienneEntite} à {projet.Entite} : ancien numéro {d.Numero}", Qui = par });
                d.Numero = numero;
                d.Entite = projet.Entite;
                foreach (var l in d.Lignes)
                    if (projet.Dalles.FirstOrDefault(x => x.Id == l.DalleId) is { } dalle) l.Reference = l.Reference.Replace(l.Reference[..l.Reference.LastIndexOf('-')], dalle.Reference[..dalle.Reference.LastIndexOf('-')]);
            }
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Reprise des devis créés avant la numérotation par entité : entité renseignée et compteurs recalés.</summary>
    public static async Task ReprendreAsync(DevisDbContext db, IProjetsLookup projets, CancellationToken ct)
    {
        var sans = await db.Devis.Where(d => d.Entite == "").ToListAsync(ct);
        if (sans.Count > 0)
        {
            var infos = await projets.ProjetsAsync(sans.Select(d => d.ProjetId), ct);
            foreach (var d in sans) d.Entite = infos.GetValueOrDefault(d.ProjetId)?.Entite ?? Entites.Apt;
            await db.SaveChangesAsync(ct);
        }
        var numeros = await db.Devis.AsNoTracking().Select(d => new { d.Entite, d.Numero }).Distinct().ToListAsync(ct);
        foreach (var g in numeros.Select(n => new { n.Entite, M = Format().Match(n.Numero) }).Where(x => x.M.Success).GroupBy(x => (x.Entite, Annee: x.M.Groups[2].Value)))
            await db.RecalerAsync($"devis:{g.Key.Entite}:{g.Key.Annee}", g.Max(x => int.Parse(x.M.Groups[1].Value)), ct);
    }
}
