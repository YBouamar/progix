using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Endpoints;

namespace Progix.Tests.Projets;

/// <summary>Allongements de mise en tension et tolérance EN 13670 : ± 15 % par câble, ± 5 % pour le groupe.</summary>
public class TensionTests
{
    private static MesureTension Cable(decimal theorique, decimal deltaA, decimal? deltaB = null) => new()
    {
        Theorique = theorique, A25 = 10, A100 = 10 + deltaA,
        B25 = deltaB is null ? null : 5, B100 = deltaB is null ? null : 5 + deltaB,
    };

    [Theory]
    [InlineData(100, true)]
    [InlineData(85, true)]
    [InlineData(84.9, false)]
    [InlineData(115, true)]
    [InlineData(115.1, false)]
    public void Cable_seul_a_plus_ou_moins_15_pourcent(decimal mesure, bool conforme) =>
        Assert.Equal(conforme, Cable(100, mesure).Conforme);

    [Fact]
    public void Bornes_affichees_du_cable()
    {
        var c = Cable(100, 100);
        Assert.Equal(85.0m, c.Min);
        Assert.Equal(115.0m, c.Max);
    }

    [Fact]
    public void Tension_des_deux_cotes()
    {
        var c = Cable(100, 50, 50);
        Assert.Equal(100, c.Mesure);
        Assert.Equal(50, c.Moyenne);
        Assert.Equal(0, c.Ecart);
    }

    [Fact]
    public void Cote_b_commence_mais_incomplet()
    {
        var c = new MesureTension { Theorique = 100, A25 = 10, A100 = 110, B25 = 5 };
        Assert.True(c.CoteBIncomplet);
        Assert.Null(c.Mesure);
        Assert.Null(c.Conforme);
    }

    [Fact]
    public void Groupe_hors_tolerance_meme_si_chaque_cable_est_conforme()
    {
        // 110 et 104 : chaque câble dans ± 15 %, mais le groupe fait + 7 %.
        var s = TensionEndpoints.Calculer([Cable(100, 110), Cable(100, 104)]);
        Assert.Equal(0, s.HorsTolerance);
        Assert.Equal(7.0m, s.Ecart);
        Assert.False(s.Conforme);
    }

    [Fact]
    public void Groupe_conforme_a_plus_ou_moins_5_pourcent()
    {
        var s = TensionEndpoints.Calculer([Cable(100, 104), Cable(100, 100)]);
        Assert.True(s.Conforme);
        Assert.Equal(2.0m, s.Ecart);
    }

    [Fact]
    public void Cables_incomplets_comptes_a_part()
    {
        var s = TensionEndpoints.Calculer([Cable(100, 100), new MesureTension { Theorique = 100, A25 = 10 }]);
        Assert.Equal(1, s.Incompletes);
        Assert.Equal(2, s.Total);
        Assert.True(s.Conforme);
    }

    [Fact]
    public void Allongement_theorique_par_metre_des_parametres_par_defaut()
    {
        // 221 kN × (1 − 5 %) / (150 mm² × 195 000 MPa), en millimètres par mètre.
        Assert.Equal(7.178m, new ParametresTension().MmParMetre);
    }
}
