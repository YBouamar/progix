using System.Text.RegularExpressions;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

/// <summary>Un câble reconstitué sur un plan de câblage : numéro et torons de son étiquette, longueur tracée, distance étiquette / câble.</summary>
public record CableDxf(int Numero, int Torons, string Orientation, decimal Longueur, decimal Distance, int Troncons);

/// <summary>Résultat de la lecture d'un plan de câblage DXF (export ADAPT repris dans AutoCAD).</summary>
public class PlanCablage
{
    public string Nom { get; init; } = "";
    /// <summary>X ou Y ; vide tant qu'elle n'est ni reconnue ni choisie.</summary>
    public string Orientation { get; set; } = "";
    public bool OrientationDetectee { get; set; }
    public List<CableDxf> Cables { get; } = [];
    /// <summary>Surface de la région de dalle, ouvertures comprises, en m².</summary>
    public decimal? SurfaceBrute { get; set; }
    /// <summary>Surface de la région de dalle moins les ouvertures, en m² : c'est le « Floor Area » d'ADAPT.</summary>
    public decimal? SurfaceNette { get; set; }
    /// <summary>Cartouche, à titre indicatif : référence, niveau et titre du plan.</summary>
    public string? Reference { get; set; }
    public string? Niveau { get; set; }
    public string? Titre { get; set; }
    public List<string> Erreurs { get; } = [];
    public List<string> Avertissements { get; } = [];
}

/// <summary>
/// Lecture déterministe d'un plan de câblage ADAPT enregistré en DXF : tendons (calque « _Tendon »), étiquettes « #n;S=torons »,
/// longueurs, surface de la dalle et cartouche. Les coordonnées sont en mètres.
/// </summary>
public static partial class LectureCablageAdapt
{
    /// <summary>Tolérance de raccord entre deux tronçons, en mètres.</summary>
    public const double Tolerance = 0.02;
    public const double DistanceMax = 3.0;
    public const decimal LongueurMin = 1m, LongueurMax = 60m;

