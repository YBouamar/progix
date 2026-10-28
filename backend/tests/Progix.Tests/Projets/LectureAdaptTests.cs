using Progix.Modules.Projets.Domain;

namespace Progix.Tests.Projets;

/// <summary>Lecture des exports ADAPT (PTTotal, RebarTotal) : ratios, types d'ancrage, ancrages et gaines.</summary>
public class LectureAdaptTests
{
    // Reproduit la structure des exports RTF d'ADAPT : en-tête de page, titre, tableaux cellule par cellule.
    private static string Ligne(params string[] cellules) =>
        @"\trowd\trgaph10\cellx748\cellx2152" + string.Concat(cellules.Select(c => $@"\intbl {c}\cell")) + @"\intbl\row\pard";

    private static string Rtf(string titre, string plancher, params string[] lignes) =>
        @"{\rtf1\ansi\ansicpg1252{\fonttbl{\f0 Arial;}}{\header\pard\plain\b Project Name: SOHAUS\tab Specific Data: " + plancher
        + @"\par File Name: essai.adm\tab FLOOR-PRO 2019\par}{\footer\pard RISA Tech\par}\pard\plain " + titre + @"\par " + string.Concat(lignes) + "}";

    private static string Resume(string surface, string ratio) => Ligne("Reference Plane", "Floor Area", "Floor Volume", "Weight", "Rate", "Rate", "Cost")
        + Ligne("", "m2", "m3", "kg", "kg/m2", "kg/m3", "Euro")
        + Ligne("Current plane", surface, "317.98", "2456.20", ratio, "7.72", "9824.81")
        + Ligne("Total", surface, "317.98", "2456.20", ratio, "7.72", "9824.81");

    private static string Cable(int n, string plan, string duct, int torons, string toron) =>
        Ligne(n.ToString(), $"Tendon {n}", plan, duct, "150", torons.ToString(), toron, "32", "1");

    private static string Pt(string plancher = "PH-2EME", int toronsInconnus = 0) => Rtf("300.00 PT TOTALS", plancher,
        Ligne("#", "Label", "Reference Plane", "Tendon/Duct Length", "Strand Area", "Number Of Strands", "Strand Length", "Tendon Diameter", "Stressing ends"),
        Ligne("", "", "", "m", "mm2", "", "m", "mm", ""),
        Cable(1, "Current plane", "21.380", 1, "21.380"),
        Cable(2, "Current plane", "10.000", 1, "10.000"),
        Cable(3, "Current plane", "13.939", 2, "27.878"),
        Cable(4, "Current plane", "10.346", 3, "31.037"),
        Cable(5, "Other plane", "99.000", 1, "99.000"),
        toronsInconnus > 0 ? Cable(6, "Current plane", "5.000", toronsInconnus, "25.000") : "",
        @"Summary by Type\par ",
        Ligne("Type #", "Tendon Diameter", "Strand Area"),
        @"Summary\par ",
        Resume("1409.58", "1.74"));

    private static string Rebar(string surface = "1409.58", string plancher = "PH-2EME") => Rtf("210.00 REBAR TOTALS", plancher,
        Ligne("Type", "Quantity", "Size", "Length/bar", "Length", "Weight"),
        Ligne("1", "59", "8mm", "44.35", "2616.65", "1025.39"),
        @"Summary\par ",
        Resume(surface, "12.85"));

    [Fact]
    public void Ratios_et_cables_par_type_d_ancrage()
    {
        var r = LectureAdapt.Analyser(Pt(), Rebar());
        Assert.Null(r.Erreur);
        var a = r.Donnees!;
        Assert.Equal(1.74m, a.RatioActif);
        Assert.Equal(12.85m, a.RatioPassif);
        Assert.Equal(1409.58m, a.SurfacePlancher);
        Assert.Equal("SOHAUS · PH-2EME · essai.adm", a.Plancher);
        // Le câble d'un autre plan est ignoré ; ancrages = nombre de lignes, gaine = cumul de Strand Length.
        Assert.Collection(a.Systemes,
            s => { Assert.Equal(1, s.Torons); Assert.Equal(2, s.Cables); Assert.Equal(31.38m, s.LongueurTorons); Assert.Equal(31.38m, s.LongueurGaines); },
            s => { Assert.Equal(2, s.Torons); Assert.Equal(1, s.Cables); Assert.Equal(27.88m, s.LongueurTorons); Assert.Equal(13.94m, s.LongueurGaines); },
            s => { Assert.Equal(3, s.Torons); Assert.Equal(1, s.Cables); Assert.Equal(31.04m, s.LongueurTorons); Assert.Equal(10.35m, s.LongueurGaines); });
    }

    [Fact]
    public void Les_fichiers_se_deposent_dans_n_importe_quel_ordre() =>
        Assert.Equal(1.74m, LectureAdapt.Analyser(Rebar(), Pt()).Donnees!.RatioActif);

    [Fact]
    public void Deux_fois_le_meme_export_est_refuse() =>
        Assert.Contains("PTTotal (PT TOTALS) et RebarTotal", LectureAdapt.Analyser(Pt(), Pt()).Erreur);

    [Fact]
    public void Type_d_ancrage_inconnu_refuse() =>
        Assert.Contains("S5", LectureAdapt.Analyser(Pt(toronsInconnus: 5), Rebar()).Erreur);

    [Fact]
    public void Ecarts_signales_sans_bloquer()
    {
        var a = LectureAdapt.Analyser(Pt(), Rebar("1400.00", "PH-3EME")).Donnees!;
        var l = LectureAdapt.Avertissements(Pt(), Rebar("1400.00", "PH-3EME"), a, 1500m);
        Assert.Equal(3, l.Count);
        Assert.Empty(LectureAdapt.Avertissements(Pt(), Rebar(), a, 1409.58m));
        // Dalle sans surface : pas d'écart, la surface du plancher sera reprise.
        Assert.Empty(LectureAdapt.Avertissements(Pt(), Rebar(), a, 0m));
    }
}
