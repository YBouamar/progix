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

    [Fact]
    public void Un_export_se_lit_seul()
    {
        var pt = LectureAdapt.AnalyserUn(Pt());
        Assert.Null(pt.Erreur);
        Assert.Equal(LectureAdapt.Genre.Precontrainte, pt.Genre);
        Assert.Equal(1.74m, pt.Ratio);
        Assert.Equal(1409.58m, pt.Surface);
        Assert.Equal(3, pt.Systemes.Count);

        var rebar = LectureAdapt.AnalyserUn(Rebar());
        Assert.Null(rebar.Erreur);
        Assert.Equal(LectureAdapt.Genre.Armatures, rebar.Genre);
        Assert.Equal(12.85m, rebar.Ratio);
        Assert.Empty(rebar.Systemes);
    }

    [Fact]
    public void Un_export_seul_garde_les_memes_refus()
    {
        Assert.Contains("S5", LectureAdapt.AnalyserUn(Pt(toronsInconnus: 5)).Erreur);
        Assert.Contains("RTF", LectureAdapt.AnalyserUn("PK\u0003\u0004 classeur").Erreur);
        Assert.Contains("ni un PTTotal", LectureAdapt.AnalyserUn(Rtf("AUTRE TABLEAU", "PH", Ligne("a", "b"))).Erreur);
    }

    [Fact]
    public void Chaque_export_ne_touche_que_sa_part()
    {
        // RebarTotal d'abord : ratio passif seul, la surface du plancher vient de lui tant que PTTotal manque.
        var a = LectureAdapt.Reporter(null, LectureAdapt.AnalyserUn(Rebar("1400.00")), "RebarTotal.rtf.doc");
        Assert.False(a.AvecPt);
        Assert.True(a.AvecRebar);
        Assert.Equal(12.85m, a.RatioPassif);
        Assert.Equal(0m, a.RatioActif);
        Assert.Equal(1400.00m, a.SurfacePlancher);

        // PTTotal ensuite : ratio actif, câbles et surface du plancher, sans perdre le ratio passif.
        a.FichierRebarId = Guid.NewGuid();
        var b = LectureAdapt.Reporter(a, LectureAdapt.AnalyserUn(Pt()), "PTTotal.rtf.doc");
        Assert.True(b.AvecPt && b.AvecRebar);
        Assert.Equal(1.74m, b.RatioActif);
        Assert.Equal(12.85m, b.RatioPassif);
        Assert.Equal(1409.58m, b.SurfacePlancher);
        Assert.Equal(a.FichierRebarId, b.FichierRebarId);
        Assert.Equal(3, b.Systemes.Count);

        // Un RebarTotal remplacé ne change ni les câbles ni la surface du plancher lue dans PTTotal.
        var c = LectureAdapt.Reporter(b, LectureAdapt.AnalyserUn(Rebar("1400.00")), "RebarTotal (2).rtf.doc");
        Assert.Equal(1409.58m, c.SurfacePlancher);
        Assert.Equal("RebarTotal (2).rtf.doc", c.FichierRebar);
        Assert.Null(c.FichierRebarId);
        Assert.Equal(3, c.Systemes.Count);
    }

    [Fact]
    public void Ecarts_d_un_export_seul()
    {
        var pt = LectureAdapt.AnalyserUn(Pt());
        Assert.Empty(LectureAdapt.Avertissements(pt, null, 0m));
        Assert.Single(LectureAdapt.Avertissements(pt, null, 1500m));
        var avecRebar = LectureAdapt.Reporter(null, LectureAdapt.AnalyserUn(Rebar("1400.00")), "RebarTotal.rtf.doc");
        Assert.Equal(2, LectureAdapt.Avertissements(pt, avecRebar, 1500m).Count);
    }

    private static RapprochementAdapt.Candidat Dalle(Guid id, decimal surface, decimal? adapt = null, bool pt = false, bool rebar = false) => new(id, surface, adapt, pt, rebar);

    [Fact]
    public void Un_fichier_se_rattache_par_la_surface_seulement_sans_doute()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid();
        // Une seule dalle : c'est elle.
        Assert.Equal(a, RapprochementAdapt.Deviner(999m, [Dalle(a, 0m)]));
        // Surface de la dalle égale à celle du fichier.
        Assert.Equal(b, RapprochementAdapt.Deviner(198.004m, [Dalle(a, 230.45m), Dalle(b, 198.00m)]));
        // Surface du plancher déjà lue pour la dalle : le RebarTotal suit le PTTotal, même si la dalle porte une autre surface.
        Assert.Equal(b, RapprochementAdapt.Deviner(201.30m, [Dalle(a, 230.45m), Dalle(b, 198.00m, 201.30m, pt: true)]));
        // Aucune surface égale, ou deux dalles possibles : on ne devine pas.
        Assert.Null(RapprochementAdapt.Deviner(201.30m, [Dalle(a, 230.45m), Dalle(b, 198.00m)]));
        Assert.Null(RapprochementAdapt.Deviner(198.00m, [Dalle(a, 198.00m), Dalle(b, 198.00m)]));
        Assert.Null(RapprochementAdapt.Deviner(201.30m, [Dalle(a, 201.30m), Dalle(b, 198.00m, 201.30m, pt: true)]));
        Assert.Null(RapprochementAdapt.Deviner(150m, [Dalle(a, 0m), Dalle(c, 0m)]));
    }

    [Fact]
    public void Dans_le_doute_la_dalle_proposee_est_celle_a_qui_l_export_manque_puis_la_plus_proche()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();
        Assert.Equal(b, RapprochementAdapt.Proposer(201.30m, LectureAdapt.Genre.Armatures, [Dalle(a, 230.45m), Dalle(b, 198.00m)]));
        // La dalle la plus proche a déjà son RebarTotal : l'autre est proposée.
        Assert.Equal(a, RapprochementAdapt.Proposer(201.30m, LectureAdapt.Genre.Armatures, [Dalle(a, 230.45m), Dalle(b, 198.00m, 198.00m, rebar: true)]));
    }
}