    [GeneratedRegex(@"^#\s*(\d+)\s*;\s*S\s*=\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex Etiquette();

    // Codes de mise en forme d'un texte multiligne (police, souligné, saut de ligne...).
    [GeneratedRegex(@"\\[LlOoKkP~]|\\[A-Za-z][^;\\]*;|[{}]")]
    private static partial Regex Mise();

    private sealed record Troncon(List<(double X, double Y)> Points, double Longueur);
    private sealed class Chaine(Troncon t)
    {
        public List<Troncon> Troncons { get; } = [t];
        public (double X, double Y) A { get; set; } = t.Points[0];
        public (double X, double Y) B { get; set; } = t.Points[^1];
        public double Longueur => Troncons.Sum(x => x.Longueur);
    }
    private sealed record Etiq(int Numero, int Torons, double X, double Y);

    public static PlanCablage Lire(Stream contenu, string nom, string? orientation = null)
    {
        var plan = new PlanCablage { Nom = nom };
        if (nom.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
        {
            plan.Erreurs.Add("Ce fichier est un DWG. Dans AutoCAD, enregistrez le plan en DXF (Enregistrer sous, type DXF) puis déposez le DXF.");
            return plan;
        }
        var tete = new byte[4];
        var n = contenu.Read(tete, 0, 4);
        contenu.Position = 0;
        if (n == 4 && tete[0] == (byte)'A' && tete[1] == (byte)'C' && tete[2] == (byte)'1' && tete[3] == (byte)'0')
        {
            plan.Erreurs.Add("Ce fichier est un DWG. Dans AutoCAD, enregistrez le plan en DXF (Enregistrer sous, type DXF) puis déposez le DXF.");
            return plan;
        }

        CadDocument doc;
        try { using var lecteur = new DxfReader(contenu); doc = lecteur.Read(); }
        catch (Exception) { plan.Erreurs.Add("Ce fichier DXF est illisible. Enregistrez de nouveau le plan en DXF dans AutoCAD."); return plan; }

        var troncons = new List<Troncon>();
        var etiquettes = new List<Etiq>();
        var regions = new List<double>();
        var ouvertures = new List<double>();
        var cartouche = new List<string>();
        var calques = new HashSet<string>();
        foreach (var e in doc.Entities)
        {
            var calque = e.Layer?.Name ?? "";
            if (calque.EndsWith("_Tendon", StringComparison.OrdinalIgnoreCase))
            {
                calques.Add(calque);
                switch (e)
                {
                    case Polyline3D p when p.Vertices.Count() >= 2:
                        // Un tronçon de câble (une travée) : longueur en 3D.
                        var p3 = p.Vertices.Select(v => (v.Location.X, v.Location.Y, v.Location.Z)).ToList();
                        troncons.Add(new(p3.Select(v => (v.X, v.Y)).ToList(), Enumerable.Range(0, p3.Count - 1).Sum(i => Distance3(p3[i], p3[i + 1]))));
                        break;
                    case LwPolyline p when p.Vertices.Count > 2:
                        // Plus de deux sommets : un tronçon en 2D. Deux sommets : un repère de chaise, ignoré.
                        var p2 = p.Vertices.Select(v => (v.Location.X, v.Location.Y)).ToList();
                        troncons.Add(new(p2, Enumerable.Range(0, p2.Count - 1).Sum(i => Distance2(p2[i], p2[i + 1]))));
                        break;
                    case MText m:
                        // Seules les étiquettes « #n;S=torons » comptent ; les autres textes sont des hauteurs de profil.
                        if (Etiquette().Match(Texte(m)) is { Success: true } r)
                            etiquettes.Add(new(int.Parse(r.Groups[1].Value), int.Parse(r.Groups[2].Value), m.InsertPoint.X, m.InsertPoint.Y));
                        break;
                }
            }
            else if (calque.EndsWith("_Slab_Region", StringComparison.OrdinalIgnoreCase)) { if (Contour(e) is { Count: >= 3 } c) regions.Add(Aire(c)); }
            else if (calque.EndsWith("_Opening", StringComparison.OrdinalIgnoreCase)) { if (Contour(e) is { Count: >= 3 } c) ouvertures.Add(Aire(c)); }
            else if (calque.StartsWith("CARTOUCHE", StringComparison.OrdinalIgnoreCase) && e is MText t && Texte(t) is { Length: > 0 } txt) cartouche.Add(txt);
        }

        if (calques.Count == 0)
        {
            plan.Erreurs.Add("Ce fichier ne semble pas être un export ADAPT : aucun calque « _Tendon » n'y figure.");
            return plan;
        }

        // Orientation : demandée, sinon reconnue dans le nom des calques ou du fichier.
        var (o, detectee) = Orientation(orientation, calques, nom);
        plan.Orientation = o; plan.OrientationDetectee = detectee;
        if (o.Length == 0) plan.Erreurs.Add("L'orientation du plan (X ou Y) n'est pas reconnue : choisissez-la.");

        Surface(plan, regions, ouvertures);
        Cartouche(plan, cartouche);

        if (etiquettes.Count == 0 && troncons.Count == 0) { plan.Erreurs.Add("Aucun câble sur le calque « _Tendon » de ce plan."); return plan; }
        Cables(plan, troncons, etiquettes);
        return plan;
    }

    private static string Texte(MText m) => Mise().Replace(m.Value ?? "", " ").Replace("�", "'").Trim();

    private static (string, bool) Orientation(string? demandee, IEnumerable<string> calques, string nom)
    {
        if (demandee?.Trim().ToUpperInvariant() is "X" or "Y") return (demandee.Trim().ToUpperInvariant(), false);
        foreach (var s in calques.Append(nom))
        {
            var u = s.ToUpperInvariant();
            if (u.Contains("SUIVANT_X") || u.Contains("_TX_") || u.Contains("SUIVANT X")) return ("X", true);
            if (u.Contains("SUIVANT_Y") || u.Contains("_TY_") || u.Contains("SUIVANT Y")) return ("Y", true);
        }
        return ("", false);
    }

    private static List<(double X, double Y)>? Contour(ACadSharp.Entities.Entity e) => e switch
    {
        LwPolyline p => p.Vertices.Select(v => (v.Location.X, v.Location.Y)).ToList(),
        Polyline3D p => p.Vertices.Select(v => (v.Location.X, v.Location.Y)).ToList(),
        _ => null,
    };

    private static double Aire(List<(double X, double Y)> p)
    {
        double s = 0;
        for (var i = 0; i < p.Count; i++) { var (a, b) = (p[i], p[(i + 1) % p.Count]); s += a.X * b.Y - b.X * a.Y; }
        return Math.Abs(s) / 2;
    }

    /// <summary>Région de dalle (la plus grande) moins les ouvertures : correspond au « Floor Area » d'ADAPT.</summary>
    private static void Surface(PlanCablage plan, List<double> regions, List<double> ouvertures)
    {
        if (regions.Count == 0 || regions.Max() <= 0) return;
        var brute = regions.Max();
        plan.SurfaceBrute = Math.Round((decimal)brute, 2);
        plan.SurfaceNette = Math.Round((decimal)(brute - ouvertures.Sum()), 2);
    }

    private static void Cartouche(PlanCablage plan, List<string> textes)
    {
        plan.Reference = textes.Select(t => Regex.Match(t, @"^REF\s*:\s*(.+)$", RegexOptions.IgnoreCase)).FirstOrDefault(m => m.Success)?.Groups[1].Value.Trim();
        plan.Titre = textes.FirstOrDefault(t => t.Contains("PLAN", StringComparison.OrdinalIgnoreCase));
        plan.Niveau = textes.FirstOrDefault(t => !t.StartsWith("REF", StringComparison.OrdinalIgnoreCase) && !t.Contains("PLAN", StringComparison.OrdinalIgnoreCase));
    }

    private static double Distance2((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static double Distance3((double X, double Y, double Z) a, (double X, double Y, double Z) b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));

    /// <summary>
    /// Reconstitue les câbles : raccord des tronçons qui se touchent en XY (2 cm), étiquette de chaque câble (paires triées par distance,
    /// affectation gloutonne un pour un), puis rattachement des tronçons restants aux câbles étiquetés qui sont dans leur alignement
    /// (un câble peut être dessiné en plusieurs morceaux séparés par un vide).
    /// </summary>
    private static void Cables(PlanCablage plan, List<Troncon> troncons, List<Etiq> etiquettes)
    {
        var chaines = troncons.Select(t => new Chaine(t)).ToList();
        for (var continuer = true; continuer;)
        {
            continuer = false;
            for (var i = 0; i < chaines.Count && !continuer; i++)
                for (var j = i + 1; j < chaines.Count && !continuer; j++)
                    if (Raccorder(chaines[i], chaines[j])) { chaines.RemoveAt(j); continuer = true; }
        }

        var paires = new List<(double D, int E, int C)>();
        for (var i = 0; i < etiquettes.Count; i++)
            for (var j = 0; j < chaines.Count; j++)
                paires.Add((Math.Min(Distance2((etiquettes[i].X, etiquettes[i].Y), chaines[j].A), Distance2((etiquettes[i].X, etiquettes[i].Y), chaines[j].B)), i, j));
        var etiquetee = new Dictionary<int, (int E, double D)>();
        var prises = new HashSet<int>();
        // Affectation gloutonne un pour un, paires triées par distance. Première passe : étiquettes à 3 m au plus d'un câble.
        void Affecter(double maxi, Func<int, bool> chaineLibre)
        {
            foreach (var (d, e, c) in paires.Where(p => p.D <= maxi).OrderBy(p => p.D))
            {
                if (prises.Contains(e) || etiquetee.ContainsKey(c) || !chaineLibre(c)) continue;
                prises.Add(e); etiquetee[c] = (e, d);
            }
        }
        Affecter(DistanceMax, _ => true);

        // Axe du câble : X pour un plan suivant X, donc la coordonnée perpendiculaire est Y.
        double Perp((double X, double Y) p) => plan.Orientation == "Y" ? p.X : p.Y;
        bool Droite(Chaine c) => Math.Abs(Perp(c.A) - Perp(c.B)) <= Tolerance;
        var annexes = new Dictionary<int, List<Chaine>>();
        var restants = new List<int>();
        for (var j = 0; j < chaines.Count; j++)
        {
            if (etiquetee.ContainsKey(j)) continue;
            var c = chaines[j];
            var candidats = Droite(c) && plan.Orientation.Length > 0
                ? etiquetee.Keys.Where(k => Droite(chaines[k]) && Math.Abs(Perp(chaines[k].A) - Perp(c.A)) <= Tolerance)
                    .Select(k => (K: k, D: new[] { Distance2(c.A, chaines[k].A), Distance2(c.A, chaines[k].B), Distance2(c.B, chaines[k].A), Distance2(c.B, chaines[k].B) }.Min())).OrderBy(x => x.D).ToList()
                : [];
            if (candidats.Count == 0) { restants.Add(j); continue; }
            if (candidats.Count > 1)
                plan.Avertissements.Add($"Un tronçon de câble sans étiquette est aligné avec plusieurs câbles : il est rattaché au plus proche (n° {etiquettes[etiquetee[candidats[0].K].E].Numero:00}).");
            if (!annexes.TryGetValue(candidats[0].K, out var liste)) annexes[candidats[0].K] = liste = [];
            liste.Add(c);
        }
        // Seconde passe : les étiquettes encore libres prennent, à n'importe quelle distance, les câbles qui restent sans étiquette (avertissement de distance).
        var libres = restants.ToHashSet();
        Affecter(double.MaxValue, libres.Contains);
        var orphelins = restants.Where(j => !etiquetee.ContainsKey(j)).Select(j => chaines[j]).ToList();

        foreach (var (c, (e, d)) in etiquetee)
        {
            var et = etiquettes[e];
            var morceaux = 1 + (annexes.TryGetValue(c, out var a) ? a.Count : 0);
            var longueur = chaines[c].Longueur + (a?.Sum(x => x.Longueur) ?? 0);
            plan.Cables.Add(new CableDxf(et.Numero, et.Torons, plan.Orientation, Math.Round((decimal)longueur, 2), Math.Round((decimal)d, 2), morceaux));
        }
        plan.Cables.Sort((x, y) => x.Numero.CompareTo(y.Numero));

        // Contrôles bloquants.
        foreach (var c in orphelins)
            plan.Erreurs.Add($"Câble sans étiquette près de ({c.A.X:0.00} ; {c.A.Y:0.00}) : il n'a pas de numéro « #n;S=torons ».");
        for (var i = 0; i < etiquettes.Count; i++)
            if (!prises.Contains(i)) plan.Erreurs.Add($"Étiquette #{etiquettes[i].Numero:00} (S{etiquettes[i].Torons}) sans câble.");
        foreach (var g in plan.Cables.GroupBy(c => c.Numero).Where(g => g.Count() > 1))
            plan.Erreurs.Add($"Le tendon {g.Key:00} figure {g.Count()} fois dans ce plan.");
        foreach (var inconnu in plan.Cables.Where(c => !SystemesAncrage.Existe(c.Torons)).Select(c => c.Torons).Distinct())
            plan.Erreurs.Add($"Type d'ancrage S{inconnu} inconnu : les types existants sont {SystemesAncrage.Liste}.");

        // Avertissements.
        foreach (var c in plan.Cables)
        {
            if (c.Distance > (decimal)DistanceMax) plan.Avertissements.Add($"Tendon {c.Numero:00} : l'étiquette est à {c.Distance:0.00} m du câble (plus de {DistanceMax:0} m), vérifiez l'association.");
            if (c.Longueur < LongueurMin) plan.Avertissements.Add($"Tendon {c.Numero:00} : longueur de {c.Longueur:0.00} m, inférieure à {LongueurMin:0} m.");
            if (c.Longueur > LongueurMax) plan.Avertissements.Add($"Tendon {c.Numero:00} : longueur de {c.Longueur:0.00} m, supérieure à {LongueurMax:0} m.");
        }
    }

    /// <summary>Colle b au bout de a si une extrémité de l'une touche une extrémité de l'autre (XY, 2 cm), en inversant au besoin.</summary>
    private static bool Raccorder(Chaine a, Chaine b)
    {
        bool Proche((double X, double Y) p, (double X, double Y) q) => Distance2(p, q) <= Tolerance;
        // On ramène toujours le raccord à « fin de a » contre « début de b ».
        if (Proche(a.B, b.A)) { }
        else if (Proche(a.B, b.B)) Inverser(b);
        else if (Proche(a.A, b.B)) { Inverser(a); Inverser(b); }
        else if (Proche(a.A, b.A)) Inverser(a);
        else return false;
        a.Troncons.AddRange(b.Troncons);
        a.B = b.B;
        return true;
    }

    private static void Inverser(Chaine c)
    {
        c.Troncons.Reverse();
        foreach (var t in c.Troncons) t.Points.Reverse();
        (c.A, c.B) = (c.B, c.A);
    }
}
