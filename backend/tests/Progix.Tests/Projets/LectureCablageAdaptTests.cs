using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using Progix.Modules.Projets.Domain;

namespace Progix.Tests.Projets;

/// <summary>Lecture des plans de câblage ADAPT en DXF : câbles, longueurs, surface, contrôles.</summary>
public class LectureCablageAdaptTests
{
    private const string CalqueX = "CABLAGE_TX_SUIVANT_X_LYR_Current_plane";

    /// <summary>Construit un DXF minimal qui reproduit la structure d'un export ADAPT.</summary>
    private sealed class Dessin
    {
        public CadDocument Doc { get; } = new();
        private readonly string _prefixe;
        public Dessin(string prefixe = CalqueX) { _prefixe = prefixe; }

        private Layer Calque(string suffixe)
        {
            var nom = suffixe.Length == 0 ? _prefixe : $"{_prefixe}_{suffixe}";
            if (Doc.Layers.TryGetValue(nom, out var l)) return l;
            l = new Layer(nom); Doc.Layers.Add(l); return l;
        }

        /// <summary>Un tronçon de câble en POLYLINE 3D, de (x1, y1) à (x2, y2), à la hauteur z.</summary>
        public void Troncon3D(double x1, double y1, double x2, double y2, double z1 = 0.1, double z2 = 0.1)
        {
            var p = new Polyline3D { Layer = Calque("Tendon") };
            p.Vertices.Add(new Vertex3D { Location = new XYZ(x1, y1, z1) });
            p.Vertices.Add(new Vertex3D { Location = new XYZ(x2, y2, z2) });
            Doc.Entities.Add(p);
        }

        public void Chaise(double x, double y)
        {
            var p = new LwPolyline { Layer = Calque("Tendon") };
            p.Vertices.Add(new LwPolyline.Vertex(new XY(x, y))); p.Vertices.Add(new LwPolyline.Vertex(new XY(x + 0.3, y)));
            Doc.Entities.Add(p);
        }

        public void Etiquette(int numero, int torons, double x, double y, string texte = "")
            => Doc.Entities.Add(new MText { Layer = Calque("Tendon"), Value = texte.Length > 0 ? texte : $"\\FSTANDARD.shx;#{numero};S={torons}", InsertPoint = new XYZ(x, y, 0) });

        public void Hauteur(double x, double y) => Doc.Entities.Add(new MText { Layer = Calque("Tendon"), Value = "100", InsertPoint = new XYZ(x, y, 0) });

        public void Rectangle(string calque, double x, double y, double l, double h)
        {
            var p = new LwPolyline { Layer = Calque(calque), IsClosed = true };
            foreach (var (a, b) in new[] { (x, y), (x + l, y), (x + l, y + h), (x, y + h) }) p.Vertices.Add(new LwPolyline.Vertex(new XY(a, b)));
            Doc.Entities.Add(p);
        }

        public void Cartouche(params string[] textes)
        {
            var c = new Layer("CARTOUCHE-TEX"); Doc.Layers.Add(c);
            foreach (var t in textes) Doc.Entities.Add(new MText { Layer = c, Value = t, InsertPoint = new XYZ(0, 0, 0) });
        }

        public MemoryStream Flux()
        {
            var m = new MemoryStream();
            using (var w = new DxfWriter(m, Doc, false)) { w.OnNotification += (_, _) => { }; w.Write(); }
            return new MemoryStream(m.ToArray());
        }
    }

    /// <summary>
    /// Plan type : câble 5 (S2) d'un seul tenant de 10 m ; câble 6 (S1) en deux morceaux alignés (4 m + 3 m) séparés par un vide ;
    /// câble 7 (S1) en deux tronçons qui se touchent (3 m + 3 m, le second dessiné à l'envers).
    /// </summary>
    private static Dessin Modele()
    {
        var d = new Dessin();
        d.Troncon3D(0, 0, 10, 0);                 // câble 5
        d.Troncon3D(0, 5, 4, 5);                  // câble 6, morceau 1
        d.Troncon3D(9, 5, 12, 5);                 // câble 6, morceau 2 (vide de 5 m, même Y)
        d.Troncon3D(0, 10, 3, 10);                // câble 7, tronçon 1
        d.Troncon3D(6, 10, 3, 10);                // câble 7, tronçon 2 inversé, touche à (3 ; 10)
        d.Chaise(1, 0.2); d.Chaise(5, 5.2);       // repères de chaises : ignorés
        d.Etiquette(5, 2, 0.5, 1.5); d.Etiquette(6, 1, 0.5, 6.5); d.Etiquette(7, 1, 0.5, 11.5);
        d.Hauteur(2, 0.5);                        // hauteur de profil : ignorée
        return d;
    }

