using Progix.SharedKernel;
using Xunit;

namespace Progix.Tests.Devis;

/// <summary>Forfait imposé à un devis : celui du projet, sinon celui du type de construction, sinon la grille (null).</summary>
public class ForfaitTypeTests
{
    private static ProjetResume Projet(decimal? propre, decimal? type) =>
        new(Guid.NewGuid(), "APT-2026-00001", "Essai", "apt", Guid.NewGuid(), "hangar", false, [], ForfaitPropre: propre, ForfaitType: type);

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(null, 120.0, 120.0)]
    [InlineData(95.0, 120.0, 95.0)]   // le forfait propre du projet passe avant celui du type
    [InlineData(95.0, null, 95.0)]
    [InlineData(0.0, 120.0, 120.0)]   // zéro équivaut à vide
    [InlineData(null, 0.0, null)]
    public void Priorite_projet_puis_type_puis_grille(double? propre, double? type, double? attendu) =>
        Assert.Equal((decimal?)attendu, Projet((decimal?)propre, (decimal?)type).ForfaitImpose);
}
