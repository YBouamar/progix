using Progix.Modules.Devis.Domain;
using Xunit;

namespace Progix.Tests.Devis;

public class RetraitDalleTests
{
    private static readonly List<TrancheForfait> Grille = [new() { APartirDe = 0, Prix = 100 }];

    private static LigneDevis Ligne(Guid dalle, int ordre, decimal surface) =>
        new() { DalleId = dalle, Ordre = ordre, Surface = surface, RatioActif = 2m, Systemes = [new SystemeLigne { Torons = 4, Ancrages = 10, Gaine = 20 }] };

    [Theory]
    [InlineData(EtatsDevis.Valide, true)]
    [InlineData(EtatsDevis.Remplace, true)]
    [InlineData(EtatsDevis.Brouillon, false)]
    [InlineData(EtatsDevis.Envoye, false)]
    [InlineData(EtatsDevis.Expire, false)]
    [InlineData(EtatsDevis.Annule, false)]
    public void Seul_un_devis_valide_ou_remplace_bloque(string etat, bool bloque) => Assert.Equal(bloque, RetraitDalle.Bloque(etat));

    [Theory]
    [InlineData(EtatsDevis.Brouillon, true)]
    [InlineData(EtatsDevis.Envoye, true)]
    [InlineData(EtatsDevis.Expire, true)]
    [InlineData(EtatsDevis.Annule, false)]
    public void Brouillon_envoye_et_expire_perdent_la_dalle(string etat, bool modifiable) => Assert.Equal(modifiable, RetraitDalle.Modifiable(etat));

    [Fact]
    public void Retirer_une_dalle_recalcule_les_totaux_et_renumerote()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var d = new DevisEntete { Etat = EtatsDevis.Brouillon, Lignes = [Ligne(a, 1, 100), Ligne(b, 2, 200)] };
        Calcul.Recalculer(d, Grille, false);
        var avant = d.TotalHT;

        Assert.True(RetraitDalle.Retirer(d, a, Grille, false, null));

        var reste = Assert.Single(d.Lignes);
        Assert.Equal(b, reste.DalleId);
        Assert.Equal(1, reste.Ordre);
        Assert.Equal(200m, d.SurfaceTotale);
        Assert.True(d.TotalHT < avant);
        Assert.Equal(d.TotalHT, reste.MontantHT);
    }

    [Fact]
    public void Retirer_une_dalle_absente_ne_change_rien()
    {
        var d = new DevisEntete { Etat = EtatsDevis.Envoye, Lignes = [Ligne(Guid.NewGuid(), 1, 100)] };
        Assert.False(RetraitDalle.Retirer(d, Guid.NewGuid(), Grille, false, null));
        Assert.Equal(EtatsDevis.Envoye, d.Etat);
    }

    [Fact]
    public void Un_devis_envoye_redevient_brouillon_sans_signature_ni_relecture()
    {
        var a = Guid.NewGuid();
        var d = new DevisEntete
        {
            Etat = EtatsDevis.Envoye, EnvoyeLe = DateTime.UtcNow, SignePar = "directeur", SigneLe = DateTime.UtcNow, Jeton = "abc",
            RevueEtat = "validee", RevuePar = "chef", Lignes = [Ligne(a, 1, 100), Ligne(Guid.NewGuid(), 2, 50)],
        };

        Assert.True(RetraitDalle.Retirer(d, a, Grille, false, null));

        Assert.Equal(EtatsDevis.Brouillon, d.Etat);
        Assert.Null(d.EnvoyeLe); Assert.Null(d.SignePar); Assert.Null(d.SigneLe); Assert.Null(d.Jeton);
        Assert.Null(d.RevueEtat); Assert.Null(d.RevuePar);
    }
}
