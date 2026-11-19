using Progix.Modules.Projets.Domain;
using Xunit;

namespace Progix.Tests.Projets;

public class SuppressionDalleTests
{
    [Fact]
    public void Une_dalle_vierge_se_supprime() => Assert.Null(SuppressionDalle.Refus(new Dalle()));

    [Fact]
    public void Une_dalle_avec_etapes_faites_se_garde() =>
        Assert.Contains("étapes", SuppressionDalle.Refus(new Dalle { EtapesFaites = 1 }));

    [Fact]
    public void Une_dalle_avec_import_adapt_se_garde() =>
        Assert.Contains("ADAPT", SuppressionDalle.Refus(new Dalle { Adapt = new AdaptDalle() }));

    [Fact]
    public void Une_dalle_avec_dessinateur_se_garde() =>
        Assert.Contains("dessinateur", SuppressionDalle.Refus(new Dalle { DessinateurId = Guid.NewGuid() }));

    [Fact]
    public void Une_dalle_dont_l_etude_est_planifiee_se_garde() =>
        Assert.Contains("étude", SuppressionDalle.Refus(new Dalle { EtudePlanifiee = new DateOnly(2026, 12, 1) }));

    [Fact]
    public void Une_dalle_avec_avis_de_conformite_se_garde() =>
        Assert.Contains("conformité", SuppressionDalle.Refus(new Dalle { Conforme = true }));

    [Fact]
    public void Le_message_nomme_la_dalle_et_le_motif() =>
        Assert.Equal("La dalle APT-2026-00001-01A ne peut pas être supprimée : elle figure dans le devis DEV-00001-2026.",
            SuppressionDalle.Message("APT-2026-00001-01A", "elle figure dans le devis DEV-00001-2026"));
}
