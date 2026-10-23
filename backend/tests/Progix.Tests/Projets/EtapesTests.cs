using Progix.Modules.Projets.Domain;
using Progix.SharedKernel;

namespace Progix.Tests.Projets;

/// <summary>Les 11 étapes du suivi de chantier et leurs validations (Projets/Domain/Etape.cs).</summary>
public class EtapesTests
{
    [Fact]
    public void Onze_etapes_numerotees_dans_l_ordre()
    {
        Assert.Equal(Etapes.Total, Etapes.All.Count);
        Assert.Equal(Enumerable.Range(1, 11), Etapes.All.Select(e => e.Numero));
    }

    [Fact]
    public void Etapes_a_validations()
    {
        Assert.Equal([Etapes.CommandeUsine, Etapes.MiseEnTension, Etapes.Injection], Etapes.All.Where(e => e.DoubleValidation).Select(e => e.Numero));
        // Commande usine : validations après la saisie, sans validation chantier.
        Assert.False(Etapes.Get(Etapes.CommandeUsine).ValidationAvant);
        Assert.False(Etapes.Get(Etapes.CommandeUsine).ValidationChantier);
        // Mise en tension et injection : trois validations avant la saisie.
        Assert.All(new[] { Etapes.MiseEnTension, Etapes.Injection }, n =>
        {
            Assert.True(Etapes.Get(n).ValidationAvant);
            Assert.True(Etapes.Get(n).ValidationChantier);
        });
    }

    [Fact]
    public void Roles_par_etape()
    {
        Assert.Equal([Roles.ResponsableLogistique], Etapes.Get(Etapes.CommandeUsine).Roles);
        Assert.Contains(Roles.Dessinateur, Etapes.Get(1).Roles);
        Assert.Contains(Roles.ResponsableAdministratif, Etapes.Get(11).Roles);
    }

    private static EtapeDalle Etape(int n, bool fait = true, bool technique = false, bool administrative = false, bool chantier = false)
    {
        var maintenant = DateTime.UtcNow;
        return new EtapeDalle
        {
            Numero = n, FaitLe = fait ? maintenant : null,
            ValidationTechniqueLe = technique ? maintenant : null, ValidationAdministrativeLe = administrative ? maintenant : null,
            ValidationChantierLe = chantier ? maintenant : null,
        };
    }

    [Fact]
    public void Etape_simple_terminee_des_qu_elle_est_faite() => Assert.True(Etape(3).Terminee);

    [Fact]
    public void Commande_usine_terminee_apres_les_deux_validations()
    {
        Assert.False(Etape(Etapes.CommandeUsine).Terminee);
        Assert.False(Etape(Etapes.CommandeUsine, technique: true).Terminee);
        Assert.True(Etape(Etapes.CommandeUsine, technique: true, administrative: true).Terminee);
    }

    [Theory]
    [InlineData(Etapes.MiseEnTension)]
    [InlineData(Etapes.Injection)]
    public void Tension_et_injection_exigent_la_validation_chantier(int n)
    {
        Assert.False(Etape(n, technique: true, administrative: true).Validee);
        Assert.True(Etape(n, technique: true, administrative: true, chantier: true).Validee);
        Assert.False(Etape(n, fait: false, technique: true, administrative: true, chantier: true).Terminee);
    }
}
