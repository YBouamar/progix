using Progix.Modules.Devis.Domain;
using Xunit;

namespace Progix.Tests.Devis;

/// <summary>Un brouillon se relit en interne (validé ou refusé avec remarque) avant d'être marqué envoyé ; un projet lancé accepte une variante validée.</summary>
public class RelectureDevisTests
{
    private static readonly Guid Projet = Guid.NewGuid();
    private static DevisEntete D(string etat = EtatsDevis.Brouillon, int version = 1) =>
        new() { Id = Guid.NewGuid(), ProjetId = Projet, Etat = etat, Numero = "DEV-00001-2026", Version = version };
    private static readonly DateTime Jour = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Un_brouillon_non_relu_ne_peut_pas_etre_envoye()
    {
        var refus = ValidationDevis.RefusEnvoi(D());
        Assert.NotNull(refus);
        Assert.Contains("validé", refus);
    }

    [Fact]
    public void Un_brouillon_valide_peut_etre_envoye()
    {
        var d = D();
        Assert.Null(ValidationDevis.Reviser(d, true, null, "Chef", Jour));
        Assert.Equal(RevuesDevis.Validee, d.RevueEtat);
        Assert.Equal("Chef", d.RevuePar);
        Assert.Null(ValidationDevis.RefusEnvoi(d));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Le_refus_exige_une_remarque(string? remarque)
    {
        var d = D();
        Assert.NotNull(ValidationDevis.Reviser(d, false, remarque, "Chef", Jour));
        Assert.Null(d.RevueEtat);
    }

    [Fact]
    public void Un_brouillon_refuse_garde_sa_remarque_et_ne_peut_pas_etre_envoye()
    {
        var d = D();
        Assert.Null(ValidationDevis.Reviser(d, false, "  Ratio actif à revoir  ", "Directeur", Jour));
        Assert.Equal(RevuesDevis.Refusee, d.RevueEtat);
        Assert.Equal("Ratio actif à revoir", d.RevueRemarque);
        Assert.Contains("refusé", ValidationDevis.RefusEnvoi(d));
    }

    [Fact]
    public void Seul_un_brouillon_se_relit()
    {
        Assert.NotNull(ValidationDevis.Reviser(D(EtatsDevis.Envoye), true, null, "Chef", Jour));
    }

    [Fact]
    public void Modifier_le_brouillon_remet_la_relecture_a_zero()
    {
        var d = D();
        ValidationDevis.Reviser(d, true, null, "Chef", Jour);
        Assert.True(ValidationDevis.ReinitialiserRevue(d));
        Assert.Null(d.RevueEtat); Assert.Null(d.RevuePar); Assert.Null(d.RevueLe);
        Assert.False(ValidationDevis.ReinitialiserRevue(d));
    }

    [Fact]
    public void La_version_remplacee_retrouve_son_etat_quand_le_brouillon_est_supprime()
    {
        var validee = D(EtatsDevis.Remplace); validee.ValideLe = Jour; validee.EnvoyeLe = Jour;
        var envoyee = D(EtatsDevis.Remplace); envoyee.EnvoyeLe = Jour;
        Assert.Equal(EtatsDevis.Valide, ValidationDevis.EtatAvantRemplacement(validee));
        Assert.Equal(EtatsDevis.Envoye, ValidationDevis.EtatAvantRemplacement(envoyee));
        Assert.Null(ValidationDevis.EtatAvantRemplacement(D(EtatsDevis.Annule)));
    }

    [Fact]
    public void Projet_lance_une_variante_se_valide_malgre_un_devis_deja_valide_et_le_remplace()
    {
        var ancien = D(EtatsDevis.Valide);
        var variante = D(EtatsDevis.Envoye);
        Assert.NotNull(ValidationDevis.Refus(variante, [ancien], projetLance: false));
        Assert.Null(ValidationDevis.Refus(variante, [ancien], projetLance: true));
        Assert.Equal([ancien], ValidationDevis.ARemplacer(variante, [ancien, D(EtatsDevis.Brouillon)], projetLance: true));
        Assert.Empty(ValidationDevis.ARemplacer(variante, [ancien], projetLance: false));
    }
}
