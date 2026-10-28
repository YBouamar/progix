using Progix.Modules.Devis.Domain;

namespace Progix.Tests.Devis;

/// <summary>Prix au m² et totaux du devis (Devis/Domain/Calcul.cs).</summary>
public class CalculTests
{
    private static LigneDevis Ligne(decimal surface = 100, decimal ratioActif = 10) => new()
    {
        Surface = surface, RatioActif = ratioActif,
        Systemes = [new SystemeLigne { Torons = 4, Ancrages = 10, Gaine = 200 }],
    };

    /// <summary>Grille de l'exemple métier : de 0 à 100 m² = 120, de 101 à 2 000 m² = 100, au-delà = 90 (DH/m²).</summary>
    private static readonly List<TrancheForfait> Grille =
        [new() { APartirDe = 2001, Prix = 90 }, new() { APartirDe = 0, Prix = 120 }, new() { APartirDe = 101, Prix = 100 }];

    private static readonly List<TrancheForfait> Simple = [new() { APartirDe = 0, Prix = 100 }];

    [Theory]
    [InlineData(0, 120)]
    [InlineData(100, 120)]
    [InlineData(100.5, 120)]
    [InlineData(101, 100)]
    [InlineData(2000, 100)]
    [InlineData(2001, 90)]
    [InlineData(50000, 90)]
    public void Forfait_selon_la_tranche_de_surface_totale(decimal surface, decimal attendu) =>
        Assert.Equal(attendu, Calcul.Forfait(Grille, surface));

    [Fact]
    public void Grille_vide_donne_la_grille_d_origine()
    {
        Assert.Equal(100, Calcul.Forfait([], 9999));
        Assert.Equal(85, Calcul.Forfait([], 10000));
    }

    [Fact]
    public void Prix_au_m2_partie_entiere_plus_un()
    {
        // (10 × 100 × 4 + 200 × 3 × 4) / 100 = 64 ; 10 × 12 × 1,05 = 126 ; 64 + 126 + 100 = 290, d'où 291.
        Assert.Equal(291, Calcul.PrixM2(Ligne(), 100));
    }

    [Fact]
    public void Prix_au_m2_nul_sans_surface() => Assert.Equal(0, Calcul.PrixM2(Ligne(surface: 0), 100));

    [Fact]
    public void Recalculer_applique_remise_et_tva()
    {
        var d = new DevisEntete { RemiseM2 = 10, Lignes = [Ligne()] };
        Calcul.Recalculer(d, Simple, exonereTva: false);
        Assert.Equal(100, d.SurfaceTotale);
        Assert.Equal(100, d.Forfait);
        Assert.Equal(29100, d.TotalHT);
        Assert.Equal(1000, d.RemiseTotale);
        Assert.Equal(28100, d.NetHT);
        Assert.Equal(5620, d.Tva);
        Assert.Equal(33720, d.TotalTTC);
    }

    [Fact]
    public void Client_exonere_tva_a_zero()
    {
        var d = new DevisEntete { Lignes = [Ligne()] };
        Calcul.Recalculer(d, Simple, exonereTva: true);
        Assert.True(d.ExonereTva);
        Assert.Equal(0, d.Tva);
        Assert.Equal(d.NetHT, d.TotalTTC);
    }

    [Fact]
    public void Remise_plafonnee_au_total_ht()
    {
        var d = new DevisEntete { RemiseM2 = 100000, Lignes = [Ligne()] };
        Calcul.Recalculer(d, Simple, exonereTva: false);
        Assert.Equal(d.TotalHT, d.RemiseTotale);
        Assert.Equal(0, d.TotalTTC);
    }

    [Fact]
    public void Toutes_les_dalles_prennent_le_forfait_de_la_surface_totale()
    {
        // Deux dalles de 60 m² : 120 m² au total, donc 100 DH/m² pour chacune, et non 120.
        var d = new DevisEntete { Lignes = [Ligne(surface: 60, ratioActif: 0), Ligne(surface: 60, ratioActif: 0)] };
        Calcul.Recalculer(d, Grille, exonereTva: false);
        Assert.Equal(100, d.Forfait);
        Assert.All(d.Lignes, l => Assert.Equal(Calcul.PrixM2(l, 100), l.PrixM2));
    }

    [Fact]
    public void Echeance_du_devis_selon_la_duree_de_validite()
    {
        var d = new DevisEntete { DateDevis = new DateOnly(2026, 10, 1), DureeValidite = 30 };
        Assert.Equal(new DateOnly(2026, 10, 31), d.Echeance);
    }
}
