using System.Text.Json;
using Progix.Modules.Devis.Domain;
using Progix.Modules.Devis.Endpoints;

namespace Progix.Tests.Devis;

/// <summary>Avancement d'un devis dans la liste : l'étape de la fiche où il en est.</summary>
public class AvancementTests
{
    private static (string Titre, int Etape) Lire(DevisEntete d, HashSet<Guid>? fichiers = null)
    {
        var j = JsonSerializer.SerializeToElement(DevisEndpoints.Avancement(d, fichiers));
        return (j.GetProperty("titre").GetString()!, j.GetProperty("etape").GetInt32());
    }

    private static DevisEntete Brouillon(params LigneDevis[] lignes) => new() { Etat = EtatsDevis.Brouillon, Lignes = [.. lignes] };

    [Fact]
    public void Brouillon_sans_fichier_ni_prix_attend_ses_fichiers()
    {
        Assert.Equal(("Fichiers ADAPT à déposer", 1), Lire(Brouillon(new LigneDevis { DalleId = Guid.NewGuid() })));
    }

    [Fact]
    public void Brouillon_avec_des_fichiers_mais_une_dalle_sans_ratio_actif_reste_a_completer()
    {
        var a = new LigneDevis { DalleId = Guid.NewGuid(), PrixM2 = 150, RatioActif = 2 };
        var b = new LigneDevis { DalleId = Guid.NewGuid(), PrixM2 = 101, RatioActif = 0 };
        Assert.Equal(("Chiffrage à compléter", 2), Lire(Brouillon(a, b), [a.DalleId]));
    }

    [Fact]
    public void Brouillon_chiffre_sur_toutes_ses_dalles_est_a_relire()
    {
        var a = new LigneDevis { DalleId = Guid.NewGuid(), PrixM2 = 150, RatioActif = 2 };
        Assert.Equal(("Chiffré, à relire et envoyer", 3), Lire(Brouillon(a)));
    }

    [Fact]
    public void Devis_envoye_signe_ou_non()
    {
        Assert.Equal("Envoyé, à signer", Lire(new DevisEntete { Etat = EtatsDevis.Envoye }).Titre);
        Assert.Equal("Signé et envoyé", Lire(new DevisEntete { Etat = EtatsDevis.Envoye, SignePar = "Directeur" }).Titre);
        Assert.Equal("Validé par le client", Lire(new DevisEntete { Etat = EtatsDevis.Valide }).Titre);
    }
}
