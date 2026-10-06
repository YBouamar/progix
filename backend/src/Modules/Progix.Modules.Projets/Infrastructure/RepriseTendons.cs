using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;

namespace Progix.Modules.Projets.Infrastructure;

/// <summary>
/// Reprise des commandes usine saisies avant le modèle « un tendon par ligne ».
/// Chaque ancienne ligne groupée (préfixe et nombre) devient autant de tendons numérotés 01, 02, 03...,
/// et les fiches de mise en tension et d'injection de la dalle suivent la même numérotation.
/// Sans effet sur une commande déjà au nouveau modèle : la reprise peut tourner à chaque démarrage.
/// </summary>
public static class RepriseTendons
{
    public static async Task<int> ExecuterAsync(ProjetsDbContext db, CancellationToken ct)
    {
        var commandes = (await db.CommandesUsine.ToListAsync(ct)).Where(c => c.Lignes.Any(l => l.Numero is null)).ToList();
        foreach (var c in commandes)
        {
            var anciens = c.Tendons().ToList(); // noms d'origine : X01, X02... ou numéros déjà saisis
            var mesures = await db.MesuresTension.Where(m => m.DalleId == c.DalleId).ToListAsync(ct);
            var injection = await db.FichesInjection.FirstOrDefaultAsync(f => f.DalleId == c.DalleId, ct);
            var nomsFiches = mesures.Select(m => m.Cable).Concat(injection?.Cables.Select(x => x.Cable) ?? []).Select(n => n.Trim().ToUpperInvariant()).ToHashSet();
            var nomsCommande = anciens.Select(t => t.Numero.ToUpperInvariant()).ToHashSet();

            // Nouveau numéro : le rang du tendon dans la commande. Si une fiche porte déjà un nom qui se confondrait
            // avec ces numéros (un câble ajouté à la main et nommé « 3 »), les noms d'origine sont gardés.
            var numeros = Enumerable.Range(1, anciens.Count).Select(i => i.ToString("00")).ToList();
            var conflit = nomsFiches.Any(n => !nomsCommande.Contains(n) && numeros.Contains(n));
            var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < anciens.Count; i++) table.TryAdd(anciens[i].Numero, conflit ? anciens[i].Numero : numeros[i]);

            c.Lignes = anciens.Select((t, i) => new LigneCommande
            {
                Numero = conflit ? t.Numero : numeros[i], Torons = t.Torons, Sens = t.Sens is "X" or "Y" ? t.Sens : "X", Prefixe = "", Nombre = 1,
                Longueur = t.Longueur, AncragesSaisis = t.Ancrages,
            }).ToList();
            c.NbCables = c.Lignes.Count;
            foreach (var m in mesures) if (table.TryGetValue(m.Cable.Trim(), out var n)) m.Cable = n;
            if (injection is not null)
                foreach (var x in injection.Cables) if (table.TryGetValue(x.Cable.Trim(), out var n)) x.Cable = n;
        }
        if (commandes.Count > 0) await db.SaveChangesAsync(ct);

        // Numéros simples à mettre sur deux chiffres (« 1 » devient « 01 »), fiches comprises.
        var aFormater = (await db.CommandesUsine.ToListAsync(ct)).Where(c => c.Lignes.Any(l => l.Numero is { Length: 1 } n && char.IsAsciiDigit(n[0]))).ToList();
        foreach (var c in aFormater)
        {
            var table = c.Lignes.Where(l => l.Numero is { Length: 1 } n && char.IsAsciiDigit(n[0])).ToDictionary(l => l.Numero!, l => "0" + l.Numero);
            if (c.Lignes.Any(l => table.ContainsValue(l.Numero ?? ""))) continue; // « 1 » et « 01 » coexistent : on ne touche à rien
            c.Lignes = c.Lignes.Select(l => new LigneCommande
            {
                Numero = l.Numero is not null && table.TryGetValue(l.Numero, out var n) ? n : l.Numero, Torons = l.Torons, Sens = l.Sens, Prefixe = l.Prefixe, Nombre = l.Nombre,
                Longueur = l.Longueur, AncragesSaisis = l.AncragesSaisis,
            }).ToList();
            foreach (var m in await db.MesuresTension.Where(m => m.DalleId == c.DalleId).ToListAsync(ct)) if (table.TryGetValue(m.Cable.Trim(), out var n)) m.Cable = n;
            if (await db.FichesInjection.FirstOrDefaultAsync(f => f.DalleId == c.DalleId, ct) is { } inj)
                foreach (var x in inj.Cables) if (table.TryGetValue(x.Cable.Trim(), out var n)) x.Cable = n;
        }
        if (aFormater.Count > 0) await db.SaveChangesAsync(ct);
        return commandes.Count + aFormater.Count;
    }
}