    private static PlanCablage Lire(Dessin d, string nom = "PLAN_DE_CABLAGE_SUIVANT_X.dxf", string? orientation = null) => LectureCablageAdapt.Lire(d.Flux(), nom, orientation);

    [Fact]
    public void Cables_longueurs_et_torons()
    {
        var p = Lire(Modele());
        Assert.Empty(p.Erreurs);
        Assert.Equal("X", p.Orientation);
        Assert.True(p.OrientationDetectee);
        Assert.Collection(p.Cables,
            c => { Assert.Equal(5, c.Numero); Assert.Equal(2, c.Torons); Assert.Equal(10m, c.Longueur); Assert.Equal(1, c.Troncons); },
            c => { Assert.Equal(6, c.Numero); Assert.Equal(1, c.Torons); Assert.Equal(7m, c.Longueur); Assert.Equal(2, c.Troncons); },
            c => { Assert.Equal(7, c.Numero); Assert.Equal(1, c.Torons); Assert.Equal(6m, c.Longueur); });
    }

    [Fact]
    public void Longueur_3D_pour_un_troncon_en_pente()
    {
        var d = new Dessin();
        d.Troncon3D(0, 0, 3, 0, 0, 4);            // 3 m en plan, 4 m de dénivelé : 5 m
        d.Etiquette(1, 1, 0.5, 1);
        Assert.Equal(5m, Lire(d).Cables.Single().Longueur);
    }

    [Fact]
    public void Etiquette_sans_cable_est_bloquante()
    {
        var d = Modele(); d.Etiquette(99, 1, 40, 40);
        Assert.Contains(Lire(d).Erreurs, e => e.Contains("#99") && e.Contains("sans câble"));
    }

    [Fact]
    public void Cable_sans_etiquette_est_bloquant()
    {
        var d = Modele(); d.Troncon3D(0, 30, 8, 31);
        Assert.Contains(Lire(d).Erreurs, e => e.Contains("Câble sans étiquette"));
    }

    [Fact]
    public void Numero_en_double_dans_un_plan_est_bloquant()
    {
        var d = Modele(); d.Troncon3D(0, 20, 8, 20); d.Etiquette(5, 2, 0.5, 21.5);
        Assert.Contains(Lire(d).Erreurs, e => e.Contains("Le tendon 05 figure 2 fois"));
    }

    [Fact]
    public void Distance_et_longueur_hors_normes_sont_des_avertissements()
    {
        var d = new Dessin();
        d.Troncon3D(0, 0, 0.5, 0);                // 0,5 m : trop court
        d.Etiquette(1, 1, 0.2, 3.9);              // étiquette à 3,9 m : trop loin
        var p = Lire(d);
        Assert.Empty(p.Erreurs);
        Assert.Contains(p.Avertissements, a => a.Contains("plus de 3 m"));
        Assert.Contains(p.Avertissements, a => a.Contains("inférieure à 1 m"));
    }

    [Fact]
    public void Fichier_sans_calque_tendon_n_est_pas_un_export_adapt()
    {
        var d = new Dessin(); d.Rectangle("Slab_Region", 0, 0, 10, 10);
        var p = Lire(new Dessin("AUTRE") { }, "plan.dxf");
        Assert.Contains(p.Erreurs, e => e.Contains("ne semble pas être un export ADAPT"));
    }

    [Fact]
    public void Dwg_est_refuse_avec_la_marche_a_suivre()
    {
        var p = LectureCablageAdapt.Lire(new MemoryStream("AC1015\0\0"u8.ToArray()), "plan.dwg");
        Assert.Contains(p.Erreurs, e => e.Contains("DWG") && e.Contains("DXF"));
        var p2 = LectureCablageAdapt.Lire(new MemoryStream("AC1015\0\0"u8.ToArray()), "plan.dxf");
        Assert.Contains(p2.Erreurs, e => e.Contains("DWG"));
    }

