using Progix.Modules.Facturation.Domain;
using Progix.Modules.Facturation.Endpoints;

namespace Progix.Tests.Facturation;

/// <summary>Paiements suivis par dalle : ventilation, imputation sur les factures, taux de paiement, trop-perçu.</summary>
public class PaiementsDallesTests
{
    private static readonly DateOnly Jour = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid();

    private static Encaissement Paiement(params (Guid Dalle, decimal Montant)[] dalles) => new()
    {
        Montant = dalles.Sum(d => d.Montant), DatePaiement = Jour.AddDays(-1), Mode = ModesPaiement.Virement,
        Dalles = dalles.Select(d => new PaiementDalle { DalleId = d.Dalle, Montant = d.Montant }).ToList(),
    };

    // Facture de livraison : une ligne par dalle, TTC = HT × 1,2.
    private static Facture Livraison(int ilYaJours, params (Guid Dalle, decimal HT)[] lignes)
    {
        var f = new Facture
        {
            Id = Guid.NewGuid(), Numero = $"FAC-{ilYaJours}", DateFacture = Jour.AddDays(-ilYaJours), Echeance = Jour.AddDays(30), TauxTva = 20,
            Lignes = lignes.Select((l, i) => new LigneFacture { Ordre = i, DalleId = l.Dalle, MontantHT = l.HT, Libelle = $"D{i} · niveau" }).ToList(),
        };
        f.TotalHT = f.NetHT = lignes.Sum(l => l.HT);
        f.TotalTTC = f.NetHT * 1.2m;
        f.Parts = Imputation.PartsLivraison(f);
        return f;
    }

    [Fact]
    public void Repartition_au_centime_ecart_sur_la_derniere_part()
    {
        var r = Imputation.Repartir(100m, [(A, 1), (B, 2)]);
        Assert.Equal(33.33m, r[0].Montant);
        Assert.Equal(66.67m, r[1].Montant);
        Assert.Equal(100m, r.Sum(x => x.Montant));
        Assert.Empty(Imputation.Repartir(100m, []));
    }

    [Fact]
    public void Parts_d_une_facture_de_livraison_au_prorata()
    {
        var f = Livraison(1, (A, 1000), (B, 3000));
        Assert.Equal(1200m, f.Parts.Single(p => p.DalleId == A).MontantTTC);
        Assert.Equal(3600m, f.Parts.Single(p => p.DalleId == B).MontantTTC);
        Assert.Equal("D0", f.Parts[0].Reference);
    }

    [Fact]
    public void Paiement_d_une_dalle_impute_sur_la_facture_la_plus_ancienne()
    {
        var ancienne = Livraison(20, (A, 1000));
        var recente = Livraison(5, (A, 500));
        Imputation.Imputer([recente, ancienne], [Paiement((A, 1500))], Jour);
        Assert.Equal(1200m, ancienne.Regle);
        Assert.Equal("payee", FacturesEndpoints.Statut(ancienne));
        Assert.Equal(300m, recente.Regle);
        Assert.Equal("partielle", FacturesEndpoints.Statut(recente));
    }

    [Fact]
    public void Paiement_ventile_sur_deux_dalles_d_une_meme_facture()
    {
        var f = Livraison(3, (A, 1000), (B, 1000));
        Imputation.Imputer([f], [Paiement((A, 1200), (B, 600))], Jour);
        Assert.Equal(1800m, f.Regle);
        Assert.Equal(600m, f.Reste);
    }

    [Fact]
    public void Avance_avant_facture_puis_imputation_a_l_emission()
    {
        // Le paiement à la signature arrive sur la dalle avant toute facture : il reste à son crédit.
        var paiement = Paiement((A, 400));
        var f = Livraison(1, (A, 1000));
        Imputation.Imputer([f], [paiement], Jour);
        Assert.Equal(400m, f.Regle);
        Assert.Equal(800m, f.Reste);
    }

    [Fact]
    public void Taux_de_paiement_sur_le_montant_TTC_du_devis()
    {
        var dues = new[] { new DalleDue(A, "A", "", 2000m), new DalleDue(B, "B", "", 0m) };
        var s = Imputation.Situation(dues, [], [Paiement((A, 500))], Jour);
        Assert.Equal(25.0m, s[0].Taux);
        Assert.Equal(1500m, s[0].Reste);
        Assert.Equal(1500m, s[0].ACouvrir);
        Assert.Null(s[1].Taux);
    }

    [Fact]
    public void Effet_non_echu_reduit_seulement_le_reste_a_couvrir()
    {
        var effet = Paiement((A, 300));
        effet.Mode = ModesPaiement.Effet; effet.EcheanceEffet = Jour.AddDays(10);
        var s = Imputation.Situation([new DalleDue(A, "A", "", 1000m)], [], [effet], Jour).Single();
        Assert.Equal(0m, s.Paye);
        Assert.Equal(300m, s.EffetsEnAttente);
        Assert.Equal(700m, s.ACouvrir);
    }

    [Fact]
    public void Avoir_partiel_cree_un_trop_percu_sur_la_dalle()
    {
        var f = Livraison(3, (A, 1000));
        f.Avoirs = [new Avoir { TotalTTC = 240 }];
        var s = Imputation.Situation([new DalleDue(A, "A", "", 1200m)], [f], [Paiement((A, 1200))], Jour).Single();
        Assert.Equal(240m, s.Avoirs);
        Assert.Equal(240m, s.TropPercu);
        var rembourse = Paiement((A, 240)); rembourse.Remboursement = true;
        Assert.Equal(0m, Imputation.Situation([new DalleDue(A, "A", "", 1200m)], [f], [Paiement((A, 1200)), rembourse], Jour).Single().TropPercu);
    }

    [Fact]
    public void Avoir_total_ne_reduit_pas_le_du_de_la_dalle()
    {
        // La facture annulée rend son bon de livraison facturable : la dalle reste due.
        var f = Livraison(3, (A, 1000));
        f.Avoirs = [new Avoir { TotalTTC = 1200, Total = true }];
        var s = Imputation.Situation([new DalleDue(A, "A", "", 1200m)], [f], [], Jour).Single();
        Assert.Equal(0m, s.Avoirs);
        Assert.Equal(1200m, s.Du);
    }

    [Fact]
    public void Paiement_annule_ignore()
    {
        var p = Paiement((A, 500)); p.Annule = true;
        Assert.Equal(0m, Imputation.Situation([new DalleDue(A, "A", "", 1000m)], [], [p], Jour).Single().Paye);
    }
}
