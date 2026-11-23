using Progix.Modules.Negoce.Application;
using Progix.Modules.Negoce.Domain;
using Xunit;

namespace Progix.Tests.Negoce;

public class SuppressionTests
{
    [Fact] public void DevisOuvertNonSigneSeSupprime() =>
        Assert.Null(Suppression.RefusDevis(new DocumentVente { Type = TypesVente.Devis, Etat = EtatsVente.Ouvert }, false));

    [Fact] public void DevisSigneNeSeSupprimePas() =>
        Assert.NotNull(Suppression.RefusDevis(new DocumentVente { Type = TypesVente.Devis, Etat = EtatsVente.Ouvert, Jeton = "abc" }, false));

    [Fact] public void DevisDevenuCommandeNeSeSupprimePas()
    {
        Assert.NotNull(Suppression.RefusDevis(new DocumentVente { Type = TypesVente.Devis, Etat = EtatsVente.Accepte }, false));
        Assert.NotNull(Suppression.RefusDevis(new DocumentVente { Type = TypesVente.Devis, Etat = EtatsVente.Ouvert }, true));
    }

    [Fact] public void CommandeSansLivraisonNiFactureSeSupprime() => Assert.Null(Suppression.RefusCommande(0));
    [Fact] public void CommandeLivreeMemePartiellementNeSeSupprimePas() => Assert.NotNull(Suppression.RefusCommande(1));
}
