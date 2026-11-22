using Progix.Modules.Devis.Domain;
using Xunit;

namespace Progix.Tests.Devis;

/// <summary>Un seul devis validé par projet ; les autres devis ouverts du projet sont annulés à la validation.</summary>
public class ValidationDevisTests
{
    private static readonly Guid Projet = Guid.NewGuid();
    private static DevisEntete D(string etat, Guid? projet = null, string numero = "DEV-00001-2026") =>
        new() { Id = Guid.NewGuid(), ProjetId = projet ?? Projet, Etat = etat, Numero = numero, Version = 1 };

    [Fact]
    public void Brouillons_et_devis_envoyes_du_projet_sont_annules()
    {
        var valide = D(EtatsDevis.Envoye);
        var autres = new[] { D(EtatsDevis.Brouillon), D(EtatsDevis.Envoye), D(EtatsDevis.Expire), D(EtatsDevis.Remplace), D(EtatsDevis.Annule), D(EtatsDevis.Brouillon, Guid.NewGuid()) };
        var annules = ValidationDevis.AAnnuler(valide, autres);
        Assert.Equal(2, annules.Count);
        Assert.All(annules, a => Assert.True(a.Etat is EtatsDevis.Brouillon or EtatsDevis.Envoye && a.ProjetId == Projet));
    }

    [Fact]
    public void Le_devis_valide_lui_meme_n_est_jamais_annule()
    {
        var valide = D(EtatsDevis.Envoye);
        Assert.Empty(ValidationDevis.AAnnuler(valide, [valide]));
    }

    [Fact]
    public void Validation_refusee_si_un_autre_devis_du_projet_est_deja_valide()
    {
        var deja = D(EtatsDevis.Valide, numero: "DEV-00007-2026");
        var refus = ValidationDevis.Refus(D(EtatsDevis.Envoye), [deja]);
        Assert.NotNull(refus);
        Assert.Contains("DEV-00007-2026", refus);
    }

    [Fact]
    public void Validation_permise_sans_autre_devis_valide_ou_si_le_valide_est_d_un_autre_projet()
    {
        Assert.Null(ValidationDevis.Refus(D(EtatsDevis.Envoye), [D(EtatsDevis.Brouillon), D(EtatsDevis.Annule)]));
        Assert.Null(ValidationDevis.Refus(D(EtatsDevis.Envoye), [D(EtatsDevis.Valide, Guid.NewGuid())]));
    }

    [Fact]
    public void Motif_cite_le_devis_valide() =>
        Assert.Contains("DEV-00003-2026", ValidationDevis.MotifAnnulation(D(EtatsDevis.Valide, numero: "DEV-00003-2026")));
}
