using Progix.Modules.Projets.Domain;
using static Progix.Modules.Projets.Domain.EvaluationDessinateurs;

namespace Progix.Tests.Projets;

/// <summary>Évaluation des dessinateurs : mêmes résultats que la feuille « Évaluation » du classeur d'origine (valeurs 2026).</summary>
public class EvaluationDessinateursTests
{
    private static readonly ParametresEvaluation P = new();
    private static readonly DateOnly Jour = new(2026, 10, 31);

    private static Personne Pers(string nom, decimal mois, decimal nbModif, decimal sModif, decimal nbExe, decimal sExe, bool partie = false) =>
        new(Guid.NewGuid(), nom, partie, null, null, mois, nbModif, sModif, nbExe, sExe);

    // Les onze personnes en cours et trois personnes parties du classeur, désignées par une lettre.
    private static readonly Personne[] Classeur =
    [
        Pers("A", 2, 18, 7471.38m, 40, 21683.97m), Pers("B", 10, 61, 22291.34m, 143, 109414.61m), Pers("C", 5, 37, 18648.65m, 94, 36654.14m),
        Pers("D", 6, 49, 21563.63m, 100, 33271.09m), Pers("E", 5, 36, 16808.83m, 88, 28230.02m), Pers("F", 10, 74, 56461.47m, 115, 41063.97m),
        Pers("G", 3, 22, 1393.75m, 52, 22489.56m), Pers("H", 10, 78, 36408.74m, 121, 47283.47m), Pers("I", 10, 47, 16474.09m, 117, 57441.69m),
        Pers("J", 1, 4, 1874.64m, 10, 2581.78m), Pers("K", 1, 3, 860.44m, 9, 2415.21m),
        Pers("X", 6.3m, 71, 20204.21m, 88, 28233.55m, partie: true), Pers("Y", 3, 29, 1488.25m, 37, 9736.07m, partie: true), Pers("Z", 3, 11, 5040.56m, 48, 1651.01m, partie: true),
    ];

    [Fact]
    public void Medianes_des_personnes_en_cours()
    {
        var r = Calculer(Classeur, P);
        Assert.Equal(0.887m, Math.Round(r.MedianeDossiers!.Value, 3));
        Assert.Equal(391.642m, Math.Round(r.MedianeSurface!.Value, 3));
    }

    [Theory]
    [InlineData("A", 1.579, 1, Excellent)]
    [InlineData("B", 1.370, 2, Excellent)]
    [InlineData("C", 1.239, 3, Bon)]
    [InlineData("D", 1.055, 4, Bon)]
    [InlineData("F", 1.051, 5, Bon)]
    [InlineData("E", 1.043, 6, Bon)]
    [InlineData("G", 0.949, 7, Bon)]
    [InlineData("H", 0.938, 8, Bon)]
    [InlineData("I", 0.817, 9, Moyen)]
    public void Score_rang_et_niveau_du_classeur(string nom, double score, int rang, string niveau)
    {
        var l = Calculer(Classeur, P).EnCours.Single(x => x.P.Nom == nom);
        Assert.Equal((decimal)score, Math.Round(l.Score!.Value, 3));
        Assert.Equal(rang, l.Rang);
        Assert.Equal(niveau, l.Niveau);
    }

    [Fact]
    public void Trop_recentes_notees_mais_pas_classees_et_en_fin_de_classement()
    {
        var r = Calculer(Classeur, P);
        var j = r.EnCours.Single(x => x.P.Nom == "J");
        Assert.Equal(0.533m, Math.Round(j.Score!.Value, 3));
        Assert.Null(j.Rang);
        Assert.Equal(TropRecente, j.Niveau);
        Assert.Equal(["A", "B", "C", "D", "F", "E", "G", "H", "I", "J", "K"], r.EnCours.Select(x => x.P.Nom));
    }

    [Fact]
    public void Parties_comparees_a_la_mediane_des_personnes_en_cours_et_classees_entre_elles()
    {
        var r = Calculer(Classeur, P);
        var x = r.Parties.Single(l => l.P.Nom == "X");
        Assert.Equal(144.9m, x.Jours);
        Assert.Equal(0.930m, Math.Round(x.Score!.Value, 3));
        Assert.Equal(1, x.Rang);
        Assert.Equal(Bon, x.Niveau);
        Assert.Equal(ARenforcer, r.Parties.Single(l => l.P.Nom == "Y").Niveau);
        Assert.Equal(3, r.Parties.Single(l => l.P.Nom == "Z").Rang);
    }

    [Fact]
    public void Rendement_par_jour()
    {
        var b = Calculer(Classeur, P).EnCours.Single(x => x.P.Nom == "B");
        Assert.Equal(230m, b.Jours);
        Assert.Equal(0.887m, Math.Round(b.DossiersParJour!.Value, 3));
        Assert.Equal(572.635m, Math.Round(b.SurfaceParJour!.Value, 3));
        Assert.Equal(645.617m, Math.Round(b.SurfaceParDossier!.Value, 3));
    }

    [Fact]
    public void Sans_date_d_arrivee_la_ligne_n_est_pas_calculee()
    {
        var sans = new Personne(Guid.NewGuid(), "S", false, null, null, null, 2, 500, 3, 900);
        var l = Calculer([.. Classeur, sans], P).EnCours.Last();
        Assert.Equal("S", l.P.Nom);
        Assert.Null(l.Score);
        Assert.Null(l.Niveau);
        Assert.Equal(280m, l.SurfaceParDossier);
        // Elle n'entre pas dans les médianes.
        Assert.Equal(391.642m, Math.Round(Calculer([.. Classeur, sans], P).MedianeSurface!.Value, 3));
    }

    [Fact]
    public void Mois_de_presence_au_dixieme()
    {
        var janvier = new DateOnly(2025, 3, 1);
        // Présente toute l'année jusqu'à aujourd'hui.
        Assert.Equal(10m, Mois(janvier, null, 2026, Jour));
        // Départs du classeur : 8 juillet 6,3 mois ; 1er juillet 6 ; 1er avril 3.
        Assert.Equal(6.3m, Mois(janvier, new DateOnly(2026, 7, 8), 2026, Jour));
        Assert.Equal(6m, Mois(janvier, new DateOnly(2026, 7, 1), 2026, Jour));
        Assert.Equal(3m, Mois(janvier, new DateOnly(2026, 4, 1), 2026, Jour));
        // Arrivée en cours d'année.
        Assert.Equal(2m, Mois(new DateOnly(2026, 9, 1), null, 2026, Jour));
        // Année passée entière, année avant l'arrivée, date d'arrivée inconnue.
        Assert.Equal(12m, Mois(janvier.AddYears(-1), null, 2025, Jour));
        Assert.Equal(0m, Mois(new DateOnly(2026, 9, 1), null, 2025, Jour));
        Assert.Null(Mois(null, null, 2026, Jour));
    }

    [Fact]
    public void Reglages_refuses()
    {
        Assert.Null(new ParametresEvaluation().Refus());
        Assert.NotNull(new ParametresEvaluation { PoidsSurface = 0.7m }.Refus());
        Assert.NotNull(new ParametresEvaluation { SeuilBon = 1.3m }.Refus());
        Assert.NotNull(new ParametresEvaluation { JoursParMois = 0 }.Refus());
    }
}
