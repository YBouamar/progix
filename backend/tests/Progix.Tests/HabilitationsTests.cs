using Progix.SharedKernel;

namespace Progix.Tests;

/// <summary>Habilitations : niveaux d'origine, plafonds, niveau effectif et module d'une requête.</summary>
public class HabilitationsTests
{
    /// <summary>
    /// Menus de chaque fonction avant les habilitations : les niveaux d'origine doivent ouvrir exactement les mêmes modules,
    /// plus le menu Exécution ajouté depuis pour les dessinateurs, qui remplace la répartition des dessinateurs.
    /// </summary>
    public static readonly TheoryData<string, string[]> MenusDorigine = new()
    {
        { Roles.ResponsableAdministratif, [Habilitations.Projets, Habilitations.Devis, Habilitations.Clients, Habilitations.Livraisons, Habilitations.Facturation, Habilitations.Tresorerie] },
        { Roles.Administrateur, [Habilitations.Projets, Habilitations.Devis, Habilitations.Clients, Habilitations.Livraisons, Habilitations.Facturation, Habilitations.Tresorerie] },
        { Roles.AgentCommercial, [Habilitations.Projets] },
        { Roles.IngenieurChef, [Habilitations.Projets, Habilitations.Devis, Habilitations.Etude, Habilitations.RepartitionIngenieurs, Habilitations.Performance] },
        { Roles.Ingenieur, [Habilitations.Projets, Habilitations.Devis, Habilitations.Etude] },
        { Roles.DessinateurChef, [Habilitations.Projets, Habilitations.Execution, Habilitations.Performance] },
        { Roles.Dessinateur, [Habilitations.Projets, Habilitations.Execution] },
        { Roles.ResponsableLogistique, [Habilitations.Projets, Habilitations.Logistique, Habilitations.Parametrage] },
        { Roles.ResponsableChantier, [Habilitations.Projets] },
        { Roles.ChefChantier, [Habilitations.Projets] },
    };

    [Theory]
    [MemberData(nameof(MenusDorigine))]
    public void Les_niveaux_d_origine_ouvrent_les_memes_modules_qu_avant(string role, string[] modules) =>
        Assert.Equal(modules.Order(), Habilitations.Modules.Where(m => Habilitations.ParDefaut(role, m.Code) > Habilitations.Aucun).Select(m => m.Code).Order());

    [Theory]
    [InlineData(Roles.Directeur)]
    [InlineData(Roles.ChefProjet)]
    public void La_direction_a_tous_les_modules_a_leur_niveau_le_plus_haut(string role) =>
        Assert.All(Habilitations.Modules, m => Assert.Equal(m.Maximum, Habilitations.ParDefaut(role, m.Code)));

    [Fact]
    public void Les_modules_de_consultation_s_arretent_a_la_lecture()
    {
        Assert.Equal(Habilitations.Lecture, Habilitations.ParDefaut(Roles.Administrateur, Habilitations.Tresorerie));
        Assert.Equal(Habilitations.Lecture, Habilitations.Trouver(Habilitations.Performance)!.Maximum);
        Assert.Equal(Habilitations.Ecriture, Habilitations.ParDefaut(Roles.Administrateur, Habilitations.Facturation));
    }

    [Fact]
    public void Un_module_accordable_s_ouvre_a_toute_fonction_un_module_lie_aux_fonctions_d_origine_ne_fait_que_se_restreindre()
    {
        // Devis : accordable, un dessinateur peut le recevoir en écriture.
        Assert.Equal(Habilitations.Ecriture, Habilitations.Plafond(Roles.Dessinateur, Habilitations.Devis));
        Assert.Equal(Habilitations.Ecriture, Habilitations.Niveau(Roles.Dessinateur, Habilitations.Devis, Habilitations.Ecriture));
        // Étude : liée aux fonctions d'origine, fermée pour un dessinateur quel que soit le réglage.
        Assert.Equal(Habilitations.Aucun, Habilitations.Plafond(Roles.Dessinateur, Habilitations.Etude));
        Assert.Equal(Habilitations.Aucun, Habilitations.Niveau(Roles.Dessinateur, Habilitations.Etude, Habilitations.Ecriture));
        // Un ingénieur peut y être ramené à la lecture ou à rien.
        Assert.Equal(Habilitations.Lecture, Habilitations.Niveau(Roles.Ingenieur, Habilitations.Etude, Habilitations.Lecture));
        Assert.Equal(Habilitations.Aucun, Habilitations.Niveau(Roles.Ingenieur, Habilitations.Etude, Habilitations.Aucun));
    }

