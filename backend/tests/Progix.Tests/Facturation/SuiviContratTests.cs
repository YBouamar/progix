using Progix.Modules.Facturation.Domain;
using Progix.Modules.Facturation.Endpoints;
using Xunit;

namespace Progix.Tests.Facturation;

/// <summary>Suivi du contrat : en attente, envoyé, validé par le client ; nom du dossier ZIP d'un devis.</summary>
public class SuiviContratTests
{
    private static readonly DateTime Jour = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);
    private static Contrat C(bool signe = true) => new() { Numero = "CTR-00001-2026", Jeton = signe ? "abc" : null };

    [Fact]
    public void Un_contrat_neuf_est_en_attente()
    {
        Assert.Equal(StatutsContrat.EnAttente, C().Statut);
    }

    [Fact]
    public void Le_contrat_avance_d_un_cran_a_la_fois()
    {
        var c = C();
        Assert.NotNull(StatutsContrat.Passer(c, StatutsContrat.ValideClient, false, "Moi", Jour));
        Assert.Null(StatutsContrat.Passer(c, StatutsContrat.Envoye, false, "Moi", Jour));
        Assert.Equal("Moi", c.EnvoyePar);
        Assert.Null(StatutsContrat.Passer(c, StatutsContrat.ValideClient, false, "Moi", Jour));
        Assert.NotNull(c.ValideClientLe);
    }

    [Fact]
    public void Un_contrat_non_signe_ne_part_pas()
    {
        var c = C(signe: false);
        Assert.Contains("Signez", StatutsContrat.Passer(c, StatutsContrat.Envoye, false, "Moi", Jour));
        Assert.Equal(StatutsContrat.EnAttente, c.Statut);
    }

    [Fact]
    public void Reculer_revient_aux_fonctions_qui_annulent_et_efface_la_trace()
    {
        var c = C();
        StatutsContrat.Passer(c, StatutsContrat.Envoye, false, "Moi", Jour);
        Assert.NotNull(StatutsContrat.Passer(c, StatutsContrat.EnAttente, false, "Moi", Jour));
        Assert.Null(StatutsContrat.Passer(c, StatutsContrat.EnAttente, true, "Chef", Jour));
        Assert.Null(c.EnvoyeLe);
    }

    [Fact]
    public void Un_contrat_annule_ou_un_statut_inconnu_est_refuse()
    {
        var c = C(); c.Annule = true;
        Assert.NotNull(StatutsContrat.Passer(c, StatutsContrat.Envoye, false, "Moi", Jour));
        Assert.NotNull(StatutsContrat.Passer(C(), "autre", false, "Moi", Jour));
    }

    [Theory]
    [InlineData("DEV-00012-2026", "Villa Alami", "DEV_00012-2026 Villa Alami")]
    [InlineData("DEV-00003-2027", "Hangar / Zone: 2", "DEV_00003-2027 Hangar _ Zone_ 2")]
    public void Le_dossier_zip_s_appelle_DEV_numero_annee_et_nom_du_projet(string numero, string projet, string attendu)
    {
        Assert.Equal(attendu, PdfEndpoints.NomDossierDevis(numero, projet));
    }
}
