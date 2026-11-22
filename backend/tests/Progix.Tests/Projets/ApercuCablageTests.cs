using Progix.Modules.Projets.Domain;
using Xunit;

namespace Progix.Tests.Projets;

public class ApercuCablageTests
{
    private static PlanCablage Plan(string sens, decimal? surface, params (int N, int S, decimal L)[] c)
    {
        var p = new PlanCablage { Nom = $"cablage_{sens}.dxf", Orientation = sens, SurfaceNette = surface };
        foreach (var (n, s, l) in c) p.Cables.Add(new CableDxf(n, s, sens, l, 0.5m, 1));
        return p;
    }

    private static SystemeAdapt Sys(int torons, int cables, int actives) => new() { Torons = torons, Cables = cables, ExtremitesActives = actives };

    [Fact]
    public void Surlongueur_par_extremite_active_et_ancrages_a_deux()
    {
        var a = ApercuCablage.Composer([Plan("X", 100m, (1, 2, 10m), (2, 4, 8m)), Plan("Y", 100m, (3, 2, 5m))], [Sys(2, 2, 3), Sys(4, 1, 1)], 30, 100m);
        Assert.True(a.Importable);
        var l1 = a.Lignes.Single(l => l.Numero == "01");
        // S2 : 3 extrémités actives pour 2 câbles, soit 2 par câble en moyenne arrondie.
        Assert.Equal((2, 0.60m, 10.60m, 2), (l1.Actives, l1.Surlongueur, l1.Longueur, l1.Ancrages));
        Assert.Equal((1, 8.30m), (a.Lignes.Single(l => l.Numero == "02").Actives, a.Lignes.Single(l => l.Numero == "02").Longueur));
        Assert.Empty(a.Avertissements);
        Assert.False(a.SurfaceDifferente);
    }

    [Fact]
    public void Surlongueur_nulle_garde_la_longueur_tracee() =>
        Assert.Equal(10m, ApercuCablage.Composer([Plan("X", null, (1, 2, 10m))], null, 0, 0).Lignes[0].Longueur);

    [Fact]
    public void Numero_present_sur_les_deux_plans_est_bloquant()
    {
        var a = ApercuCablage.Composer([Plan("X", null, (1, 2, 10m)), Plan("Y", null, (1, 2, 5m))], null, 0, 0);
        Assert.False(a.Importable);
        Assert.Contains(a.Erreurs, e => e.Contains("tendon 01"));
    }

    [Fact]
    public void Deux_plans_de_meme_orientation_sont_bloquants()
    {
        var a = ApercuCablage.Composer([Plan("X", null, (1, 2, 10m)), Plan("X", null, (2, 2, 5m))], null, 0, 0);
        Assert.Contains(a.Erreurs, e => e.Contains("orientation X"));
    }

    [Fact]
    public void Erreur_de_lecture_d_un_plan_bloque()
    {
        var p = Plan("X", null, (1, 2, 10m)); p.Erreurs.Add("étiquette sans câble");
        Assert.False(ApercuCablage.Composer([p], null, 0, 0).Importable);
    }

    [Fact]
    public void Ecart_avec_le_PTTotal_et_surface_sont_signales()
    {
        var a = ApercuCablage.Composer([Plan("X", 230.45m, (1, 2, 10m)), Plan("Y", 230.45m, (2, 2, 5m))], [Sys(2, 3, 3)], 0, 228m);
        Assert.Contains(a.Avertissements, w => w.Contains("S2") && w.Contains("2 câbles") && w.Contains("3 dans le PTTotal"));
        Assert.True(a.SurfaceDifferente);
        Assert.Equal(230.45m, a.SurfaceDxf);
    }

    [Fact]
    public void Surlongueur_hors_limites_est_refusee() =>
        Assert.False(ApercuCablage.Composer([Plan("X", null, (1, 2, 10m))], null, 500, 0).Importable);

    private static TendonAdapt T(string e, int torons, decimal l, int actives = 1) => new(e, torons, l, actives);

