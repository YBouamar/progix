using Progix.Modules.Devis.Domain;
using Progix.Modules.Devis.Endpoints;
using Progix.SharedKernel;

namespace Progix.Tests.Devis;

/// <summary>Reprise des exports ADAPT par une ligne de devis : chaque export n'apporte que sa part.</summary>
public class RepriseAdaptTests
{
    private static LigneDevis Ligne() => new()
    {
        RatioActif = 2.5m, RatioPassif = 6m, Systemes = [new SystemeLigne { Torons = 3, Ancrages = 9, Gaine = 80m }],
    };

    private static DalleResume Dalle(AdaptResume? a) => new(Guid.NewGuid(), "APT-1-01A", "PH RDC", 200m, Adapt: a);

    [Fact]
    public void Sans_export_la_ligne_reste_telle_quelle()
    {
        var l = Ligne();
        Assert.False(DevisEndpoints.RepriseAdapt(l, Dalle(null)));
        Assert.Equal(2.5m, l.RatioActif);
    }

    [Fact]
    public void RebarTotal_seul_ne_reprend_que_le_ratio_passif()
    {
        var l = Ligne();
        Assert.True(DevisEndpoints.RepriseAdapt(l, Dalle(new AdaptResume(0m, 4.35m, [], AvecPt: false, AvecRebar: true))));
        Assert.Equal(2.5m, l.RatioActif);
        Assert.Equal(4.35m, l.RatioPassif);
        Assert.Single(l.Systemes);
    }

    [Fact]
    public void PTTotal_seul_reprend_ratio_actif_et_cables_sans_toucher_au_ratio_passif()
    {
        var l = Ligne();
        Assert.True(DevisEndpoints.RepriseAdapt(l, Dalle(new AdaptResume(2.05m, 0m, [new SystemeAdaptResume(4, 28, 301.2m)], AvecPt: true, AvecRebar: false))));
        Assert.Equal(2.05m, l.RatioActif);
        Assert.Equal(6m, l.RatioPassif);
        Assert.Equal(4, Assert.Single(l.Systemes).Torons);
    }
}
