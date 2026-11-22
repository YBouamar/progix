using Progix.Modules.Negoce.Application;
using Progix.Modules.Negoce.Domain;
using Xunit;

namespace Progix.Tests.Negoce;

public class IndicateursNegoceTests
{
    private static LigneVente Ligne(decimal q, decimal prix, decimal cout = 0, Guid? source = null) =>
        new() { Id = Guid.NewGuid(), Quantite = q, PrixUnitaire = prix, CoutUnitaire = cout, SourceLigneId = source };

    private static DocumentVente Doc(string type, string date, string etat = EtatsVente.Emis, Guid? parent = null, params LigneVente[] lignes) =>
        new() { Id = Guid.NewGuid(), Type = type, DateDocument = DateOnly.Parse(date), Etat = etat, ParentId = parent, TauxTva = 20m, Lignes = [.. lignes] };

    private static readonly DateOnly Du = new(2026, 11, 1), Au = new(2026, 11, 30);

    [Fact]
    public void Periode_precedente_d_un_mois_entier_est_le_mois_d_avant()
    {
        var (du, au) = Indicateurs.Precedente(Du, Au);
        Assert.Equal(new DateOnly(2026, 10, 1), du);
        Assert.Equal(new DateOnly(2026, 10, 31), au);
    }

    [Fact]
    public void Periode_precedente_d_une_periode_libre_a_la_meme_duree()
    {
        var (du, au) = Indicateurs.Precedente(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 19));
        Assert.Equal(new DateOnly(2026, 10, 31), du);
        Assert.Equal(new DateOnly(2026, 11, 9), au);
    }

    [Fact]
    public void Chiffre_d_affaires_en_ttc_apres_avoirs_hors_factures_annulees()
    {
        var f1 = Doc(TypesVente.Facture, "2026-11-05", lignes: Ligne(10, 100));           // 1000 HT, 1200 TTC
        var f2 = Doc(TypesVente.Facture, "2026-11-10", lignes: Ligne(5, 100));           // 600 TTC
        var f3 = Doc(TypesVente.Facture, "2026-11-12", EtatsVente.Annule, lignes: Ligne(1, 100));
        var hors = Doc(TypesVente.Facture, "2026-10-28", lignes: Ligne(1, 100));
        var avoir = Doc(TypesVente.Avoir, "2026-11-15", parent: f1.Id, lignes: Ligne(2, 100)); // 240 TTC
        var avoirs = new[] { avoir }.ToLookup(a => a.ParentId);
        Assert.Equal(1200m - 240m + 600m, Indicateurs.ChiffreAffaires([f1, f2, f3, hors], avoirs, Du, Au));
    }

    [Fact]
    public void Un_avoir_annule_ne_reduit_pas_le_chiffre_d_affaires()
    {
        var f = Doc(TypesVente.Facture, "2026-11-05", lignes: Ligne(10, 100));
        var avoir = Doc(TypesVente.Avoir, "2026-11-15", EtatsVente.Annule, f.Id, Ligne(2, 100));
        Assert.Equal(1200m, Indicateurs.ChiffreAffaires([f], new[] { avoir }.ToLookup(a => a.ParentId), Du, Au));
    }

    [Fact]
    public void Encaisse_compte_les_paiements_de_la_periode_non_annules()
    {
        var f = Doc(TypesVente.Facture, "2026-10-20", lignes: Ligne(10, 100));
        f.Paiements = [
            new PaiementClient { Montant = 300, DatePaiement = new DateOnly(2026, 11, 3) },
            new PaiementClient { Montant = 200, DatePaiement = new DateOnly(2026, 11, 4), Annule = true },
            new PaiementClient { Montant = 100, DatePaiement = new DateOnly(2026, 10, 30) },
        ];
        Assert.Equal(300m, Indicateurs.Encaisse([f], Du, Au));
    }

    [Fact]
    public void Marge_brute_est_le_prix_moins_le_cout_moyen_des_livraisons_de_la_periode()
    {
        var bl1 = Doc(TypesVente.Livraison, "2026-11-06", lignes: Ligne(10, 100, 70));   // 300
        var bl2 = Doc(TypesVente.Livraison, "2026-11-07", EtatsVente.Annule, lignes: Ligne(10, 100, 70));
        var bl3 = Doc(TypesVente.Livraison, "2026-10-07", lignes: Ligne(10, 100, 70));
        Assert.Equal(300m, Indicateurs.MargeBrute([bl1, bl2, bl3], Du, Au));
    }

    [Fact]
    public void Taux_de_conversion_des_devis_de_la_periode()
    {
        var devis = new[]
        {
            Doc(TypesVente.Devis, "2026-11-02", EtatsVente.Accepte), Doc(TypesVente.Devis, "2026-11-03", EtatsVente.Ouvert),
            Doc(TypesVente.Devis, "2026-11-04", EtatsVente.Refuse), Doc(TypesVente.Devis, "2026-11-05", EtatsVente.Accepte),
            Doc(TypesVente.Devis, "2026-11-06", EtatsVente.Annule), Doc(TypesVente.Devis, "2026-09-06", EtatsVente.Accepte),
        };
        Assert.Equal(50m, Indicateurs.TauxConversion(devis, Du, Au));
        Assert.Null(Indicateurs.TauxConversion([], Du, Au));
    }

    [Fact]
    public void Commandes_a_livrer_et_a_facturer()
    {
        var ligne = Ligne(10, 100);
        var commande = Doc(TypesVente.Commande, "2026-11-01", EtatsVente.Ouvert, lignes: ligne);
        var bl = Doc(TypesVente.Livraison, "2026-11-05", parent: commande.Id, lignes: Ligne(4, 100, source: ligne.Id));
        var enfants = new[] { bl }.ToLookup(d => d.ParentId);
        Assert.Equal(1, Indicateurs.ALivrer([commande], enfants));
        Assert.Equal((1, 400m), Indicateurs.AFacturer([commande], enfants));

        var facture = Doc(TypesVente.Facture, "2026-11-06", parent: commande.Id, lignes: Ligne(4, 100, source: ligne.Id));
        var apres = new[] { bl, facture }.ToLookup(d => d.ParentId);
        Assert.Equal((0, 0m), Indicateurs.AFacturer([commande], apres));
    }

    [Fact]
    public void Un_article_sans_seuil_n_est_jamais_sous_seuil()
    {
        Assert.False(Indicateurs.SousSeuil(0, 0));
        Assert.True(Indicateurs.SousSeuil(5, 5));
        Assert.False(Indicateurs.SousSeuil(6, 5));
    }
}
