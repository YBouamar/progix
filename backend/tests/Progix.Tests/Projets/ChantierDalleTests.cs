using Progix.Modules.Projets.Domain;

namespace Progix.Tests.Projets;

/// <summary>Menu Chantier : état d'une dalle vue du terrain, ce qui retient son étape, accès d'un chef de chantier.</summary>
public class ChantierDalleTests
{
    [Theory]
    [InlineData(0, 0, ChantierDalle.AAffecter)]
    [InlineData(5, 0, ChantierDalle.AAffecter)]
    [InlineData(0, 1, ChantierDalle.Preparation)]
    [InlineData(1, 1, ChantierDalle.Preparation)]
    [InlineData(2, 1, ChantierDalle.ARealiser)]
    [InlineData(3, 2, ChantierDalle.Commande)]
    [InlineData(4, 1, ChantierDalle.ARealiser)]
    [InlineData(10, 1, ChantierDalle.ARealiser)]
    [InlineData(11, 0, ChantierDalle.Terminee)]
    [InlineData(11, 1, ChantierDalle.Terminee)]
    public void L_etat_suit_les_etapes_faites_et_les_chefs_de_chantier_du_projet(int etapesFaites, int chefs, string attendu) =>
        Assert.Equal(attendu, ChantierDalle.Etat(etapesFaites, chefs));

    [Fact]
    public void Ce_qui_retient_l_etape_en_cours()
    {
        var (coffrage, commande, tension) = (Etapes.Get(Etapes.ReceptionCoffrage), Etapes.Get(Etapes.CommandeUsine), Etapes.Get(Etapes.MiseEnTension));
        Assert.Null(ChantierDalle.Attente(projetBloque: false, devisValide: true, coffrage, realisee: false, validee: false));
        Assert.Contains("bloqué", ChantierDalle.Attente(true, true, coffrage, false, false));
        Assert.Contains("devis", ChantierDalle.Attente(false, false, coffrage, false, false));
        // Mise en tension : les validations précèdent la saisie.
        Assert.Contains("avant la saisie", ChantierDalle.Attente(false, true, tension, false, false));
        Assert.Null(ChantierDalle.Attente(false, true, tension, false, true));
        // Commande usine : les validations suivent la réalisation.
        Assert.Null(ChantierDalle.Attente(false, true, commande, false, false));
        Assert.Contains("en attente de validation", ChantierDalle.Attente(false, true, commande, true, false));
    }

    [Fact]
    public void Un_chef_de_chantier_n_accede_qu_aux_projets_ou_il_est_affecte()
    {
        Assert.Null(ChantierDalle.RefusAcces(responsable: true, affecte: false));
        Assert.Null(ChantierDalle.RefusAcces(responsable: false, affecte: true));
        Assert.Contains("pas affecté", ChantierDalle.RefusAcces(responsable: false, affecte: false));
    }
}
