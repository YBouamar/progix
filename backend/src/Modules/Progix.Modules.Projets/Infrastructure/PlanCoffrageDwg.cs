using System.Text.RegularExpressions;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using CSMath;
using Progix.Modules.Projets.Application;

namespace Progix.Modules.Projets.Infrastructure;

/// <summary>Lecture d'un plan de coffrage AutoCAD (DWG ou DXF) et écriture du fond de plan en DXF, sans AutoCAD.</summary>
public static partial class PlanCoffrageDwg
{
    public const string CalqueDalle = "ADAPT-DALLE";
    public const string CalquePoteaux = "ADAPT-POTEAUX";
    public const string CalqueVoiles = "ADAPT-VOILE";
    public const string CalqueReservations = "ADAPT-RESERVATION";

    // Codes de mise en forme d'un texte multiligne : sans paramètre (souligné, surligné, barré, saut de ligne) ou terminés par un point-virgule.
    [GeneratedRegex(@"\\[LlOoKkP~]|\\[A-Za-z][^;\\]*;|[{}]")]
    private static partial Regex Mise();

    /// <summary>
    /// Lit l'espace objet du dessin, blocs compris (poteaux et réservations posés en blocs, seuls leurs éléments visibles).
    /// Les coordonnées sont ramenées au mètre : l'unité déclarée dans le fichier sert si elle donne un bâtiment plausible,
    /// sinon elle se déduit des dimensions du dessin (voir <see cref="FondPlan.Echelle"/>).
    /// </summary>
    public static PlanCoffrage Lire(Stream contenu, string nom)
    {
        CadDocument doc;
        if (nom.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase))
        {
            using var lecteur = new DxfReader(contenu);
            doc = lecteur.Read();
        }
        else
        {
            using var lecteur = new DwgReader(contenu);
            doc = lecteur.Read();
        }
        var brut = new Brut();
        foreach (var e in doc.Entities) Lire(e, Repere.Identite, null, 0, brut);

        var declaree = doc.Header.InsUnits switch { UnitsType.Millimeters => 0.001, UnitsType.Centimeters => 0.01, _ => 1.0 };
        var k = FondPlan.Echelle(declaree,
            brut.Contours.Select(c => Math.Max(c.Sommets.Max(s => s.X) - c.Sommets.Min(s => s.X), c.Sommets.Max(s => s.Y) - c.Sommets.Min(s => s.Y))).ToList(),
            brut.Contours.Where(c => c.Fermee && c.Sommets.Count == 4).Select(c => Math.Min(Cote(c.Sommets[0], c.Sommets[1]), Cote(c.Sommets[1], c.Sommets[2]))).ToList());

