using System.Text.RegularExpressions;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Simplify;

namespace Progix.Modules.Projets.Application;

/// <summary>Contour d'un plan de coffrage : une polyligne et son calque. Les coordonnées sont en mètres.</summary>
public sealed record ContourPlan(string Calque, Polygon Polygone);

/// <summary>Cercle d'un plan de coffrage (poteau rond).</summary>
public sealed record RondPlan(string Calque, double X, double Y, double Rayon);

/// <summary>Texte d'un plan de coffrage, pour retrouver le nom d'un niveau.</summary>
public sealed record TitrePlan(double X, double Y, string Texte, double Hauteur);

/// <summary>Plan de coffrage lu : ce que le fond de plan en retient.</summary>
public sealed class PlanCoffrage
{
    public List<ContourPlan> Contours { get; } = [];
    public List<RondPlan> Ronds { get; } = [];
    public List<TitrePlan> Titres { get; } = [];
}

/// <summary>Niveau détecté dans le plan : son nom, sa zone et son repère (centre de la zone).</summary>
public sealed record NiveauPlan(string Nom, Envelope Zone)
{
    public double X => Zone.Centre.X;
    public double Y => Zone.Centre.Y;
}

/// <summary>Fond de plan d'un niveau : la dalle, ses poteaux, ses voiles et ses réservations.</summary>
public sealed record FondPlanNiveau(Polygon Dalle, IReadOnlyList<Polygon> Poteaux, IReadOnlyList<RondPlan> Ronds, IReadOnlyList<Polygon> Voiles, IReadOnlyList<Polygon> Reservations)
{
    public int Appuis => Poteaux.Count + Ronds.Count + Voiles.Count;
}

/// <summary>
/// Fond de plan pour ADAPT tiré d'un plan de coffrage. Chaque calque du plan reçoit un rôle (dalle, poteaux, voiles, réservations
/// ou ignoré) : le rôle est proposé d'après le nom du calque, puis confirmé ou corrigé par l'ingénieur. L'extraction suit ces rôles.
/// </summary>
public static class FondPlan
{
    public const string Dalle = "dalle";
    public const string Poteaux = "poteaux";
    public const string Voiles = "voiles";
    public const string Reservations = "reservations";
    public const string Ignore = "";

    public static readonly (string Code, string Label)[] Roles =
        [(Dalle, "Dalle"), (Poteaux, "Poteaux"), (Voiles, "Voiles"), (Reservations, "Réservations"), (Ignore, "Ignoré")];

