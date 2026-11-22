using Progix.SharedKernel;

namespace Progix.Tests.Negoce;

/// <summary>Grille des droits du négoce validée le 22/11/2026 : profils, prix d'achat, routes.</summary>
public class NegoceHabilitationsTests
{
    private static int Niveau(string role, string module) => Habilitations.ParDefaut(role, module);

    private static readonly string[] ModulesNegoce =
    [
        Habilitations.NegoceArticles, Habilitations.NegocePrixAchat, Habilitations.NegoceGroupes, Habilitations.NegoceDevis, Habilitations.NegoceLivraisons, Habilitations.NegoceFactures,
        Habilitations.NegoceFournisseurs, Habilitations.NegoceAchats, Habilitations.NegoceReceptions, Habilitations.NegocePaiementsFournisseurs, Habilitations.NegoceStock,
    ];

    [Fact]
    public void Les_modules_du_negoce_sont_groupes_et_accordables() =>
        Assert.All(ModulesNegoce, c =>
        {
            var m = Habilitations.Trouver(c)!;
            Assert.Equal(Habilitations.GroupeNegoce, m.Groupe);
            Assert.True(m.Accordable);
        });

    [Theory]
    [InlineData(Roles.ResponsableAchats)]
    [InlineData(Roles.ResponsableVentesDirectes)]
    public void Les_fonctions_du_negoce_n_ont_aucun_module_de_post_tension(string role)
    {
        var interdits = new[] { Habilitations.Projets, Habilitations.Devis, Habilitations.Livraisons, Habilitations.Facturation, Habilitations.Tresorerie, Habilitations.Dalles,
            Habilitations.Contrats, Habilitations.Courriers, Habilitations.Paiements, Habilitations.Depenses, Habilitations.Etude, Habilitations.Execution, Habilitations.Chantier, Habilitations.Logistique };
        Assert.All(interdits, m => Assert.Equal(Habilitations.Aucun, Niveau(role, m)));
    }

    [Fact]
    public void Le_gestionnaire_de_stock_et_les_fonctions_du_negoce_restent_hors_de_la_post_tension()
    {
        Assert.DoesNotContain(Roles.ResponsableAchats, Policies.RolesDevis);
        Assert.DoesNotContain(Roles.ResponsableVentesDirectes, Policies.RolesDevis);
        Assert.False(Roles.Find(Roles.ResponsableAchats)!.VoitMontants);
        Assert.False(Roles.Find(Roles.ResponsableVentesDirectes)!.VoitMontants);
    }

    [Theory]
    [InlineData(Roles.ResponsableAchats, Habilitations.Ecriture)]
    [InlineData(Roles.ResponsableAdministratif, Habilitations.Lecture)]
    [InlineData(Roles.ChefProjet, Habilitations.Lecture)]
    [InlineData(Roles.Directeur, Habilitations.Ecriture)]
    [InlineData(Roles.ResponsableVentesDirectes, Habilitations.Aucun)]
    [InlineData(Roles.GestionnaireStock, Habilitations.Aucun)]
    [InlineData(Roles.Administrateur, Habilitations.Aucun)]
    [InlineData(Roles.AgentCommercial, Habilitations.Aucun)]
    public void Le_prix_d_achat_n_est_visible_que_de_quatre_fonctions(string role, int attendu) =>
        Assert.Equal(attendu, Niveau(role, Habilitations.NegocePrixAchat));

    [Fact]
    public void Le_responsable_de_ventes_directes_vend_sans_acheter()
    {
        Assert.Equal(Habilitations.Ecriture, Niveau(Roles.ResponsableVentesDirectes, Habilitations.NegoceDevis));
        Assert.Equal(Habilitations.Ecriture, Niveau(Roles.ResponsableVentesDirectes, Habilitations.NegoceFactures));
        Assert.Equal(Habilitations.Ecriture, Niveau(Roles.ResponsableVentesDirectes, Habilitations.NegoceGroupes));
        Assert.Equal(Habilitations.Aucun, Niveau(Roles.ResponsableVentesDirectes, Habilitations.NegoceAchats));
        Assert.Equal(Habilitations.Aucun, Niveau(Roles.ResponsableVentesDirectes, Habilitations.NegocePaiementsFournisseurs));
        Assert.Equal(Habilitations.Lecture, Niveau(Roles.ResponsableVentesDirectes, Habilitations.NegoceStock));
    }

    [Fact]
    public void Le_responsable_d_achats_achete_et_paie_sans_vendre()
    {
        Assert.Equal(Habilitations.Ecriture, Niveau(Roles.ResponsableAchats, Habilitations.NegoceFournisseurs));
        Assert.Equal(Habilitations.Ecriture, Niveau(Roles.ResponsableAchats, Habilitations.NegoceAchats));
        Assert.Equal(Habilitations.Ecriture, Niveau(Roles.ResponsableAchats, Habilitations.NegocePaiementsFournisseurs));
        Assert.Equal(Habilitations.Aucun, Niveau(Roles.ResponsableAchats, Habilitations.NegoceDevis));
        Assert.Equal(Habilitations.Aucun, Niveau(Roles.ResponsableAchats, Habilitations.NegoceFactures));
        Assert.Equal(Habilitations.Aucun, Niveau(Roles.ResponsableAchats, Habilitations.NegoceGroupes));
    }