    [Fact]
    public void Orientation_inconnue_se_choisit()
    {
        var d = new Dessin("PLAN_QUELCONQUE"); d.Troncon3D(0, 0, 10, 0); d.Etiquette(1, 1, 0.5, 1);
        var sans = Lire(d, "plan.dxf");
        Assert.Equal("", sans.Orientation);
        Assert.Contains(sans.Erreurs, e => e.Contains("orientation"));
        var d2 = new Dessin("PLAN_QUELCONQUE"); d2.Troncon3D(0, 0, 10, 0); d2.Etiquette(1, 1, 0.5, 1);
        var avec = Lire(d2, "plan.dxf", "Y");
        Assert.Equal("Y", avec.Orientation);
        Assert.False(avec.OrientationDetectee);
        Assert.Empty(avec.Erreurs);
    }

    [Fact]
    public void Surface_nette_des_ouvertures_et_cartouche()
    {
        var d = Modele();
        d.Rectangle("Slab_Region", 0, 0, 20, 10);   // 200 m²
        d.Rectangle("Opening", 2, 2, 2, 3);         // 6 m²
        d.Rectangle("Opening", 10, 2, 1, 1);        // 1 m²
        d.Cartouche("PH- RDC", "REF:Mk4-847-HY-10-26", "PLAN DE CABLAGE SUIVANT X");
        var p = Lire(d);
        Assert.Equal(200m, p.SurfaceBrute);
        Assert.Equal(193m, p.SurfaceNette);
        Assert.Equal("Mk4-847-HY-10-26", p.Reference);
        Assert.Equal("PLAN DE CABLAGE SUIVANT X", p.Titre);
        Assert.Equal("PH- RDC", p.Niveau);
    }

    /// <summary>Plans réels de démonstration (non versionnés) : dossier donné par PROGIX_DXF, sinon le test est ignoré.</summary>
    [Fact]
    public void Plans_reels_mk4_847()
    {
        var dossier = Environment.GetEnvironmentVariable("PROGIX_DXF");
        if (string.IsNullOrEmpty(dossier) || !Directory.Exists(dossier)) return;
        PlanCablage Plan(string motif) { var f = Directory.GetFiles(dossier, motif).Single(); using var s = File.OpenRead(f); return LectureCablageAdapt.Lire(s, Path.GetFileName(f)); }
        var (x, y) = (Plan("*SUIVANT_X.dxf"), Plan("*SUIVANT_Y.dxf"));
        Assert.Empty(x.Erreurs); Assert.Empty(y.Erreurs);
        decimal Somme(PlanCablage p, int torons) => p.Cables.Where(c => c.Torons == torons).Sum(c => c.Longueur);
        Assert.Equal(22, x.Cables.Count); Assert.Equal(14, x.Cables.Count(c => c.Torons == 1)); Assert.Equal(6, x.Cables.Count(c => c.Torons == 2)); Assert.Equal(2, x.Cables.Count(c => c.Torons == 4));
        Assert.Equal(138.21m, Somme(x, 1), 1); Assert.Equal(46.49m, Somme(x, 2), 1); Assert.Equal(17.09m, Somme(x, 4), 1);
        Assert.Equal(9, y.Cables.Count); Assert.Equal(100.82m, Somme(y, 1), 1); Assert.Equal(11.10m, Somme(y, 2), 1);
        Assert.Equal(313.72m, x.Cables.Concat(y.Cables).Sum(c => c.Longueur), 1);
        Assert.Equal(6.99m, x.Cables.Single(c => c.Numero == 5).Longueur, 2);
        Assert.Equal(20.85m, x.Cables.Single(c => c.Numero == 15).Longueur, 2);
        Assert.Equal(13.54m, y.Cables.Single(c => c.Numero == 25).Longueur, 2);
        Assert.Equal(230.45m, x.SurfaceNette!.Value, 2);
        Assert.Equal("Mk4-847-HY-10-26", x.Reference);
    }
}
