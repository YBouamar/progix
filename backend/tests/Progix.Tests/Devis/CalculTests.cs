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

    [Theory]
    [InlineData("immeuble", 9999.99, 100)]
    [InlineData("immeuble", 10000, 85)]
    [InlineData("villa", 50000, 120)]
    [InlineData("radier", 10, 120)]
    [InlineData("dallage", 10, 50)]
    [InlineData("inconnu", 10, 100)]
    public void Forfait_selon_le_type_et_la_surface(string type, decimal surface, decimal attendu) =>
        Assert.Equal(attendu, Calcul.Forfait(type, surface));

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
        Calcul.Recalculer(d, "immeuble", exonereTva: false);
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
        Calcul.Recalculer(d, "immeuble", exonereTva: true);
        Assert.True(d.ExonereTva);
        Assert.Equal(0, d.Tva);
        Assert.Equal(d.NetHT, d.TotalTTC);
    }

    [Fact]
    public void Remise_plafonnee_au_total_ht()
    {
        var d = new DevisEntete { RemiseM2 = 100000, Lignes = [Ligne()] };
        Calcul.Recalculer(d, "immeuble", exonereTva: false);
        Assert.Equal(d.TotalHT, d.RemiseTotale);
        Assert.Equal(0, d.TotalTTC);
    }

    [Fact]
    public void Echeance_du_devis_selon_la_duree_de_validite()
    {
        var d = new DevisEntete { DateDevis = new DateOnly(2026, 10, 1), DureeValidite = 30 };
        Assert.Equal(new DateOnly(2026, 10, 31), d.Echeance);
    }
}