        var plan = new PlanCoffrage();
        foreach (var c in brut.Contours)
            if (FondPlan.Polygone(c.Sommets.Select(s => (s.X * k, s.Y * k)).ToList(), c.Fermee) is { } polygone) plan.Contours.Add(new ContourPlan(c.Calque, polygone));
        plan.Ronds.AddRange(brut.Ronds.Select(r => r with { X = r.X * k, Y = r.Y * k, Rayon = r.Rayon * k }));
        plan.Titres.AddRange(brut.Titres.Select(t => t with { X = t.X * k, Y = t.Y * k, Hauteur = t.Hauteur * k }));
        return plan;
    }

    private static double Cote((double X, double Y) a, (double X, double Y) b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    /// <summary>Éléments lus, dans l'unité du dessin.</summary>
    private sealed class Brut
    {
        public List<(string Calque, List<(double X, double Y)> Sommets, bool Fermee)> Contours { get; } = [];
        public List<RondPlan> Ronds { get; } = [];
        public List<TitrePlan> Titres { get; } = [];
    }

    /// <summary>Position d'un bloc inséré : point de base, échelles, rotation, point d'insertion. Les repères s'enchaînent pour les blocs imbriqués.</summary>
    private sealed record Repere(double A, double B, double C, double D, double Tx, double Ty)
    {
        public static readonly Repere Identite = new(1, 0, 0, 1, 0, 0);
        public (double X, double Y) Point(double x, double y) => (A * x + B * y + Tx, C * x + D * y + Ty);
        public double Facteur => Math.Sqrt(Math.Abs(A * D - B * C));

        public Repere Puis(Insert i)
        {
            var (cos, sin) = (Math.Cos(i.Rotation), Math.Sin(i.Rotation));
            var (bx, by) = (i.Block.BlockEntity.BasePoint.X, i.Block.BlockEntity.BasePoint.Y);
            // Local : (p - base) mis à l'échelle, tourné, puis posé au point d'insertion.
            var (a, b, c, d) = (cos * i.XScale, -sin * i.YScale, sin * i.XScale, cos * i.YScale);
            var (tx, ty) = (i.InsertPoint.X - a * bx - b * by, i.InsertPoint.Y - c * bx - d * by);
            return new Repere(A * a + B * c, A * b + B * d, C * a + D * c, C * b + D * d, A * tx + B * ty + Tx, C * tx + D * ty + Ty);
        }
    }

    private static void Lire(Entity e, Repere repere, string? calqueDuBloc, int profondeur, Brut brut)
    {
        if (e.IsInvisible) return;
        // Dans un bloc, le calque « 0 » prend celui de l'insertion.
        var calque = e.Layer?.Name is { } n && !(n == "0" && calqueDuBloc is not null) ? n : calqueDuBloc ?? "0";
        switch (e)
        {
            case Insert i when profondeur < 4 && i.Block is not null:
                var suite = repere.Puis(i);
                foreach (var enfant in i.Block.Entities) Lire(enfant, suite, i.Layer?.Name ?? calqueDuBloc, profondeur + 1, brut);
                break;
            case LwPolyline p when p.Vertices.Count >= 2:
                brut.Contours.Add((calque, p.Vertices.Select(v => repere.Point(v.Location.X, v.Location.Y)).ToList(), p.IsClosed));
                break;
            case Polyline2D p when p.Vertices.Count() >= 2:
                brut.Contours.Add((calque, p.Vertices.Select(v => repere.Point(v.Location.X, v.Location.Y)).ToList(), p.IsClosed));
                break;
            case Arc:
                break;
            case Circle c:
                var (cx, cy) = repere.Point(c.Center.X, c.Center.Y);
                brut.Ronds.Add(new RondPlan(calque, cx, cy, c.Radius * repere.Facteur));
                break;
            case MText m when profondeur == 0:
                brut.Titres.Add(new TitrePlan(m.InsertPoint.X, m.InsertPoint.Y, Mise().Replace(m.Value ?? "", " ").Trim(), m.Height));
                break;
            case TextEntity t when profondeur == 0:
                brut.Titres.Add(new TitrePlan(t.InsertPoint.X, t.InsertPoint.Y, (t.Value ?? "").Trim(), t.Height));
                break;
        }
    }

    /// <summary>Écrit le fond de plan d'un niveau : quatre calques, en mètres, le coin bas gauche de la dalle à l'origine.</summary>
    public static void Ecrire(FondPlanNiveau fond, Stream sortie)
    {
        var doc = new CadDocument();
        doc.Header.InsUnits = UnitsType.Meters;
        Layer Calque(string nom, short couleur) { var c = new Layer(nom) { Color = new Color(couleur) }; doc.Layers.Add(c); return c; }
        var (dalle, poteaux, voiles, reservations) = (Calque(CalqueDalle, 7), Calque(CalquePoteaux, 1), Calque(CalqueVoiles, 5), Calque(CalqueReservations, 3));
        var (ox, oy) = (fond.Dalle.EnvelopeInternal.MinX, fond.Dalle.EnvelopeInternal.MinY);

        void Contour(NetTopologySuite.Geometries.Polygon p, Layer calque)
        {
            var pl = new LwPolyline { IsClosed = true, Layer = calque };
            foreach (var c in p.ExteriorRing.Coordinates.SkipLast(1)) pl.Vertices.Add(new LwPolyline.Vertex(new XY(c.X - ox, c.Y - oy)));
            doc.Entities.Add(pl);
        }

        Contour(fond.Dalle, dalle);
        foreach (var p in fond.Poteaux) Contour(p, poteaux);
        foreach (var r in fond.Ronds) doc.Entities.Add(new Circle { Center = new XYZ(r.X - ox, r.Y - oy, 0), Radius = r.Rayon, Layer = poteaux });
        foreach (var p in fond.Voiles) Contour(p, voiles);
        foreach (var p in fond.Reservations) Contour(p, reservations);

        using var ecrivain = new DxfWriter(sortie, doc, false);
        ecrivain.Write();
    }
}
