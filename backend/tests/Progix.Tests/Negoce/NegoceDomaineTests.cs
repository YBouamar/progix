using Progix.Modules.Negoce.Domain;
using Progix.SharedKernel;

namespace Progix.Tests.Negoce;

/// <summary>Règles du négoce : coût moyen pondéré, tonnage des tendons, mouvements, prix de groupe, réception, avancement des ventes.</summary>
public class NegoceDomaineTests
{
    [Fact]
    public void Le_cout_moyen_se_pondere_par_les_quantites()
    {
        // 100 à 10 DH en stock, 50 reçus à 16 DH : (1000 + 800) / 150 = 12.
        Assert.Equal(12m, Valorisation.NouveauCoutMoyen(100, 10, 50, 16));
    }

    [Fact]
    public void Sans_stock_le_cout_de_l_entree_devient_le_cout_moyen()
    {
        Assert.Equal(16m, Valorisation.NouveauCoutMoyen(0, 10, 50, 16));
        Assert.Equal(16m, Valorisation.NouveauCoutMoyen(-5, 10, 50, 16));
    }

    [Fact]
    public void Une_entree_au_cout_moyen_ne_le_change_pas() =>
        Assert.Equal(12.3457m, Valorisation.NouveauCoutMoyen(80, 12.3457m, 20, 12.3457m));

    [Fact]
    public void Un_metre_de_toron_pese_1_18_kg()
    {
        Assert.Equal(1.18m, Valorisation.TonnesDepuisMetres(1000, 1.18m));
        Assert.Equal(11.8m, Valorisation.TonnesDepuisMetres(10_000, 1.18m));
    }

    [Theory]
    [InlineData(TypesMouvement.Entree, 5, 0, 5)]
    [InlineData(TypesMouvement.Production, 12, 3, 12)]
    [InlineData(TypesMouvement.Sortie, 4, 10, -4)]
    [InlineData(TypesMouvement.Inventaire, 7, 10, -3)]
    [InlineData(TypesMouvement.Inventaire, 14, 10, 4)]
    [InlineData(TypesMouvement.Correction, -2, 10, -2)]
    public void Un_mouvement_valide_est_signe(string type, double saisie, double stock, double attendu)
    {
        var (q, refus) = Valorisation.Signer(type, (decimal)saisie, (decimal)stock, "motif");
        Assert.Null(refus);
        Assert.Equal((decimal)attendu, q);
    }

    [Fact]
    public void Une_sortie_ne_depasse_pas_le_stock() =>
        Assert.StartsWith("Stock insuffisant", Valorisation.Signer(TypesMouvement.Sortie, 11, 10, "casse").Refus);

    [Theory]
    [InlineData(TypesMouvement.Sortie, 1, 10)]
    [InlineData(TypesMouvement.Correction, 1, 10)]
    public void Une_sortie_et_une_correction_demandent_un_motif(string type, double saisie, double stock) =>
        Assert.NotNull(Valorisation.Signer(type, (decimal)saisie, (decimal)stock, " ").Refus);

    [Fact]
    public void Un_inventaire_identique_au_stock_est_refuse() =>
        Assert.NotNull(Valorisation.Signer(TypesMouvement.Inventaire, 10, 10, null).Refus);

    [Fact]
    public void Une_correction_ne_rend_pas_le_stock_negatif() =>
        Assert.NotNull(Valorisation.Signer(TypesMouvement.Correction, -11, 10, "erreur").Refus);

    [Fact]
    public void Une_entree_demande_une_quantite_positive()
    {
        Assert.NotNull(Valorisation.Signer(TypesMouvement.Entree, 0, 10, null).Refus);
        Assert.NotNull(Valorisation.Signer(TypesMouvement.Production, -1, 10, null).Refus);
    }

    private static readonly Guid Article = Guid.NewGuid();
    private static readonly Guid Autre = Guid.NewGuid();
    private static readonly Guid Categorie = Guid.NewGuid();

    private static RegleGroupe Regle(Guid? article, Guid? categorie, string type, decimal valeur) => new() { ArticleId = article, CategorieId = categorie, Type = type, Valeur = valeur };

