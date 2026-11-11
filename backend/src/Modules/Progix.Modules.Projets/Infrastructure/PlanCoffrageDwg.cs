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

    [GeneratedRegex(@"\\[A-Za-z][^;]*;|[{}]|\\L|\\P")]
    private static partial Regex Mise();

    /// <summary>Lit l'espace objet du dessin. Les coordonnées sont ramenées au mètre d'après l'unité déclarée dans le fichier.</summary>
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
        var k = doc.Header.InsUnits switch { UnitsType.Millimeters => 0.001, UnitsType.Centimeters => 0.01, _ => 1.0 };
        var plan = new PlanCoffrage();
        foreach (var e in doc.Entities)
        {
            var calque = e.Layer?.Name ?? "0";
            switch (e)
            {
                case LwPolyline p:
                    Ajouter(plan, calque, p.Vertices.Select(v => (v.Location.X * k, v.Location.Y * k)).ToList(), p.IsClosed);
                    break;
                case Polyline2D p:
                    Ajouter(plan, calque, p.Vertices.Select(v => (v.Location.X * k, v.Location.Y * k)).ToList(), p.IsClosed);
                    break;
                case Arc:
                    break;
                case Circle c:
                    plan.Ronds.Add(new RondPlan(calque, c.Center.X * k, c.Center.Y * k, c.Radius * k));
                    break;
                case MText m:
                    plan.Titres.Add(new TitrePlan(m.InsertPoint.X * k, m.InsertPoint.Y * k, Mise().Replace(m.Value ?? "", " ").Trim(), m.Height * k));
                    break;
                case TextEntity t:
                    plan.Titres.Add(new TitrePlan(t.InsertPoint.X * k, t.InsertPoint.Y * k, (t.Value ?? "").Trim(), t.Height * k));
                    break;
            }
        }
        return plan;
    }

    private static void Ajouter(PlanCoffrage plan, string calque, List<(double X, double Y)> sommets, bool fermee)
    {
        if (FondPlan.Polygone(sommets, fermee) is { } polygone) plan.Contours.Add(new ContourPlan(calque, polygone));
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
