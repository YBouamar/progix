using Progix.Modules.Partenaires.Domain;
using Progix.Modules.Partenaires.Endpoints;

namespace Progix.Tests.Partenaires;

/// <summary>Lecture d'une ligne de la trame d'import des clients et partenaires.</summary>
public class ImportPartenairesTests
{
    private static Dictionary<string, string?> Ligne(params (string Colonne, string? Valeur)[] valeurs)
    {
        var l = ImportPartenaires.Colonnes.ToDictionary(c => c, _ => (string?)null);
        foreach (var (c, v) in valeurs) l[c] = v;
        return l;
    }

    [Fact]
    public void Personne_morale_avec_contact()
    {
        var (i, refus) = ImportPartenaires.VersInput(Ligne(("Type*", "Client final"), ("Catégorie*", "Personne morale"), ("Raison sociale ou nom*", "Exemple SARL"),
            ("ICE", "001 234 567 000 089"), ("Exonéré de TVA", "oui"), ("Contact", "Karim"), ("Fonction du contact", "Gérant")));
        Assert.Null(refus);
        Assert.Equal(TypesPartenaire.ClientFinal, i!.Type);
        Assert.Equal(Natures.Morale, i.Nature);
        Assert.Equal("Exemple SARL", i.RaisonSociale);
        Assert.Equal("001234567000089", i.Ice);
        Assert.True(i.ExonereTva);
        Assert.Equal("Gérant", Assert.Single(i.Contacts!).Fonction);
        Assert.Empty(PartenairesEndpoints.Valider(i));
    }

    [Fact]
    public void Personne_physique_nom_et_prenom()
    {
        var (i, refus) = ImportPartenaires.VersInput(Ligne(("Type*", "client_final"), ("Catégorie*", "physique"), ("Raison sociale ou nom*", "Bennani"), ("Prénom", "Youssef"), ("CIN", "ab123456")));
        Assert.Null(refus);
        Assert.Equal("Bennani", i!.NomFamille);
        Assert.Equal("Youssef", i.Prenom);
        Assert.Null(i.RaisonSociale);
        Assert.Equal("M.", i.Civilite);
        Assert.False(i.ExonereTva);
    }

    [Theory]
    [InlineData("BET", TypesPartenaire.Bet)]
    [InlineData("entreprise de construction", TypesPartenaire.EntrepriseConstruction)]
    [InlineData("Laboratoire", TypesPartenaire.Laboratoire)]
    public void Type_par_libelle_ou_code(string saisi, string attendu)
    {
        var (i, _) = ImportPartenaires.VersInput(Ligne(("Type*", saisi), ("Catégorie*", "Autre"), ("Raison sociale ou nom*", "X")));
        Assert.Equal(attendu, i!.Type);
    }

    [Theory]
    [InlineData("Fournisseur", "Personne morale", "Oui", "Type")]
    [InlineData("BET", "Société civile", "Oui", "Catégorie")]
    [InlineData("BET", "Autre", "peut-être", "Exonéré")]
    public void Valeur_inconnue_refusee(string type, string categorie, string exonere, string debut)
    {
        var (i, refus) = ImportPartenaires.VersInput(Ligne(("Type*", type), ("Catégorie*", categorie), ("Raison sociale ou nom*", "X"), ("Exonéré de TVA", exonere)));
        Assert.Null(i);
        Assert.StartsWith(debut, refus);
    }

    [Fact]
    public void Ice_invalide_signale_par_la_validation_de_la_fiche()
    {
        var (i, _) = ImportPartenaires.VersInput(Ligne(("Type*", "BCT"), ("Catégorie*", "morale"), ("Raison sociale ou nom*", "Contrôle SA"), ("ICE", "123")));
        Assert.Contains("ice", PartenairesEndpoints.Valider(i!).Keys);
    }
}
