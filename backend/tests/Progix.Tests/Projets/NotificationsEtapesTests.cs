using Progix.Modules.Projets.Domain;
using Progix.SharedKernel;

namespace Progix.Tests.Projets;

/// <summary>Notifications de changement d'étape : chacun est prévenu quand c'est son tour, pas autrement.</summary>
public class NotificationsEtapesTests
{
    private static readonly DateTime T = new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);
    private static Func<string, bool> Role(params string[] roles) => roles.Contains;
    private static List<NotificationsEtapes.Avis> Avis(int n, EtapeDalle? e, string role, bool rouverte = false) =>
        NotificationsEtapes.Pour(n, e, (T, "Autre personne"), rouverte, Role(role)).ToList();

    [Fact]
    public void La_fonction_qui_realise_l_etape_est_prevenue_quand_elle_devient_l_etape_en_cours()
    {
        var a = Assert.Single(Avis(Etapes.CommandeUsine, null, Roles.ResponsableLogistique));
        Assert.Equal(NotificationsEtapes.ATour, a.Genre);
        Assert.Equal(T, a.Le);
        Assert.Empty(Avis(Etapes.CommandeUsine, null, Roles.ChefChantier));
        Assert.Single(Avis(3, null, Roles.ChefChantier));
        Assert.Single(Avis(2, null, Roles.Ingenieur));
        Assert.Single(Avis(1, null, Roles.Dessinateur));
    }

    [Fact]
    public void Directeur_et_chef_de_projet_ne_recoivent_que_les_validations()
    {
        Assert.Empty(Avis(3, null, Roles.Directeur));
        Assert.Empty(Avis(3, null, Roles.ChefProjet));
        // Commande usine pas encore saisie : rien à valider.
        Assert.Empty(Avis(Etapes.CommandeUsine, null, Roles.ChefProjet));
    }

    [Fact]
    public void Commande_usine_saisie_attend_ses_validations()
    {
        var e = new EtapeDalle { Numero = Etapes.CommandeUsine, FaitLe = T.AddHours(2), FaitPar = "Logisticien" };
        var a = Assert.Single(Avis(Etapes.CommandeUsine, e, Roles.ChefProjet));
        Assert.Equal(NotificationsEtapes.AValider, a.Genre);
        Assert.Equal(T.AddHours(2), a.Le);
        Assert.Contains("technique", a.Texte);
        Assert.Contains("administrative", Assert.Single(Avis(Etapes.CommandeUsine, e, Roles.ResponsableAdministratif)).Texte);
        // Le directeur peut donner les deux.
        Assert.Contains("technique et administrative", Assert.Single(Avis(Etapes.CommandeUsine, e, Roles.Directeur)).Texte);
        // Une fois la validation technique donnée, le chef de projet n'a plus rien ; la logistique non plus, sa saisie est faite.
        e.ValidationTechniqueLe = T.AddHours(3);
        Assert.Empty(Avis(Etapes.CommandeUsine, e, Roles.ChefProjet));
        Assert.Empty(Avis(Etapes.CommandeUsine, e, Roles.ResponsableLogistique));
    }

    [Fact]
    public void Mise_en_tension_validations_d_abord_puis_le_chantier()
    {
        // L'étape devient en cours : les trois validateurs sont prévenus, pas encore le chantier.
        Assert.Equal(NotificationsEtapes.AValider, Assert.Single(Avis(Etapes.MiseEnTension, null, Roles.ChefProjet)).Genre);
        Assert.Single(Avis(Etapes.MiseEnTension, null, Roles.ResponsableAdministratif));
        Assert.Empty(Avis(Etapes.MiseEnTension, null, Roles.ChefChantier));
        // Le responsable des chantiers valide lui aussi : prévenu pour sa validation seulement.
        var resp = Avis(Etapes.MiseEnTension, null, Roles.ResponsableChantier);
        Assert.Equal(NotificationsEtapes.AValider, Assert.Single(resp).Genre);

        // Les trois validations données : le chantier est prévenu, à l'heure de la dernière.
        var e = new EtapeDalle
        {
            Numero = Etapes.MiseEnTension, ValidationTechniqueLe = T.AddHours(1), ValidationAdministrativeLe = T.AddHours(2),
            ValidationChantierLe = T.AddHours(3), ValidationChantierPar = "Responsable",
        };
        var chef = Assert.Single(Avis(Etapes.MiseEnTension, e, Roles.ChefChantier));
        Assert.Equal(NotificationsEtapes.ATour, chef.Genre);
        Assert.Equal(T.AddHours(3), chef.Le);
        Assert.Equal("Responsable", chef.Par);
        Assert.Empty(Avis(Etapes.MiseEnTension, e, Roles.ChefProjet));
    }

    [Fact]
    public void Etape_rouverte_et_etape_deja_faite()
    {
        Assert.Contains("rouverte", Assert.Single(Avis(5, null, Roles.ChefChantier, rouverte: true)).Texte);
        Assert.Empty(Avis(5, new EtapeDalle { Numero = 5, FaitLe = T }, Roles.ChefChantier));
    }

    [Fact]
    public void Fonctions_limitees_a_leurs_affectations()
    {
        Assert.Equal([Roles.Ingenieur, Roles.Dessinateur, Roles.ChefChantier], NotificationsEtapes.RolesAffectes);
    }
}
