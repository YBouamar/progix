using Progix.SharedKernel;

namespace Progix.Tests.Devis;

/// <summary>Ratios passifs des conditions de l'offre : un ratio par dalle, le ratio du devis par défaut.</summary>
public class RatiosPassifsOffreTests
{
    private static DevisDocumentLigne Ligne(string reference, decimal surface, decimal ratioPassif, decimal? propre = null) =>
        new(reference, "", surface, 0, ratioPassif, 100, surface * 100, propre);

    private static DevisDocument Devis(decimal? saisi, params DevisDocumentLigne[] lignes) =>
        new(Guid.NewGuid(), "DEV-1", 1, "e", Guid.NewGuid(), "brouillon", "Brouillon", default, default, null, lignes,
            lignes.Sum(l => l.Surface), 0, 0, 0, 0, false, 0, 0, [], null, null, null, null, saisi);

    [Fact]
    public void Sans_saisie_par_dalle_un_seul_ratio_le_calcule()
    {
        // Moyenne pondérée : (10 × 100 + 20 × 300) / 400 = 17,5.
        var (commun, exceptions) = Devis(null, Ligne("01A", 100, 10), Ligne("02A", 300, 20)).RatiosPassifsOffre;
        Assert.Equal(17.5m, commun);
        Assert.Empty(exceptions);
    }

    [Fact]
    public void Une_dalle_differente_suit_le_ratio_commun()
    {
        var (commun, exceptions) = Devis(12, Ligne("01A", 100, 10), Ligne("02A", 100, 10, 15), Ligne("03A", 100, 10, 12)).RatiosPassifsOffre;
        Assert.Equal(12m, commun);
        // 03A saisi à 12 vaut le ratio commun : il n'est pas répété.
        Assert.Equal([("02A", 15m)], exceptions);
    }

    [Fact]
    public void Toutes_les_dalles_au_meme_ratio_un_seul_ratio()
    {
        var (commun, exceptions) = Devis(null, Ligne("01A", 100, 10, 14), Ligne("02A", 100, 20, 14)).RatiosPassifsOffre;
        Assert.Equal(14m, commun);
        Assert.Empty(exceptions);
    }

    [Fact]
    public void Ratios_tous_differents_le_ratio_du_devis_puis_chaque_dalle()
    {
        var (commun, exceptions) = Devis(12, Ligne("01A", 100, 10, 13), Ligne("02A", 100, 10, 15)).RatiosPassifsOffre;
        Assert.Equal(12m, commun);
        Assert.Equal([("01A", 13m), ("02A", 15m)], exceptions);
    }
}
