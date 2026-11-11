using Progix.Modules.Projets.Domain;
using Progix.SharedKernel;

namespace Progix.Tests.Projets;

/// <summary>Étude d'une dalle : état, affectation, clôture et dépôt des pièces.</summary>
public class EtudeDalleTests
{
    [Theory]
    [InlineData(false, 0, EtudeDalle.AAffecter)]
    [InlineData(false, 1, EtudeDalle.EnCours)]
    [InlineData(false, 3, EtudeDalle.EnCours)]
    [InlineData(true, 2, EtudeDalle.Terminee)]
    [InlineData(true, 0, EtudeDalle.Terminee)]
    public void Etat_selon_les_ingenieurs_et_la_cloture(bool terminee, int ingenieurs, string attendu) =>
        Assert.Equal(attendu, EtudeDalle.Etat(terminee, ingenieurs));

    [Fact]
    public void Seul_un_ingenieur_affecte_termine_l_etude()
    {
        Assert.Null(EtudeDalle.RefusTerminer(terminee: false, affecte: true));
        Assert.NotNull(EtudeDalle.RefusTerminer(terminee: false, affecte: false));
        Assert.NotNull(EtudeDalle.RefusTerminer(terminee: true, affecte: true));
    }

    [Fact]
    public void Les_ingenieurs_ne_changent_plus_une_fois_l_etude_terminee()
    {
        Assert.Null(EtudeDalle.RefusAffecter(terminee: false));
        Assert.NotNull(EtudeDalle.RefusAffecter(terminee: true));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void Depot_des_pieces_par_les_responsables_et_les_ingenieurs_affectes(bool responsable, bool affecte, bool permis) =>
        Assert.Equal(permis, EtudeDalle.RefusDeposer(responsable, affecte) is null);

    [Fact]
    public void L_affectation_revient_a_l_ingenieur_en_chef_au_chef_de_projet_et_au_directeur()
    {
        Assert.Equal([Roles.Directeur, Roles.ChefProjet, Roles.IngenieurChef], EtudeDalle.RolesAffectation);
        Assert.Contains(Roles.Ingenieur, EtudeDalle.RolesVue);
        Assert.DoesNotContain(Roles.Dessinateur, EtudeDalle.RolesVue);
        Assert.Equal([Roles.Ingenieur, Roles.IngenieurChef], EtudeDalle.RolesIngenieur);
    }

    [Fact]
    public void Quatre_pieces_dans_l_ordre_du_travail() =>
        Assert.Equal(["coffrage", "fond_plan", "adapt", "principe"], EtudeDalle.Pieces.Select(p => p.Code));
}