    [Fact]
    public void Le_gestionnaire_de_stock_gere_le_stock_recoit_et_livre_sans_voir_les_paiements()
    {
        Assert.Equal(Habilitations.Ecriture, Niveau(Roles.GestionnaireStock, Habilitations.NegoceStock));
        Assert.Equal(Habilitations.Ecriture, Niveau(Roles.GestionnaireStock, Habilitations.NegoceReceptions));
        Assert.Equal(Habilitations.Ecriture, Niveau(Roles.GestionnaireStock, Habilitations.NegoceLivraisons));
        Assert.Equal(Habilitations.Lecture, Niveau(Roles.GestionnaireStock, Habilitations.NegoceAchats));
        Assert.Equal(Habilitations.Aucun, Niveau(Roles.GestionnaireStock, Habilitations.NegocePaiementsFournisseurs));
        Assert.Equal(Habilitations.Aucun, Niveau(Roles.GestionnaireStock, Habilitations.NegoceFactures));
    }

    [Theory]
    [InlineData(Roles.ResponsableAdministratif)]
    [InlineData(Roles.Administrateur)]
    public void Le_responsable_administratif_et_l_administrateur_font_aussi_les_ventes_directes(string role)
    {
        Assert.Equal(Habilitations.Ecriture, Niveau(role, Habilitations.NegoceDevis));
        Assert.Equal(Habilitations.Ecriture, Niveau(role, Habilitations.NegoceLivraisons));
        Assert.Equal(Habilitations.Ecriture, Niveau(role, Habilitations.NegoceFactures));
        Assert.Equal(Habilitations.Ecriture, Niveau(role, Habilitations.NegoceGroupes));
    }

    [Fact]
    public void Seul_le_responsable_administratif_paie_les_fournisseurs_parmi_les_fonctions_de_gestion()
    {
        Assert.Equal(Habilitations.Ecriture, Niveau(Roles.ResponsableAdministratif, Habilitations.NegocePaiementsFournisseurs));
        Assert.Equal(Habilitations.Aucun, Niveau(Roles.Administrateur, Habilitations.NegocePaiementsFournisseurs));
    }

    [Theory]
    [InlineData("GET", "/api/negoce/articles", Habilitations.NegoceArticles, Habilitations.Lecture)]
    [InlineData("POST", "/api/negoce/articles", Habilitations.NegoceArticles, Habilitations.Ecriture)]
    [InlineData("GET", "/api/negoce/articles/prix", Habilitations.NegoceArticles, Habilitations.Lecture)]
    [InlineData("PUT", "/api/negoce/prix-achat", Habilitations.NegocePrixAchat, Habilitations.Ecriture)]
    [InlineData("GET", "/api/negoce/groupes/00000000-0000-0000-0000-000000000001/membres", Habilitations.NegoceGroupes, Habilitations.Lecture)]
    [InlineData("POST", "/api/negoce/devis/00000000-0000-0000-0000-000000000001/dupliquer", Habilitations.NegoceDevis, Habilitations.Ecriture)]
    [InlineData("POST", "/api/negoce/commandes", Habilitations.NegoceDevis, Habilitations.Ecriture)]
    [InlineData("POST", "/api/negoce/livraisons", Habilitations.NegoceLivraisons, Habilitations.Ecriture)]
    [InlineData("POST", "/api/negoce/factures", Habilitations.NegoceFactures, Habilitations.Ecriture)]
    [InlineData("POST", "/api/negoce/avoirs", Habilitations.NegoceFactures, Habilitations.Ecriture)]
    [InlineData("POST", "/api/negoce/encaissements", Habilitations.NegoceFactures, Habilitations.Ecriture)]
    [InlineData("GET", "/api/negoce/fournisseurs", Habilitations.NegoceFournisseurs, Habilitations.Lecture)]
    [InlineData("POST", "/api/negoce/achats", Habilitations.NegoceAchats, Habilitations.Ecriture)]
    [InlineData("POST", "/api/negoce/achats/00000000-0000-0000-0000-000000000001/receptions", Habilitations.NegoceReceptions, Habilitations.Ecriture)]
    [InlineData("POST", "/api/negoce/achats/00000000-0000-0000-0000-000000000001/paiements", Habilitations.NegocePaiementsFournisseurs, Habilitations.Ecriture)]
    [InlineData("GET", "/api/negoce/achats/00000000-0000-0000-0000-000000000001/factures", Habilitations.NegocePaiementsFournisseurs, Habilitations.Lecture)]
    [InlineData("GET", "/api/negoce/achats/00000000-0000-0000-0000-000000000001", Habilitations.NegoceAchats, Habilitations.Lecture)]
    [InlineData("GET", "/api/negoce/stock", Habilitations.NegoceStock, Habilitations.Lecture)]
    [InlineData("POST", "/api/negoce/mouvements", Habilitations.NegoceStock, Habilitations.Ecriture)]
    [InlineData("GET", "/api/negoce/inconnu", Habilitations.NegoceArticles, Habilitations.Lecture)]
    public void Chaque_route_du_negoce_depend_de_son_module(string methode, string chemin, string module, int requis) =>
        Assert.Equal((module, requis), Habilitations.Resoudre(methode, chemin));

    [Fact]
    public void Les_routes_de_la_post_tension_ne_changent_pas()
    {
        Assert.Equal((Habilitations.Facturation, Habilitations.Lecture), Habilitations.Resoudre("GET", "/api/factures"));
        Assert.Equal((Habilitations.Livraisons, Habilitations.Ecriture), Habilitations.Resoudre("POST", "/api/livraisons"));
        Assert.Equal((Habilitations.Devis, Habilitations.Lecture), Habilitations.Resoudre("GET", "/api/devis"));
    }
}
