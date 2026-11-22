using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Endpoints;
using Progix.SharedKernel;
using System.Security.Claims;

namespace Progix.Tests.Projets;

/// <summary>Commandes usine multiples d'une dalle : cycle de vie, modifications tracées, droits.</summary>
public class CommandesTests
{
    private static LigneCommande Ligne(string n, int torons, string sens, decimal longueur, int ancrages = 2) =>
        new() { Numero = n, Torons = torons, Sens = sens, Prefixe = "", Nombre = 1, Longueur = longueur, AncragesSaisis = ancrages };

    private static CommandeUsine Commande() => new()
    {
        DalleId = Guid.NewGuid(), Surface = 100,
        Lignes = [Ligne("01", 1, "X", 10m), Ligne("02", 2, "Y", 7m)],
        Accessoires = [new() { Libelle = "SCOTCHE", Quantite = 5 }],
    };

    private static TendonInput T(string n, int torons, string sens, decimal l, int a = 2) => new(n, torons, sens, l, a);

    private static ClaimsPrincipal Role(string role) => new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));

    [Fact]
    public void Etats_de_la_commande_initiale_et_des_complements()
    {
        var c = Commande();
        Assert.Equal(CommandeUsine.Attente, c.Etat(false));
        Assert.Equal(CommandeUsine.APreparer, c.Etat(true));
        c.FabricationLe = DateTime.UtcNow; Assert.Equal(CommandeUsine.Preparee, c.Etat(true));
        c.LivreeLe = DateTime.UtcNow; Assert.Equal(CommandeUsine.Envoyee, c.Etat(true));
        Assert.True(c.Consommee);
        c.RecueLe = DateTime.UtcNow; Assert.Equal(CommandeUsine.Recue, c.Etat(true));
        c.AnnuleeLe = DateTime.UtcNow; Assert.Equal(CommandeUsine.Annulee_, c.Etat(true));
        Assert.False(c.Consommee);   // annulée : le stock n'est plus consommé
        // Un complément n'attend pas les validations de l'étape.
        Assert.Equal(CommandeUsine.APreparer, new CommandeUsine { Numero = 2 }.Etat(false));
    }

    [Fact]
    public void Une_modification_trace_chaque_changement_et_garde_les_valeurs_d_avant()
    {
        var c = Commande();
        var maintenant = DateTime.UtcNow;
        var changements = CommandesEndpoints.Appliquer(c,
            [T("01", 1, "X", 10.5m), T("03", 4, "X", 8m)],            // 01 allongé, 02 retiré, 03 ajouté
            [new() { Libelle = "SCOTCHE", Quantite = 8 }], 110, maintenant);
        Assert.Contains(changements, x => x.Contains("Tendon 01") && x.Contains("longueur 10,00 m → 10,50 m"));
        Assert.Contains(changements, x => x.Contains("Tendon 02 retiré"));
        Assert.Contains(changements, x => x.Contains("Tendon 03 ajouté"));
        Assert.Contains(changements, x => x.Contains("SCOTCHE") && x.Contains("5 → 8"));
        Assert.Contains(changements, x => x.StartsWith("Surface"));
        var l1 = c.Lignes.Single(l => l.Numero == "01");
        Assert.Equal("S1 · X · 10,00 m · 2 ancrages", l1.Avant);
        Assert.NotNull(l1.ModifieLe); Assert.False(l1.Ajoutee);
        Assert.True(c.Lignes.Single(l => l.Numero == "03").Ajoutee);
        Assert.Equal(2, c.NbCables);
    }

    [Fact]
    public void Sans_changement_rien_n_est_trace()
    {
        var c = Commande();
        var changements = CommandesEndpoints.Appliquer(c, [T("01", 1, "X", 10m), T("02", 2, "Y", 7m)], [new() { Libelle = "SCOTCHE", Quantite = 5 }], 100, DateTime.UtcNow);
        Assert.Empty(changements);
        Assert.All(c.Lignes, l => Assert.Null(l.ModifieLe));
    }

    [Fact]
    public void Une_seconde_modification_garde_l_avant_d_origine()
    {
        var c = Commande();
        CommandesEndpoints.Appliquer(c, [T("01", 1, "X", 11m), T("02", 2, "Y", 7m)], [.. c.Accessoires], 100, DateTime.UtcNow);
        CommandesEndpoints.Appliquer(c, [T("01", 1, "X", 12m), T("02", 2, "Y", 7m)], [.. c.Accessoires], 100, DateTime.UtcNow);
        Assert.Equal("S1 · X · 10,00 m · 2 ancrages", c.Lignes.Single(l => l.Numero == "01").Avant);
        Assert.Equal(12m, c.Lignes.Single(l => l.Numero == "01").Longueur);
    }

    [Theory]
    [InlineData(Roles.GestionnaireStock, true, true, false)]
    [InlineData(Roles.ResponsableLogistique, true, true, true)]
    [InlineData(Roles.ChefChantier, false, true, false)]
    [InlineData(Roles.ResponsableChantier, false, true, true)]
    [InlineData(Roles.ChefProjet, true, true, true)]
    [InlineData(Roles.Directeur, true, true, true)]
    [InlineData(Roles.Dessinateur, false, false, false)]
    public void Droits_de_preparation_de_reception_et_d_annulation(string role, bool prepare, bool recoit, bool annule)
    {
        var u = Role(role);
        Assert.Equal(prepare, CommandesEndpoints.PeutPreparer(u));
        Assert.Equal(recoit, CommandesEndpoints.PeutRecevoir(u));
        Assert.Equal(annule, CommandesEndpoints.PeutAnnuler(u));
    }

    [Fact]
    public void Le_gestionnaire_de_stock_n_a_que_la_logistique()
    {
        Assert.Contains(Roles.GestionnaireStock, Policies.RolesLogistique);
        Assert.Equal(Habilitations.Ecriture, Habilitations.ParDefaut(Roles.GestionnaireStock, Habilitations.Logistique));
        Assert.Equal(Habilitations.Aucun, Habilitations.ParDefaut(Roles.GestionnaireStock, Habilitations.Projets));
        Assert.Equal(Habilitations.Aucun, Habilitations.ParDefaut(Roles.GestionnaireStock, Habilitations.Depenses));
        Assert.Equal(Habilitations.Aucun, Habilitations.ParDefaut(Roles.GestionnaireStock, Habilitations.Facturation));
    }

    [Fact]
    public void Le_menu_chantier_peut_declarer_la_reception_d_une_commande()
    {
        var (module, projet) = Habilitations.Alternative($"/api/projets/{Guid.NewGuid()}/dalles/{Guid.NewGuid()}/commandes/{Guid.NewGuid()}/etat")!.Value;
        Assert.Equal(Habilitations.Chantier, module);
        Assert.NotEqual(Guid.Empty, projet);
    }
}
