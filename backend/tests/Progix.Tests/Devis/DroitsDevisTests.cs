using System.Security.Claims;
using System.Text.Json;
using ClosedXML.Excel;
using Progix.Modules.Devis.Domain;
using Progix.Modules.Devis.Endpoints;
using Progix.SharedKernel;

namespace Progix.Tests.Devis;

/// <summary>Droits sur le devis (administrateur en consultation, rejet réservé), libellé « Rejeté », trame de calcul Excel.</summary>
public class DroitsDevisTests
{
    private static ClaimsPrincipal Role(string role) => new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));

    /// <summary>Actions proposées pour un devis dans cet état, lues comme le fait l'écran.</summary>
    private static JsonElement Actions(string etat, string role) =>
        JsonSerializer.SerializeToElement(DevisEtatsEndpoints.Actions(new DevisEntete { Etat = etat }, Role(role), derniereVersion: true));

    [Fact]
    public void Administrateur_en_consultation_seule()
    {
        Assert.False(DevisEtatsEndpoints.PeutEcrire(Role(Roles.Administrateur)));
        Assert.False(DevisEtatsEndpoints.PeutSigner(Role(Roles.Administrateur)));
        foreach (var etat in new[] { EtatsDevis.Brouillon, EtatsDevis.Envoye, EtatsDevis.Valide })
        {
            var a = Actions(etat, Roles.Administrateur);
            foreach (var action in new[] { "modifier", "envoyer", "valider", "rejeter", "nouvelleVersion", "trame" })
                Assert.False(a.GetProperty(action).GetBoolean(), $"{action} proposé à l'administrateur sur un devis {etat}");
        }
    }

    [Theory]
    [InlineData(Roles.Directeur)]
    [InlineData(Roles.ChefProjet)]
    [InlineData(Roles.ResponsableAdministratif)]
    public void Rejet_par_la_direction_et_le_responsable_administratif(string role)
    {
        foreach (var etat in new[] { EtatsDevis.Brouillon, EtatsDevis.Envoye, EtatsDevis.Expire, EtatsDevis.Valide })
            Assert.True(Actions(etat, role).GetProperty("rejeter").GetBoolean());
        Assert.False(Actions(EtatsDevis.Remplace, role).GetProperty("rejeter").GetBoolean());
    }

    [Theory]
    [InlineData(Roles.IngenieurChef)]
    [InlineData(Roles.Ingenieur)]
    [InlineData(Roles.Administrateur)]
    public void Pas_de_rejet_pour_les_autres_roles(string role) =>
        Assert.False(Actions(EtatsDevis.Envoye, role).GetProperty("rejeter").GetBoolean());

    [Fact]
    public void Ingenieur_modifie_un_brouillon_sans_le_valider()
    {
        var a = Actions(EtatsDevis.Brouillon, Roles.Ingenieur);
        Assert.True(a.GetProperty("modifier").GetBoolean());
        Assert.True(a.GetProperty("trame").GetBoolean());
        Assert.False(Actions(EtatsDevis.Envoye, Roles.Ingenieur).GetProperty("valider").GetBoolean());
    }

    [Fact]
    public void Etat_annule_affiche_rejete() => Assert.Equal("Rejeté", EtatsDevis.Labels[EtatsDevis.Annule]);

    [Fact]
    public void Trame_de_calcul_avec_formules_et_entetes_reimportables()
    {
        var d = new DevisEntete
        {
            Numero = "DEV-00001-2026", Version = 2, RemiseM2 = 5,
            Lignes =
            [
                new LigneDevis { Ordre = 0, Reference = "APT-2026-00001-01A", Emplacement = "1er étage", Surface = 100, RatioActif = 10, Systemes = [new SystemeLigne { Torons = 4, Ancrages = 10, Gaine = 200 }] },
                new LigneDevis { Ordre = 1, Reference = "APT-2026-00001-02A", Emplacement = "2ème étage", Surface = 50, Systemes = [new SystemeLigne { Torons = 7, Ancrages = 2, Gaine = 30 }] },
            ],
        };
        var octets = DevisImportEndpoints.TrameCalcul(d, null, "Client test", [new TrancheForfait { APartirDe = 0, Prix = 120 }, new TrancheForfait { APartirDe = 101, Prix = 100 }]);
        using var wb = new XLWorkbook(new MemoryStream(octets));

        // Premier onglet : celui que relit l'import, en-têtes sur la première ligne.
        var ws = wb.Worksheets.First();
        Assert.Equal("Dalles", ws.Name);
        var titres = ws.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Equal(["Référence", "Niveau", "Surface", "Ratio actif", "Ratio passif"], titres.Take(5));
        // Systèmes proposés d'office plus ceux du devis (S7), puis les deux colonnes calculées.
        Assert.Contains("S7 ancrages", titres);
        Assert.Contains("S1 gaine", titres);
        Assert.Equal(["Prix au m² (calculé)", "Montant HT (calculé)"], titres.TakeLast(2));
        Assert.Equal("APT-2026-00001-01A", ws.Cell(2, 1).GetString());
        var colPrix = titres.Count - 1;
        Assert.StartsWith("IF(C2>0,INT(", ws.Cell(2, colPrix).FormulaA1);
        Assert.Contains("'Synthèse'!$B$8", ws.Cell(2, colPrix).FormulaA1);

        var sy = wb.Worksheet("Synthèse");
        Assert.Equal("SUM(Dalles!C2:C3)", sy.Cell(7, 2).FormulaA1);
        Assert.StartsWith("VLOOKUP(", sy.Cell(8, 2).FormulaA1);
        Assert.Equal(5, sy.Cell(10, 2).GetDouble());
        Assert.Equal(2, wb.Worksheet("Forfaits").RowsUsed().Count() - 1);

        // Les formules, évaluées, donnent les mêmes prix que PROGIX : 150 m² au total, donc forfait de 100 DH/m² (tranche à partir de 101 m²).
        Assert.Equal(150, sy.Cell(7, 2).GetDouble());
        Assert.Equal(100, sy.Cell(8, 2).GetDouble());
        Assert.Equal(Calcul.PrixM2(d.Lignes[0], 100), (int)ws.Cell(2, colPrix).GetDouble());
        Assert.Equal(Calcul.PrixM2(d.Lignes[1], 100), (int)ws.Cell(3, colPrix).GetDouble());
        Assert.Equal(291 * 100 + 141 * 50, sy.Cell(9, 2).GetDouble());
        Assert.Equal(Math.Round((291 * 100 + 141 * 50 - 5 * 150) * 1.2, 2), sy.Cell(14, 2).GetDouble(), 2);
    }
}
