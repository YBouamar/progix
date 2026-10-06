namespace Progix.Modules.Devis.Domain;

/// <summary>
/// Calcul du prix d'un devis.
/// Prix au m² = ENT((Σ ancrages × 100 × torons + Σ gaines × 3 × torons) / surface + ratio actif × 12 × 1,05 + forfait) + 1.
/// </summary>
public static class Calcul
{
    public const decimal TauxTva = 0.20m;
    public const decimal SeuilImmeuble = 10000m;

    /// <summary>Forfait en DH/m² selon le type de construction. Pour un immeuble, il dépend de la surface totale du devis.</summary>
    public static decimal Forfait(string typeConstruction, decimal surfaceTotale) => typeConstruction switch
    {
        "immeuble" => surfaceTotale >= SeuilImmeuble ? 85m : 100m,
        "villa" => 120m,
        "radier" => 120m,
        "dallage" => 50m,
        _ => 100m,
    };

    public static int PrixM2(LigneDevis l, decimal forfait)
    {
        if (l.Surface <= 0) return 0;
        var ancrages = l.Systemes.Sum(s => s.Ancrages * 100m * s.Torons);
        var gaines = l.Systemes.Sum(s => s.Gaine * 3m * s.Torons);
        var brut = (ancrages + gaines) / l.Surface + l.RatioActif * 12m * 1.05m + forfait;
        return (int)Math.Floor(brut) + 1;
    }

    public static void Recalculer(DevisEntete d, string typeConstruction, bool exonereTva)
    {
        d.SurfaceTotale = d.Lignes.Sum(l => l.Surface);
        d.Forfait = Forfait(typeConstruction, d.SurfaceTotale);
        foreach (var l in d.Lignes)
        {
            l.PrixM2 = PrixM2(l, d.Forfait);
            l.MontantHT = Math.Round(l.PrixM2 * l.Surface, 2);
        }
        d.TotalHT = d.Lignes.Sum(l => l.MontantHT);
        d.RemiseTotale = Math.Min(d.TotalHT, Math.Round(d.RemiseM2 * d.SurfaceTotale, 2));
        d.NetHT = d.TotalHT - d.RemiseTotale;
        d.ExonereTva = exonereTva;
        d.Tva = exonereTva ? 0 : Math.Round(d.NetHT * TauxTva, 2);
        d.TotalTTC = d.NetHT + d.Tva;
    }
}
