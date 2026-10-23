using Progix.Modules.Facturation.Domain;
using Progix.Modules.Facturation.Endpoints;
using Progix.Modules.Facturation.Pdf;

namespace Progix.Tests.Facturation;

/// <summary>Réglé, reste dû et statut d'une facture ; effets ; montants en lettres.</summary>
public class FactureTests
{
    private static readonly DateOnly Jour = DateOnly.FromDateTime(DateTime.UtcNow);

    private static Facture Facture(decimal ttc = 1200, int echeanceDansJours = 30) =>
        new() { TotalTTC = ttc, DateFacture = Jour.AddDays(-10), Echeance = Jour.AddDays(echeanceDansJours) };

    private static Encaissement Paiement(decimal montant, string mode = ModesPaiement.Virement, DateOnly? echeanceEffet = null, bool annule = false, bool remboursement = false) =>
        new() { Montant = montant, Mode = mode, DatePaiement = Jour.AddDays(-1), EcheanceEffet = echeanceEffet, Annule = annule, Remboursement = remboursement };

    [Fact]
    public void Facture_sans_reglement_impayee()
    {
        var f = Facture();
        Assert.Equal(1200, f.Reste);
        Assert.Equal("impayee", FacturesEndpoints.Statut(f));
    }

    [Fact]
    public void Reglement_partiel_et_annulation()
    {
        var f = Facture();
        f.Encaissements = [Paiement(500), Paiement(100, annule: true)];
        Assert.Equal(500, f.Regle);
        Assert.Equal(700, f.Reste);
        Assert.Equal("partielle", FacturesEndpoints.Statut(f));
    }

    [Fact]
    public void Effet_compte_seulement_a_son_echeance()
    {
        var f = Facture();
        f.Encaissements = [Paiement(300, ModesPaiement.Effet, Jour.AddDays(15)), Paiement(200, ModesPaiement.Effet, Jour.AddDays(-1))];
        Assert.Equal(200, f.Regle);
        Assert.Equal(300, f.EffetsEnAttente);
        Assert.Equal(1000, f.Reste);
        Assert.Equal(700, f.ACouvrir);
    }

    [Fact]
    public void Avoir_partiel_reduit_le_du()
    {
        var f = Facture();
        f.Avoirs = [new Avoir { TotalTTC = 200 }, new Avoir { TotalTTC = 999, Annule = true }];
        Assert.Equal(1000, f.Du);
        Assert.False(f.Annulee);
    }

    [Fact]
    public void Avoir_total_annule_la_facture()
    {
        var f = Facture();
        f.Avoirs = [new Avoir { TotalTTC = 1200, Total = true }];
        Assert.True(f.Annulee);
        Assert.Equal("annulee", FacturesEndpoints.Statut(f));
    }

    [Fact]
    public void Trop_percu_puis_remboursement()
    {
        var f = Facture();
        f.Encaissements = [Paiement(1200)];
        f.Avoirs = [new Avoir { TotalTTC = 200 }];
        Assert.Equal(-200, f.Reste);
        Assert.Equal("payee", FacturesEndpoints.Statut(f));
        f.Encaissements.Add(Paiement(200, remboursement: true));
        Assert.Equal(0, f.Reste);
    }

    [Fact]
    public void Facture_echue_en_retard() => Assert.Equal("retard", FacturesEndpoints.Statut(Facture(echeanceDansJours: -1)));

    [Fact]
    public void Constantes_legales()
    {
        Assert.Equal(5000m, EncaissementsEndpoints.PlafondEspeces);
        Assert.Equal(90, FacturesEndpoints.DelaiMaxJours);
        Assert.Equal(20m, FacturesEndpoints.TauxTva);
    }

    [Theory]
    [InlineData(1, "Un dirham")]
    [InlineData(2, "Deux dirhams")]
    [InlineData(21, "Vingt et un dirhams")]
    [InlineData(80, "Quatre-vingts dirhams")]
    [InlineData(100.5, "Cent dirhams et cinquante centimes")]
    public void Montant_en_lettres(decimal montant, string attendu) => Assert.Equal(attendu, EnLettres.Montant(montant));
}
