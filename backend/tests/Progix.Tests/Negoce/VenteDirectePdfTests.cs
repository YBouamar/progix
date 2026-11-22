using Progix.Modules.Facturation.Domain;
using Progix.Modules.Facturation.Pdf;
using Progix.Modules.Negoce.Application;
using Progix.Modules.Negoce.Domain;
using Progix.SharedKernel;
using Xunit;

namespace Progix.Tests.Negoce;

public class VenteDirectePdfTests
{
    private static DocumentVentePdf Document(string type, bool annule = false) => new(type, "FAC-00001-2026", "APT", new DateOnly(2026, 11, 22), new DateOnly(2026, 12, 22), "Client Test", "Casablanca", "ICE 123",
        "P-001", "CMD-00001-2026", 20m, [new LigneVentePdf("POU-12", "Poutrelle 12 cm", "ml", 120.5m, 38.9m, 4687.45m)], 4687.45m, 937.49m, 5624.94m, "Livraison partielle", annule, annule ? "Erreur de saisie" : null,
        "directeur", new DateTime(2026, 11, 22, 21, 0, 0, DateTimeKind.Utc));

    [Theory]
    [InlineData("devis")]
    [InlineData("bl")]
    [InlineData("facture")]
    [InlineData("avoir")]
    public void Chaque_document_de_vente_produit_un_pdf_signe_avec_qr(string type)
    {
        var e = new ParametresEntite { Code = "APT", RaisonSociale = "APT Test" };
        var pdf = DocumentsPdf.VenteDirecte(Document(type), e, "http://localhost:3000/verification/abc123");
        Assert.True(pdf.Length > 2000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void Un_document_annule_produit_quand_meme_un_pdf()
    {
        var e = new ParametresEntite { Code = "APT", RaisonSociale = "APT Test" };
        Assert.True(DocumentsPdf.VenteDirecte(Document("facture", annule: true), e, "http://x/verification/abc").Length > 2000);
    }

    [Fact]
    public void Le_plafond_des_especes_est_de_5000_dh_comme_en_post_tension()
    {
        Assert.Equal(5000m, PlafondEspecesClient.Montant);
        Assert.Equal(PlafondEspecesClient.Montant, Progix.Modules.Facturation.Endpoints.EncaissementsEndpoints.PlafondEspeces);
    }
}