    [Fact]
    public void Sans_reglage_la_fonction_garde_le_niveau_de_sa_reference_et_le_directeur_garde_tout()
    {
        Assert.Equal(Habilitations.Ecriture, Habilitations.Niveau(Roles.Ingenieur, Habilitations.Devis, null));
        Assert.Equal(Habilitations.Aucun, Habilitations.Niveau(Roles.Ingenieur, Habilitations.Facturation, null));
        Assert.All(Habilitations.Modules, m => Assert.Equal(m.Maximum, Habilitations.Niveau(Roles.Directeur, m.Code, Habilitations.Aucun)));
    }

    [Theory]
    [InlineData("GET", "/api/devis", Habilitations.Devis, Habilitations.Lecture)]
    [InlineData("POST", "/api/devis/3f2a/valider", Habilitations.Devis, Habilitations.Ecriture)]
    [InlineData("GET", "/api/partenaires/compteurs", Habilitations.Clients, Habilitations.Lecture)]
    [InlineData("PUT", "/api/partenaires/3f2a", Habilitations.Clients, Habilitations.Ecriture)]
    [InlineData("GET", "/api/factures/3f2a/pdf", Habilitations.Facturation, Habilitations.Lecture)]
    [InlineData("POST", "/api/encaissements/3f2a/annuler", Habilitations.Facturation, Habilitations.Ecriture)]
    [InlineData("GET", "/api/tresorerie/export.xlsx", Habilitations.Tresorerie, Habilitations.Lecture)]
    [InlineData("POST", "/api/etudes/3f2a/fond-plan/deposer", Habilitations.Etude, Habilitations.Ecriture)]
    [InlineData("GET", "/api/execution/jours", Habilitations.Execution, Habilitations.Lecture)]
    [InlineData("POST", "/api/execution/dossiers/3f2a/terminer", Habilitations.Execution, Habilitations.Ecriture)]
    [InlineData("GET", "/api/repartition/ingenieurs", Habilitations.RepartitionIngenieurs, Habilitations.Lecture)]
    [InlineData("GET", "/api/statistiques/performance", Habilitations.Performance, Habilitations.Lecture)]
    [InlineData("PUT", "/api/parametres/niveaux", Habilitations.Niveaux, Habilitations.Ecriture)]
    [InlineData("PUT", "/api/parametres/tension", Habilitations.Parametrage, Habilitations.Ecriture)]
    [InlineData("GET", "/api/projets/3f2a/dalles", Habilitations.Projets, Habilitations.Lecture)]
    [InlineData("POST", "/api/projets/3f2a/dalles/9b1c/etapes/4/realiser", Habilitations.Projets, Habilitations.Ecriture)]
    public void Chaque_requete_releve_de_son_module(string methode, string chemin, string module, int requis) =>
        Assert.Equal((module, requis), Habilitations.Resoudre(methode, chemin));

    [Theory]
    [InlineData("GET", "/api/parametres/niveaux")]
    [InlineData("GET", "/api/parametres/tension")]
    [InlineData("GET", "/api/auth/me")]
    [InlineData("POST", "/api/fichiers/envois")]
    [InlineData("GET", "/api/tableau-de-bord/projets")]
    [InlineData("GET", "/api/statistiques/paiements")]
    [InlineData("POST", "/api/contrats/3f2a/signer")]
    [InlineData("GET", "/api/devisement")]
    public void Les_listes_de_parametres_et_les_familles_hors_module_ne_dependent_d_aucun_niveau(string methode, string chemin) =>
        Assert.Null(Habilitations.Resoudre(methode, chemin));

    [Fact]
    public void Le_refus_dit_s_il_manque_l_acces_ou_seulement_l_ecriture()
    {
        Assert.Contains("n'a pas accès", Habilitations.Refus(Habilitations.Devis, Habilitations.Aucun));
        Assert.Contains("lecture seule", Habilitations.Refus(Habilitations.Devis, Habilitations.Lecture));
    }
}
