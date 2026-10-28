using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Endpoints;

namespace Progix.Tests.Projets;

/// <summary>Types de construction paramétrables : code stable tiré du libellé, libellés en mémoire.</summary>
public class TypesConstructionTests
{
    [Theory]
    [InlineData("Bâtiment industriel", "batiment_industriel")]
    [InlineData("  Hôpital / Clinique ", "hopital_clinique")]
    [InlineData("***", "type")]
    public void Code_tire_du_libelle(string libelle, string attendu) => Assert.Equal(attendu, TypesConstructionEndpoints.NouveauCode(libelle, []));

    [Fact]
    public void Code_deja_pris_suffixe()
    {
        var pris = new HashSet<string> { "villa", "villa_2" };
        Assert.Equal("villa_3", TypesConstructionEndpoints.NouveauCode("Villa", pris));
        Assert.Contains("villa_3", pris);
    }

    [Fact]
    public void Libelle_parametre_sinon_par_defaut()
    {
        TypesConstruction.Charger([new TypeConstructionParam { Code = "villa", Libelle = "Villa individuelle" }, new TypeConstructionParam { Code = "hangar", Libelle = "Hangar" }]);
        try
        {
            Assert.Equal("Villa individuelle", TypesConstruction.Libelle("villa"));
            Assert.Equal("Hangar", TypesConstruction.Libelle("hangar"));
            Assert.Equal("Immeuble", TypesConstruction.Libelle("immeuble"));
            Assert.Equal("inconnu", TypesConstruction.Libelle("inconnu"));
        }
        finally { TypesConstruction.Charger([]); }
    }
}
