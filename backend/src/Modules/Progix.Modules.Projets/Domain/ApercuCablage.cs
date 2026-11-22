using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

/// <summary>Une ligne de la commande à créer depuis les plans de câblage : longueur tracée, surlongueur et longueur commandée.</summary>
public record LigneApercu(string Numero, int Torons, string Sens, decimal Tracee, int Actives, decimal Surlongueur, decimal Longueur, int Ancrages);

/// <summary>Total par orientation et par système, pour contrôler d'un coup d'oeil avant l'import.</summary>
public record RecapApercu(string Sens, int Torons, int Cables, decimal Longueur);

/// <summary>
/// Commande proposée à partir des deux plans de câblage DXF (X et Y), recoupée avec le PTTotal quand il est connu.
/// Rien n'est enregistré : c'est l'aperçu que l'utilisateur confirme.
/// </summary>
public class ApercuCablage
{
    public List<LigneApercu> Lignes { get; } = [];
    public List<RecapApercu> Recap { get; } = [];
    /// <summary>Bloquent l'import.</summary>
    public List<string> Erreurs { get; } = [];
    public List<string> Avertissements { get; } = [];
    /// <summary>Surface réelle lue sur les plans (région de dalle moins ouvertures), en m², ou null si absente.</summary>
    public decimal? SurfaceDxf { get; set; }
    /// <summary>Vrai si la surface des plans diffère de celle de la dalle : l'utilisateur décide de la modifier ou non.</summary>
    public bool SurfaceDifferente { get; set; }

    public bool Importable => Erreurs.Count == 0 && Lignes.Count > 0;

    /// <summary>
    /// Compose l'aperçu. La surlongueur (cm) s'ajoute à chaque extrémité active du câble ; les extrémités actives par câble viennent du PTTotal
    /// (colonne « Stressing ends », en moyenne par système), 1 à défaut de PTTotal. Les ancrages valent 2 par tendon, modifiables ensuite.
    /// </summary>
    public static ApercuCablage Composer(IReadOnlyList<PlanCablage> plans, IReadOnlyList<SystemeAdapt>? pt, decimal surlongueurCm, decimal surfaceDalle)
    {
        var a = new ApercuCablage();
        if (plans.Count == 0) { a.Erreurs.Add("Aucun plan de câblage DXF n'est déposé dans le dossier de plans."); return a; }
        if (surlongueurCm < 0 || surlongueurCm > 200) a.Erreurs.Add("La surlongueur doit être comprise entre 0 et 200 cm.");

        foreach (var p in plans)
        {
            a.Erreurs.AddRange(p.Erreurs.Select(e => $"{p.Nom} : {e}"));
            a.Avertissements.AddRange(p.Avertissements.Select(e => $"{p.Nom} : {e}"));
        }
        foreach (var g in plans.GroupBy(p => p.Orientation).Where(g => g.Key.Length > 0 && g.Count() > 1))
            a.Erreurs.Add($"Deux plans portent l'orientation {g.Key} ({string.Join(" et ", g.Select(p => p.Nom))}) : il faut un plan X et un plan Y.");
        if (plans.Count == 1) a.Avertissements.Add($"Un seul plan de câblage ({plans[0].Nom}) : la commande ne contiendra que les tendons {plans[0].Orientation}.");

        var actives = new Dictionary<int, int>();
        if (pt is not null)
            foreach (var s in pt.Where(s => s.Cables > 0))
                actives[s.Torons] = s.ExtremitesActives > 0 ? Math.Max(1, (int)Math.Round((double)s.ExtremitesActives / s.Cables, MidpointRounding.AwayFromZero)) : 1;

        // Un numéro de tendon est unique dans la dalle, tous plans confondus.
        foreach (var g in plans.SelectMany(p => p.Cables).GroupBy(c => c.Numero).Where(g => g.Count() > 1))
            a.Erreurs.Add($"Le tendon {g.Key:00} figure sur plusieurs plans ({string.Join(", ", g.Select(c => c.Orientation))}) : un numéro de tendon ne se commande qu'une fois.");

        foreach (var c in plans.SelectMany(p => p.Cables).OrderBy(c => c.Orientation).ThenBy(c => c.Numero))
        {
            var act = actives.TryGetValue(c.Torons, out var n) ? n : 1;
            var sur = Math.Round(act * surlongueurCm / 100m, 2);
            a.Lignes.Add(new LigneApercu(c.Numero.ToString("00"), c.Torons, c.Orientation, Math.Round(c.Longueur, 2), act, sur, Math.Round(c.Longueur + sur, 2), 2));
        }
        foreach (var g in a.Lignes.GroupBy(l => (l.Sens, l.Torons)).OrderBy(g => g.Key.Sens).ThenBy(g => g.Key.Torons))
            a.Recap.Add(new RecapApercu(g.Key.Sens, g.Key.Torons, g.Count(), g.Sum(l => l.Longueur)));

        // Recoupement avec le PTTotal : nombre de câbles par système.
        if (pt is not null && plans.Count == 2 && a.Erreurs.Count == 0)
        {
            var dxf = a.Lignes.GroupBy(l => l.Torons).ToDictionary(g => g.Key, g => g.Count());
            foreach (var t in dxf.Keys.Union(pt.Select(s => s.Torons)).OrderBy(t => t))
            {
                var (d, p) = (dxf.GetValueOrDefault(t), pt.FirstOrDefault(s => s.Torons == t)?.Cables ?? 0);
                if (d != p) a.Avertissements.Add($"Système S{t} : {d} câble{(d > 1 ? "s" : "")} sur les plans DXF, {p} dans le PTTotal.");
            }
        }
        else if (pt is null) a.Avertissements.Add("Aucun PTTotal pour cette dalle : extrémités actives supposées à 1 par câble, recoupement impossible.");

        var surfaces = plans.Where(p => p.SurfaceNette is > 0).Select(p => Math.Round(p.SurfaceNette!.Value, 2)).Distinct().ToList();
        if (surfaces.Count > 1) a.Avertissements.Add($"Les plans ne donnent pas la même surface ({string.Join(" et ", surfaces.Select(s => s.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("fr-FR"))))} m²).");
        a.SurfaceDxf = surfaces.Count > 0 ? surfaces.Max() : null;
        a.SurfaceDifferente = a.SurfaceDxf is { } sd && surfaceDalle > 0 && Math.Round(sd, 2) != Math.Round(surfaceDalle, 2);
        return a;
    }
}
