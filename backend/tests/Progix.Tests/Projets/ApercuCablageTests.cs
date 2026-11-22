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
}
