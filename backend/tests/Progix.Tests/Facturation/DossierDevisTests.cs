using Progix.Modules.Facturation.Endpoints;

namespace Progix.Tests.Facturation;

/// <summary>Dossier ZIP du devis : nom des documents joints tels qu'ils entrent dans l'archive.</summary>
public class DossierDevisTests
{
    [Theory]
    [InlineData("Plan de principe PH RDC.pdf", "Plan de principe PH RDC.pdf")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData(@"C:\Plans\coffrage.dwg", "coffrage.dwg")]
    [InlineData("plan:v2?.pdf", "plan_v2_.pdf")]
    [InlineData("  ", "document")]
    public void Nom_d_un_document_sans_dossier_ni_caractere_interdit(string nom, string attendu) =>
        Assert.Equal(attendu, PdfEndpoints.NomDeFichier(nom));
}
