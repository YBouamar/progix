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
    // Un calque de hachures ne désigne des voiles que s'il le dit (« HACHURE POT.V ») : seul, il porte aussi bien des remplissages non porteurs.
    private static readonly Regex ReVoile = new(@"voile|hachure.*pot|pot.*hachure", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // « Ouverture voile » dessine le passage laissé dans un voile, pas un voile.
    private static readonly Regex ReOuverture = new("ouverture", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReDalle = new("coffrage|dalle", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReVide = new("poutre|vide|tr[eé]mie|r[eé]serv", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReNiveau = new(@"\bPH\b|MEZZ|[EÉ]TAGE|\bRDC|SOUS.?SOL|TERRASSE|PLANCHER", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Rôle proposé pour un calque, d'après son nom. Les bureaux d'études n'ont pas de convention commune : la proposition se corrige.
    /// Un calque de hachures sans autre précision est proposé ignoré ; l'ingénieur le passe en voiles au besoin.
    /// </summary>
    public static string RolePropose(string calque) =>
        ReOuverture.IsMatch(calque) ? Ignore : RePoteau.IsMatch(calque) ? Poteaux : ReVoile.IsMatch(calque) ? Voiles : ReDalle.IsMatch(calque) ? Dalle : ReVide.IsMatch(calque) ? Reservations : Ignore;

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
        // Un contour dessiné deux fois au même endroit sur le même calque ne compte qu'une fois.
        var polys = plan.Contours.Where(c => zone.Contains(c.Polygone.InteriorPoint.Coordinate))
            .DistinctBy(c => (c.Calque, Math.Round(c.Polygone.Centroid.X, 2), Math.Round(c.Polygone.Centroid.Y, 2), Math.Round(c.Polygone.Area, 2)))
            .Select(c => (Role: Role(roles, c.Calque), c.Calque, P: c.Polygone)).ToList();

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

        // Sur un calque de poteaux : une forme ramassée est un poteau, une forme plus de quatre fois plus longue qu'épaisse un voile, une grande forme une cage.
        var calquesPoteaux = polys.Where(x => x.Role == Poteaux && pres.Contains(x.P.Centroid)).ToList();
        var surPoteaux = calquesPoteaux.Select(x => x.P).ToList();
        var poteaux = surPoteaux.Where(p => p.Area <= 1.5 && !Allonge(p, 4)).ToList();
        var ronds = plan.Ronds.Where(r => Role(roles, r.Calque) == Poteaux && pres.Contains(Fabrique.CreatePoint(new Coordinate(r.X, r.Y)))).ToList();

        // Voiles : rectangles allongés des calques de voile ; les morceaux alignés bout à bout sont réunis.
        var brins = polys.Where(x => x.Role == Voiles && x.P.Area < 5 && Allonge(x.P) && pres.Intersects(x.P))
            .Concat(calquesPoteaux.Where(x => x.P.Area <= 1.5 && Allonge(x.P, 4))).Select(x => (x.Calque, x.P)).ToList();

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
                // Un rectangle dessiné dans l'épaisseur du voile est une ouverture (porte de la cage) : il coupe le voile, il n'en est pas un.
                var ouvertures = brins.Where(b => b.P.Intersection(anneau).Area > 0.9 * b.P.Area).Select(b => b.P).ToList();
                brins.RemoveAll(b => ouvertures.Contains(b.P));
                var cotes = new List<Polygon>();
                var c = ext.ExteriorRing.Coordinates;
                for (var i = 0; i + 1 < c.Length; i++)
                {
                    var (p, q) = (c[i], c[i + 1]);
                    var n = p.Distance(q);
                    if (n < 0.3) continue;
                    var (nx, ny) = (-(q.Y - p.Y) / n * epaisseur, (q.X - p.X) / n * epaisseur);
                    foreach (var s in new[] { 1, -1 })
                    {
                        var bande = Fabrique.CreatePolygon([p, q, new Coordinate(q.X + s * nx, q.Y + s * ny), new Coordinate(p.X + s * nx, p.Y + s * ny), p]);
                        var utile = bande.Intersection(anneau);
                        if (utile.Area > 0.9 * bande.Area && Rectangle(utile) is { } r) { cotes.Add(r); break; }
                    }
                }
                // Les côtés ne se recouvrent pas aux angles : un côté percé d'une ouverture garde l'angle, puis les plus longs.
                Geometry pris = Fabrique.CreatePolygon();
                foreach (var ouverture in ouvertures) pris = pris.Union(ouverture);
                foreach (var cote in cotes.OrderByDescending(r => ouvertures.Any(o => o.Intersects(r))).ThenByDescending(r => r.Area))
                {
                    var reste = SansFil(cote.Difference(pris));
                    for (var i = 0; i < reste.NumGeometries; i++)
                        if (reste.GetGeometryN(i) is Polygon { Area: > 0.01 } morceau && Rectangle(morceau) is { } r) peripherie.Add(r);
                    pris = pris.Union(cote);
                }
            }

        var voiles = new List<Polygon>();
        // Les morceaux d'un même calque alignés bout à bout se réunissent en un seul rectangle. Deux voiles en équerre restent deux voiles,
        // et deux calques différents ne se fondent pas en un seul voile.
        foreach (var calque in brins.GroupBy(b => b.Calque))
        {
            var morceaux = calque.Select(b => b.P).ToList();
            for (var fusion = true; fusion;)
            {
                fusion = false;
                for (var i = 0; i < morceaux.Count && !fusion; i++)
                    for (var j = i + 1; j < morceaux.Count && !fusion; j++)
                    {
                        if (morceaux[i].Distance(morceaux[j]) > 0.01) continue;
                        if (PlusGrand(morceaux[i].Buffer(0.005, AngleVif).Union(morceaux[j].Buffer(0.005, AngleVif)).Buffer(-0.005, AngleVif)) is not { } u
                            || Rectangle(u) is not { } r || u.Area < 0.97 * r.Area || u.Area < 0.97 * (morceaux[i].Area + morceaux[j].Area)) continue;
                        morceaux[i] = r; morceaux.RemoveAt(j); fusion = true;
                    }
            }
            foreach (var g in morceaux)
                voiles.Add(Rectangle(g) is { } r && g.Area > 0.9 * r.Area ? r : Simplifie(Fabrique.CreatePolygon(g.ExteriorRing.Coordinates)));
        }
        voiles.AddRange(peripherie);

        // Réservations : contours pleins des calques de réservation ou de dalle, entièrement dans la dalle. À confirmer par l'ingénieur.
        var dedans = dalle.Buffer(0.02);
        var reservations = polys.Where(x => x.Role is Reservations or Dalle && x.P.Area > 0.2 && x.P.Area < 0.25 * dalle.Area && dedans.Contains(x.P)
                && Rectangle(x.P) is { } r && x.P.Area > 0.8 * r.Area).Select(x => x.P).Concat(cages).ToList();

        return new FondPlanNiveau(dalle, poteaux, ronds, voiles, reservations);
    }

    /// <summary>
    /// Facteur qui ramène le dessin au mètre. L'unité déclarée dans le fichier est gardée si elle donne un bâtiment plausible
    /// (plus grand contour entre 4 m et 400 m) ; sinon on retient, entre mètre, centimètre et millimètre, celle qui en donne un,
    /// en départageant par l'épaisseur courante des poteaux et des voiles (8 cm à 1,20 m). Des dessins en mètres sont souvent déclarés en millimètres.
    /// </summary>
    public static double Echelle(double declaree, IReadOnlyList<double> etendues, IReadOnlyList<double> epaisseurs)
    {
        if (etendues.Count == 0) return declaree;
        var plusGrand = etendues.Max();
        bool Plausible(double k) => plusGrand * k is >= 4 and <= 400;
        if (Plausible(declaree)) return declaree;
        var possibles = new[] { 1.0, 0.01, 0.001 }.Where(Plausible).ToList();
        if (possibles.Count == 0) return declaree;
        var triees = epaisseurs.Where(e => e > 0).OrderBy(e => e).ToList();
        if (possibles.Count == 1 || triees.Count == 0) return possibles[0];
        var mediane = triees[triees.Count / 2];
        return possibles.FirstOrDefault(k => mediane * k is >= 0.08 and <= 1.2, possibles[0]);
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
    /// <param name="rapport">Rapport minimal de la longueur à l'épaisseur. Sur un calque de poteaux, 4 : en dessous c'est un poteau rectangulaire.</param>
    internal static bool Allonge(Polygon p, double rapport = 2.4)
    {
        if (Rectangle(p) is not { } r) return false;
        var c = r.ExteriorRing.Coordinates;
        var (a, b) = (c[0].Distance(c[1]), c[1].Distance(c[2]));
        var (petit, grand) = (Math.Min(a, b), Math.Max(a, b));
        return petit < 0.45 && grand > 0.6 && grand > rapport * petit && p.Area > 0.85 * r.Area;
    }

    private static Polygon? Rectangle(Geometry g) => g.IsEmpty ? null : MinimumAreaRectangle.GetMinimumRectangle(g) as Polygon;

    private static readonly NetTopologySuite.Operation.Buffer.BufferParameters AngleVif = new() { JoinStyle = NetTopologySuite.Operation.Buffer.JoinStyle.Mitre };

    /// <summary>Retire les filets de moins de deux centimètres laissés par une découpe, sans arrondir les angles.</summary>
    private static Geometry SansFil(Geometry g) => g.Buffer(-0.01, AngleVif).Buffer(0.01, AngleVif);

    private static Polygon? PlusGrand(Geometry g) => g switch
    {
        Polygon p when !p.IsEmpty => p,
        GeometryCollection c => Enumerable.Range(0, c.NumGeometries).Select(c.GetGeometryN).OfType<Polygon>().OrderByDescending(p => p.Area).FirstOrDefault(),
        _ => null,
    };

    private static Polygon Simplifie(Polygon p) => DouglasPeuckerSimplifier.Simplify(p, 0.005) as Polygon ?? p;
}
