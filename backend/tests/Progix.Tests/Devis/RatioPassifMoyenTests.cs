using Progix.SharedKernel;

namespace Progix.Tests.Devis;

/// <summary>Ratio passif moyen imprimé dans les conditions d'offre : moyenne pondérée par la surface des dalles.</summary>
public class RatioPassifMoyenTests
{
    private static DevisDocument Devis(params (decimal Surface, decimal RatioPassif)[] dalles) => DevisSaisi(null, dalles);

    private static DevisDocument DevisSaisi(decimal? saisi, params (decimal Surface, decimal RatioPassif)[] dalles) => new(Guid.NewGuid(), "DEV-00001-2026", 1, "APT", Guid.NewGuid(), "brouillon", "Brouillon",
        new DateOnly(2026, 11, 13), new DateOnly(2026, 12, 13), null,
        dalles.Select(x => new DevisDocumentLigne("R", "E", x.Surface, 2m, x.RatioPassif, 150, 0)).ToList(),
        dalles.Sum(x => x.Surface), 0, 0, 0, 0, false, 0, 0, [], null, null, null, null, saisi);

    [Fact]
    public void Moyenne_ponderee_par_la_surface()
    {
        // 100 m² à 4 et 300 m² à 6 : (400 + 1 800) / 400 = 5,5, pas la moyenne simple de 5.
        Assert.Equal(5.5m, Devis((100m, 4m), (300m, 6m)).RatioPassifMoyen);
    }

    [Fact]
    public void Le_ratio_saisi_remplace_le_ratio_calcule_dans_les_conditions_de_l_offre()
    {
        var d = DevisSaisi(5m, (100m, 4m), (300m, 6m));
        Assert.Equal(5m, d.RatioPassifAffiche);
        // Le ratio calculé reste disponible pour l'écran du devis.
        Assert.Equal(5.5m, d.RatioPassifMoyen);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Sans_ratio_saisi_le_ratio_calcule_s_applique(int? saisi) =>
        Assert.Equal(5.5m, DevisSaisi(saisi, (100m, 4m), (300m, 6m)).RatioPassifAffiche);

    [Fact]
    public void Zero_sans_surface_ou_sans_dalle()
    {
        Assert.Equal(0m, Devis().RatioPassifMoyen);
        Assert.Equal(0m, Devis((0m, 6m)).RatioPassifMoyen);
    }
}