    private static readonly GeometryFactory Fabrique = new();
    private static readonly Regex RePoteau = new("pot[e]?au|^pot", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReVoile = new("voile|hachure", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReDalle = new("coffrage|dalle", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReVide = new("poutre|vide|tr[eé]mie|r[eé]serv", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReNiveau = new(@"\bPH\b|MEZZ|[EÉ]TAGE|\bRDC|SOUS.?SOL|TERRASSE|PLANCHER", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Rôle proposé pour un calque, d'après son nom. Les bureaux d'études n'ont pas de convention commune : la proposition se corrige.</summary>
    public static string RolePropose(string calque) =>
        RePoteau.IsMatch(calque) ? Poteaux : ReVoile.IsMatch(calque) ? Voiles : ReDalle.IsMatch(calque) ? Dalle : ReVide.IsMatch(calque) ? Reservations : Ignore;

    /// <summary>Rôles proposés pour tous les calques du plan qui portent des contours ou des cercles.</summary>
    public static Dictionary<string, string> RolesProposes(PlanCoffrage plan) =>
        plan.Contours.Select(c => c.Calque).Concat(plan.Ronds.Select(r => r.Calque)).Distinct().ToDictionary(c => c, RolePropose);

    private static string Role(IReadOnlyDictionary<string, string> roles, string calque) => roles.GetValueOrDefault(calque, Ignore);

    /// <summary>
    /// Un niveau par contour de dalle : les grands contours des calques de dalle ou de voile, regroupés quand ils se touchent.
    /// Le nom vient du titre de plan le plus proche.
    /// </summary>
    public static List<NiveauPlan> Niveaux(PlanCoffrage plan, IReadOnlyDictionary<string, string> roles)
    {
        var grands = plan.Contours.Where(c => Role(roles, c.Calque) is Dalle or Voiles && c.Polygone.Area > 20)
            .Select(c => c.Polygone).OrderByDescending(p => p.Area).ToList();
        var groupes = new List<List<Polygon>>();
        foreach (var p in grands)
        {
            var groupe = groupes.FirstOrDefault(g => g.Any(q => p.Distance(q) < 0.05));
            if (groupe is null) groupes.Add([p]); else groupe.Add(p);
        }
        var titres = plan.Titres.Where(t => t.Texte.Length < 40 && ReNiveau.IsMatch(t.Texte)).ToList();
        var niveaux = new List<(string? Titre, Envelope Zone)>();
        foreach (var g in groupes)
        {
            var zone = new Envelope();
            foreach (var p in g) zone.ExpandToInclude(p.EnvelopeInternal);
            zone.ExpandBy(1);
            var boite = Fabrique.ToGeometry(zone);
            var proches = titres.Select(t => (t, d: boite.Distance(Fabrique.CreatePoint(new Coordinate(t.X, t.Y))))).Where(x => x.d < 12).ToList();
            var hmax = proches.Count == 0 ? 0 : proches.Max(x => x.t.Hauteur);
            var titre = proches.Where(x => x.t.Hauteur >= 0.6 * hmax).OrderBy(x => x.d).Select(x => x.t.Texte).FirstOrDefault();
            niveaux.Add((titre, zone));
        }
        // De gauche à droite ; un plan sans titre reçoit son rang.
        return niveaux.OrderBy(z => z.Zone.MinX).Select((z, i) => new NiveauPlan(z.Titre ?? $"Plan sans titre n° {i + 1}", z.Zone)).ToList();
    }

    /// <summary>Niveaux retenus : un contour de dalle n'est un plan de structure que s'il porte au moins trois poteaux ou voiles.</summary>
    public static List<(NiveauPlan Niveau, FondPlanNiveau Fond)> Plans(PlanCoffrage plan, IReadOnlyDictionary<string, string> roles)
    {
        var retenus = new List<(NiveauPlan, FondPlanNiveau)>();
        foreach (var niveau in Niveaux(plan, roles))
            if (Extraire(plan, niveau.Zone, roles) is { Appuis: >= 3 } fond) retenus.Add((niveau, fond));
        return retenus;
    }

    /// <summary>Fond de plan de la zone d'un niveau, ou null si aucun contour de dalle n'y est trouvé.</summary>
    public static FondPlanNiveau? Extraire(PlanCoffrage plan, Envelope zone, IReadOnlyDictionary<string, string> roles)
    {
        var polys = plan.Contours.Where(c => zone.Contains(c.Polygone.InteriorPoint.Coordinate)).Select(c => (Role: Role(roles, c.Calque), P: c.Polygone)).ToList();

        // Dalle : le plus grand contour des calques de dalle ou de voile, complété par les contours de voiles périphériques accolés.
        var grands = polys.Where(x => x.Role is Dalle or Voiles && x.P.Area > 5).Select(x => x.P).OrderByDescending(p => p.Area).ToList();
        if (grands.Count == 0) return null;
        Geometry contour = grands[0];
        // Un contour de voiles périphériques se reconnaît à son jumeau intérieur, décalé de l'épaisseur du voile.
        bool Jumeau(Polygon p) => grands.Any(q => !ReferenceEquals(q, p) && q.Area > 0.6 * p.Area && p.Contains(q.Buffer(-0.01)));
        foreach (var p in grands.Skip(1))
            if (Jumeau(p) && p.Distance(contour) < 0.05 && p.Intersection(contour).Area < 0.02 * p.Area)
                contour = contour.Union(p.Buffer(0.001)).Buffer(-0.001);
        var dalle = PlusGrand(contour);
        if (dalle is null) return null;
        dalle = Simplifie(Fabrique.CreatePolygon(dalle.ExteriorRing.Coordinates));
        var pres = dalle.Buffer(0.3);

        // Sur un calque de poteaux : une forme ramassée est un poteau, une forme allongée un voile, une grande forme une cage.
        var surPoteaux = polys.Where(x => x.Role == Poteaux && pres.Contains(x.P.Centroid)).Select(x => x.P).ToList();
        var poteaux = surPoteaux.Where(p => p.Area <= 1.5 && !Allonge(p)).ToList();
        var ronds = plan.Ronds.Where(r => Role(roles, r.Calque) == Poteaux && pres.Contains(Fabrique.CreatePoint(new Coordinate(r.X, r.Y)))).ToList();

        // Voiles : rectangles allongés des calques de voile ; les morceaux alignés bout à bout sont réunis.
        var brins = polys.Where(x => x.Role == Voiles && x.P.Area < 5 && Allonge(x.P) && pres.Intersects(x.P)).Select(x => x.P)
            .Concat(surPoteaux.Where(p => p.Area <= 1.5 && Allonge(p))).ToList();

        // Voiles dessinés par deux contours emboîtés (périphérie d'un sous-sol, cage) : un rectangle par côté du contour extérieur.
        var anneaux = polys.Where(x => x.Role == Voiles && x.P.Area > 5).Select(x => x.P).Concat(surPoteaux.Where(p => p.Area > 1.5)).ToList();
        var peripherie = new List<Polygon>();
        var cages = new List<Polygon>();
        foreach (var ext in anneaux)
            foreach (var inte in anneaux)
            {
                var ecart = ext.Area - inte.Area;
                if (ReferenceEquals(inte, ext) || !(ecart > 0.03 * ext.Length && ecart < 0.5 * ext.Length) || !ext.Contains(inte.Buffer(-0.01))) continue;
                var epaisseur = ecart / ((ext.Length + inte.Length) / 2);
                // L'intérieur d'une cage d'escalier ou d'ascenseur est un vide de la dalle.
                if (inte.Area < 0.2 * dalle.Area && surPoteaux.Any(p => ReferenceEquals(p, ext))) cages.Add(inte);
                var anneau = ext.Difference(inte);
                var c = ext.ExteriorRing.Coordinates;
                for (var i = 0; i + 1 < c.Length; i++)
                {
                    var (p, q) = (c[i], c[i + 1]);
                    var n = p.Distance(q);
                    if (n < 0.3) continue;
                    var (nx, ny) = (-(q.Y - p.Y) / n * epaisseur * 1.02, (q.X - p.X) / n * epaisseur * 1.02);
                    foreach (var s in new[] { 1, -1 })
                    {
                        var bande = Fabrique.CreatePolygon([p, q, new Coordinate(q.X + s * nx, q.Y + s * ny), new Coordinate(p.X + s * nx, p.Y + s * ny), p]);
                        var utile = bande.Intersection(anneau);
                        if (utile.Area > 0.9 * bande.Area && Rectangle(utile) is { } r) { peripherie.Add(r); break; }
                    }
                }
            }

        var voiles = new List<Polygon>();
        if (brins.Count > 0)
        {
            var reunis = UnaryUnionOp.Union(brins.Select(b => b.Buffer(0.005)).ToList());
            for (var i = 0; i < reunis.NumGeometries; i++)
            {
                if (PlusGrand(reunis.GetGeometryN(i).Buffer(-0.005)) is not { } g) continue;
                var r = Rectangle(g);
                voiles.Add(r is not null && g.Area > 0.9 * r.Area ? r : Simplifie(Fabrique.CreatePolygon(g.ExteriorRing.Coordinates)));
            }
        }
        voiles.AddRange(peripherie);

        // Réservations : contours pleins des calques de réservation ou de dalle, entièrement dans la dalle. À confirmer par l'ingénieur.
        var dedans = dalle.Buffer(0.02);
        var reservations = polys.Where(x => x.Role is Reservations or Dalle && x.P.Area > 0.2 && x.P.Area < 0.25 * dalle.Area && dedans.Contains(x.P)
                && Rectangle(x.P) is { } r && x.P.Area > 0.8 * r.Area).Select(x => x.P).Concat(cages).ToList();

        return new FondPlanNiveau(dalle, poteaux, ronds, voiles, reservations);
    }

    /// <summary>Polygone à partir des sommets d'une polyligne, ou null si elle ne délimite pas une surface.</summary>
    public static Polygon? Polygone(IReadOnlyList<(double X, double Y)> sommets, bool fermee)
    {
        if (sommets.Count < 3) return null;
        var c = sommets.Select(s => new Coordinate(s.X, s.Y)).ToList();
        if (!c[0].Equals2D(c[^1])) c.Add(c[0].Copy());
        if (c.Count < 4) return null;
        Polygon brut;
        try { brut = Fabrique.CreatePolygon(c.ToArray()); } catch (ArgumentException) { return null; }
        // Une polyligne ouverte ne compte que si elle revient presque à son départ, ou si c'est un long contour (dalle dessinée sans la fermer).
        if (!fermee && sommets.Count < 6)
        {
            var ecart = Math.Sqrt(Math.Pow(sommets[0].X - sommets[^1].X, 2) + Math.Pow(sommets[0].Y - sommets[^1].Y, 2));
            if (ecart > 0.5 * Math.Max(1e-9, brut.Length) / sommets.Count) return null;
        }
        var propre = brut.IsValid ? brut : PlusGrand(brut.Buffer(0));
        return propre is { Area: > 1e-4 } ? propre : null;
    }

    /// <summary>Forme allongée : un voile plutôt qu'un poteau.</summary>
    internal static bool Allonge(Polygon p)
    {
        if (Rectangle(p) is not { } r) return false;
        var c = r.ExteriorRing.Coordinates;
        var (a, b) = (c[0].Distance(c[1]), c[1].Distance(c[2]));
        var (petit, grand) = (Math.Min(a, b), Math.Max(a, b));
        return petit < 0.45 && grand > 0.6 && grand > 2.4 * petit && p.Area > 0.85 * r.Area;
    }

    private static Polygon? Rectangle(Geometry g) => g.IsEmpty ? null : MinimumAreaRectangle.GetMinimumRectangle(g) as Polygon;

    private static Polygon? PlusGrand(Geometry g) => g switch
    {
        Polygon p when !p.IsEmpty => p,
        GeometryCollection c => Enumerable.Range(0, c.NumGeometries).Select(c.GetGeometryN).OfType<Polygon>().OrderByDescending(p => p.Area).FirstOrDefault(),
        _ => null,
    };

    private static Polygon Simplifie(Polygon p) => DouglasPeuckerSimplifier.Simplify(p, 0.005) as Polygon ?? p;
}