    [Fact]
    public void Sans_regle_le_prix_public_s_applique() =>
        Assert.Equal(100m, Tarification.PrixPour(100m, Tarification.RegleApplicable([], Article, Categorie)));

    [Fact]
    public void La_remise_en_pourcentage_et_en_dirhams_se_calculent_sur_le_prix_public()
    {
        Assert.Equal(90m, Tarification.PrixPour(100m, Regle(null, null, TypesRegle.Pourcent, 10)));
        Assert.Equal(92.5m, Tarification.PrixPour(100m, Regle(null, Categorie, TypesRegle.Montant, 7.5m)));
    }

    [Fact]
    public void Un_prix_fixe_l_emporte_sur_la_remise_de_la_categorie_et_du_catalogue()
    {
        var regles = new[] { Regle(null, null, TypesRegle.Pourcent, 5), Regle(null, Categorie, TypesRegle.Pourcent, 10), Regle(Article, null, TypesRegle.Prix, 70) };
        var r = Tarification.RegleApplicable(regles, Article, Categorie);
        Assert.Equal(TypesRegle.Prix, r!.Type);
        Assert.Equal(70m, Tarification.PrixPour(100m, r));
    }

    [Fact]
    public void La_regle_de_categorie_l_emporte_sur_celle_du_catalogue()
    {
        var regles = new[] { Regle(null, null, TypesRegle.Pourcent, 5), Regle(null, Categorie, TypesRegle.Pourcent, 10) };
        Assert.Equal(10m, Tarification.RegleApplicable(regles, Article, Categorie)!.Valeur);
        Assert.Equal(5m, Tarification.RegleApplicable(regles, Article, Guid.NewGuid())!.Valeur);
    }

    [Fact]
    public void La_regle_d_un_autre_article_ne_s_applique_pas() =>
        Assert.Null(Tarification.RegleApplicable([Regle(Autre, null, TypesRegle.Prix, 50)], Article, Categorie));

    [Fact]
    public void Un_prix_de_groupe_n_est_jamais_negatif() =>
        Assert.Equal(0m, Tarification.PrixPour(5m, Regle(null, null, TypesRegle.Montant, 8)));

    [Theory]
    [InlineData(TypesRegle.Prix, false, false, 10, true)]
    [InlineData(TypesRegle.Pourcent, true, false, 101, true)]
    [InlineData(TypesRegle.Pourcent, false, false, -1, true)]
    [InlineData("inconnu", false, false, 1, true)]
    [InlineData(TypesRegle.Pourcent, true, true, 10, true)]
    [InlineData(TypesRegle.Prix, true, false, 10, false)]
    [InlineData(TypesRegle.Montant, false, true, 3, false)]
    public void Une_regle_incoherente_est_refusee(string type, bool article, bool categorie, double valeur, bool refusee)
    {
        var r = Regle(article ? Article : null, categorie ? Categorie : null, type, (decimal)valeur);
        Assert.Equal(refusee, r.Refus() is not null);
    }

    private static CommandeAchat Commande(params (decimal Quantite, decimal Recue, decimal Prix)[] lignes) => new()
    {
        Lignes = lignes.Select(l => new LigneAchat { Designation = "Article", Quantite = l.Quantite, QuantiteRecue = l.Recue, PrixUnitaire = l.Prix }).ToList(),
    };

    [Fact]
    public void Une_commande_est_en_attente_partielle_puis_recue()
    {
        Assert.Equal(EtatsAchat.EnAttente, Commande((10, 0, 5), (4, 0, 2)).Etat);
        Assert.Equal(EtatsAchat.Partielle, Commande((10, 10, 5), (4, 1, 2)).Etat);
        Assert.Equal(EtatsAchat.Recue, Commande((10, 10, 5), (4, 4, 2)).Etat);
    }

    [Fact]
    public void Une_commande_annulee_reste_annulee()
    {
        var c = Commande((10, 10, 5));
        c.Annule = true;
        Assert.Equal(EtatsAchat.Annulee, c.Etat);
    }