    [Fact]
    public void Appariement_par_numero_donne_les_extremites_actives_exactes()
    {
        var plans = new[] { Plan("X", null, (5, 2, 10.00m), (6, 2, 12.00m)), Plan("Y", null, (7, 4, 8.00m)) };
        var a = ApercuCablage.Composer(plans, null, 20, 0, [T("6", 2, 12.02m, 2), T("5", 2, 10.00m, 1), T("7", 4, 8.00m, 2)]);
        Assert.True(a.Importable);
        Assert.Empty(a.Avertissements);
        Assert.Equal((1, 0.20m), (a.Lignes.Single(l => l.Numero == "05").Actives, a.Lignes.Single(l => l.Numero == "05").Surlongueur));
        Assert.Equal((2, 0.40m), (a.Lignes.Single(l => l.Numero == "06").Actives, a.Lignes.Single(l => l.Numero == "06").Surlongueur));
    }

    [Fact]
    public void Systeme_different_pour_un_meme_numero_est_bloquant()
    {
        var a = ApercuCablage.Composer([Plan("X", null, (5, 2, 10m)), Plan("Y", null, (7, 4, 8m))], null, 0, 0, [T("5", 4, 10m), T("7", 4, 8m)]);
        Assert.False(a.Importable);
        Assert.Contains(a.Erreurs, e => e.Contains("Tendon 05") && e.Contains("S2") && e.Contains("S4"));
    }

    [Fact]
    public void Longueur_du_PTTotal_reprise_en_priorite_avertissement_au_dela_de_5_cm()
    {
        var a = ApercuCablage.Composer([Plan("X", null, (5, 2, 10m)), Plan("Y", null, (7, 2, 20m))], null, 0, 0, [T("5", 2, 10.03m), T("7", 2, 9m)]);
        Assert.Contains(a.Avertissements, w => w.Contains("Tendon 07") && w.Contains("20,00") || w.Contains("Tendon 07") && w.Contains("20.00"));
        Assert.Equal(9m, a.Lignes.Single(l => l.Numero == "07").Longueur);
        Assert.Equal(10.03m, a.Lignes.Single(l => l.Numero == "05").Longueur);
        Assert.True(a.Importable);
    }

    [Fact]
    public void Tendon_present_d_un_seul_cote_est_signale()
    {
        var a = ApercuCablage.Composer([Plan("X", null, (5, 2, 10m)), Plan("Y", null, (7, 2, 20m))], null, 0, 0, [T("5", 2, 10m), T("9", 2, 31m)]);
        Assert.Contains(a.Avertissements, w => w.Contains("Tendon 07") && w.Contains("absent du PTTotal"));
        Assert.Contains(a.Avertissements, w => w.Contains("Tendon 9 du PTTotal"));
        Assert.True(a.Importable);
    }

    [Fact]
    public void Plans_et_PTTotal_reels_mk4_847_concordent_cable_par_cable()
    {
        var dossier = Environment.GetEnvironmentVariable("PROGIX_DXF");
        if (string.IsNullOrEmpty(dossier) || !File.Exists(Path.Combine(dossier, "PTTotal.rtf"))) return;
        PlanCablage Lire(string motif, string sens) { var f = Directory.GetFiles(dossier, motif).Single(); using var s = File.OpenRead(f); return LectureCablageAdapt.Lire(s, Path.GetFileName(f), sens); }
        var pt = LectureAdapt.Tendons(System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(Path.Combine(dossier, "PTTotal.rtf"))));
        Assert.NotNull(pt);
        var a = ApercuCablage.Composer([Lire("*SUIVANT_X.dxf", "X"), Lire("*SUIVANT_Y.dxf", "Y")], null, 0, 0, pt);
        Assert.True(a.Erreurs.Count == 0, string.Join(" | ", a.Erreurs));
        Assert.Equal(31, a.Lignes.Count);
        Assert.All(a.Lignes, l => Assert.NotNull(l.EtiquettePt));
        // Tous les tendons des deux côtés sont appariés ; les longueurs retenues sont celles du PTTotal.
        Assert.DoesNotContain(a.Avertissements, w => w.Contains("absent"));
        Assert.All(a.Lignes, l => Assert.Equal(Math.Round(pt!.Single(t => int.Parse(t.Etiquette) == int.Parse(l.Numero)).Longueur, 2), l.Tracee));
        Assert.Equal(313.7m, a.Lignes.Sum(l => l.Tracee), 1);
    }
}
