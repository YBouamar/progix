using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Endpoints;

namespace Progix.Tests.Projets;

/// <summary>Tendons de la commande usine, import et référence de dalle.</summary>
public class CommandeUsineTests
{
    [Theory]
    [InlineData("1", "01")]
    [InlineData(" 7 ", "07")]
    [InlineData("2634", "2634")]
    [InlineData("3.0", "03")]
    [InlineData("X1", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Numero_de_tendon_simple(string? saisi, string? attendu) => Assert.Equal(attendu, ImportTendons.Numero(saisi));

    [Theory]
    [InlineData("x", "X")]
    [InlineData(" Y ", "Y")]
    [InlineData("z", null)]
    public void Orientation_x_ou_y(string saisi, string? attendu) => Assert.Equal(attendu, ImportTendons.Sens(saisi));

    [Fact]
    public void Commande_valide_acceptee() =>
        Assert.Null(ImportTendons.Refus([new TendonInput("1", 4, "X", 12.5m, 2), new TendonInput("2", 1, "y", 22.2m, 1)]));

    [Fact]
    public void Tendon_en_double_refuse() =>
        Assert.Equal("Le tendon 01 figure deux fois.", ImportTendons.Refus([new TendonInput("1", 4, "X", 10, 2), new TendonInput("01", 4, "X", 10, 2)]));

    [Theory]
    [InlineData(0, 4, "X", 0)]
    [InlineData(501, 4, "X", 0)]
    [InlineData(10, 0, "X", 0)]
    [InlineData(10, 4, "Z", 0)]
    [InlineData(10, 4, "X", 100)]
    public void Tendon_invalide_refuse(decimal longueur, int torons, string sens, int ancrages) =>
        Assert.NotNull(ImportTendons.Refus([new TendonInput("1", torons, sens, longueur, ancrages)]));

    [Fact]
    public void Gaine_et_metres_de_toron()
    {
        var l = new LigneCommande { Numero = "01", Torons = 4, Longueur = 12.5m, AncragesSaisis = 3 };
        Assert.Equal(12.5m, l.Gaine);
        Assert.Equal(50m, l.MetresTorons);
        Assert.Equal(3, l.Ancrages);
    }

    [Fact]
    public void Ancienne_ligne_groupee_depliee()
    {
        var c = new CommandeUsine { Lignes = [new LigneCommande { Prefixe = "X", Nombre = 3, Torons = 4, Longueur = 10 }] };
        var t = c.Tendons().ToList();
        Assert.Equal(["X01", "X02", "X03"], t.Select(x => x.Numero));
        Assert.All(t, x => Assert.Equal(2, x.Ancrages));
        Assert.Equal("S4", t[0].Systeme);
    }

    [Fact]
    public void Stock_consomme_a_la_livraison()
    {
        Assert.False(new CommandeUsine { FabricationLe = DateTime.UtcNow }.Consommee);
        Assert.True(new CommandeUsine { LivreeLe = DateTime.UtcNow }.Consommee);
    }

    [Fact]
    public void Reference_de_dalle_numero_et_lettre()
    {
        Assert.Equal("01A", new Dalle { Numero = 1 }.Suffixe);
        Assert.Equal("12C", new Dalle { Numero = 12, Version = 'C' }.Suffixe);
    }

    [Theory]
    [InlineData(0, "Rez-de-chaussée")]
    [InlineData(-1, "1er sous-sol")]
    [InlineData(-2, "2ème sous-sol")]
    [InlineData(1, "1er étage")]
    [InlineData(3, "3ème étage")]
    public void Libelle_de_niveau(int niveau, string attendu) => Assert.Equal(attendu, Dalle.NiveauLabel(niveau));
}