    [Fact]
    public void Le_total_d_une_commande_d_achat_porte_la_tva_a_20_pour_cent()
    {
        var c = Commande((10, 0, 5.5m), (3, 0, 100));
        Assert.Equal(355m, c.TotalHT);
        Assert.Equal(71m, c.TotalTVA);
        Assert.Equal(426m, c.TotalTTC);
    }

    [Fact]
    public void Les_paiements_annules_ne_comptent_pas_dans_le_reste_a_payer()
    {
        var c = Commande((10, 0, 10));
        c.Paiements = [new() { Montant = 50 }, new() { Montant = 30, Annule = true }];
        Assert.Equal(50m, c.Paye);
        Assert.Equal(70m, c.ResteAPayer);
    }

    [Fact]
    public void Une_reception_ne_depasse_pas_la_quantite_restante()
    {
        var l = new LigneAchat { Designation = "Hourdis", Quantite = 100, QuantiteRecue = 60 };
        Assert.Null(Reception.Refus(l, 40));
        Assert.NotNull(Reception.Refus(l, 41));
        Assert.NotNull(Reception.Refus(l, 0));
    }

    private static DocumentVente Doc(string type, Guid source, decimal quantite, bool annule = false) => new()
    {
        Type = type, Etat = annule ? EtatsVente.Annule : EtatsVente.Emis,
        Lignes = [new LigneVente { SourceLigneId = source, Quantite = quantite }],
    };

    [Fact]
    public void Une_commande_se_livre_en_plusieurs_bons_et_ne_se_facture_que_livree()
    {
        var ligne = new LigneVente { Id = Guid.NewGuid(), Quantite = 100 };
        var enfants = new List<DocumentVente> { Doc(TypesVente.Livraison, ligne.Id, 30), Doc(TypesVente.Livraison, ligne.Id, 20), Doc(TypesVente.Facture, ligne.Id, 40) };
        Assert.Equal(50m, Avancement.ResteALivrer(ligne, enfants));
        Assert.Equal(10m, Avancement.ResteAFacturer(ligne, enfants));
    }

    [Fact]
    public void Un_bon_ou_une_facture_annule_libere_ses_quantites()
    {
        var ligne = new LigneVente { Id = Guid.NewGuid(), Quantite = 100 };
        var enfants = new List<DocumentVente> { Doc(TypesVente.Livraison, ligne.Id, 30, annule: true), Doc(TypesVente.Livraison, ligne.Id, 20), Doc(TypesVente.Facture, ligne.Id, 10, annule: true) };
        Assert.Equal(80m, Avancement.ResteALivrer(ligne, enfants));
        Assert.Equal(20m, Avancement.ResteAFacturer(ligne, enfants));
    }

    [Fact]
    public void Un_avoir_ne_credite_pas_plus_que_la_facture()
    {
        var ligne = new LigneVente { Id = Guid.NewGuid(), Quantite = 10 };
        var avoirs = new List<DocumentVente> { Doc(TypesVente.Avoir, ligne.Id, 4), Doc(TypesVente.Avoir, ligne.Id, 3, annule: true) };
        Assert.Equal(6m, Avancement.ResteACrediter(ligne, avoirs));
    }

    [Fact]
    public void Les_totaux_d_un_document_suivent_le_taux_de_tva_et_l_exoneration()
    {
        var d = new DocumentVente { TauxTva = 20, Lignes = [new() { Quantite = 3, PrixUnitaire = 33.33m }, new() { Quantite = 2, PrixUnitaire = 10 }] };
        Assert.Equal(119.99m, d.TotalHT);
        Assert.Equal(24m, d.TotalTVA);
        Assert.Equal(143.99m, d.TotalTTC);
        d.TauxTva = 0;
        Assert.Equal(d.TotalHT, d.TotalTTC);
    }

    [Fact]
    public void Les_paiements_annules_ne_comptent_pas_dans_l_encaisse()
    {
        var d = new DocumentVente { Paiements = [new() { Montant = 100 }, new() { Montant = 40, Annule = true }] };
        Assert.Equal(100m, d.Encaisse);
    }
}
