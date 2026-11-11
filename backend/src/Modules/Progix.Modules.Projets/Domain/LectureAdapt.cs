using System.Globalization;
using System.Text;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

/// <summary>Données de chiffrage d'une dalle lues dans les exports ADAPT-Floor Pro (PTTotal et RebarTotal).</summary>
public class AdaptDalle
{
    /// <summary>Ratio actif en kg/m² : PTTotal, ligne « Current plane » du résumé, colonne Rate.</summary>
    public decimal RatioActif { get; set; }

    /// <summary>Ratio passif en kg/m² : RebarTotal, ligne « Current plane » du résumé, colonne Rate.</summary>
    public decimal RatioPassif { get; set; }

    /// <summary>Surface du plancher (Floor Area), comparée à celle de la dalle.</summary>
    public decimal SurfacePlancher { get; set; }

    /// <summary>Projet, plancher et fichier ADAPT, tels qu'ils figurent dans l'en-tête de l'export.</summary>
    public string? Plancher { get; set; }
    public List<SystemeAdapt> Systemes { get; set; } = [];
    /// <summary>Nom de l'export PTTotal lu ; vide tant qu'il manque (les deux exports peuvent arriver séparément).</summary>
    public string FichierPt { get; set; } = "";
    public string FichierRebar { get; set; } = "";

    /// <summary>Fichiers d'origine conservés (contexte « adapt »), pour les rouvrir ; null pour un import d'avant leur conservation.</summary>
    public Guid? FichierPtId { get; set; }
    public Guid? FichierRebarId { get; set; }
    public DateTime ImporteLe { get; set; }
    public string ImportePar { get; set; } = "";

    public const string Contexte = "adapt";
    public const string CategoriePt = "pt";
    public const string CategorieRebar = "rebar";

    public bool AvecPt => FichierPt.Length > 0;
    public bool AvecRebar => FichierRebar.Length > 0;
}

/// <summary>Câbles d'un même type d'ancrage (Number Of Strands).</summary>
public class SystemeAdapt
{
    public int Torons { get; set; }

    /// <summary>Nombre de lignes de câbles du type : c'est le nombre d'ancrages repris dans le devis.</summary>
    public int Cables { get; set; }

    /// <summary>Cumul de Strand Length, en mètres : c'est la longueur de gaine reprise dans le devis.</summary>
    public decimal LongueurTorons { get; set; }

    /// <summary>Cumul de Tendon/Duct Length, en mètres, gardé pour information.</summary>
    public decimal LongueurGaines { get; set; }
}

/// <summary>Lecture des exports RTF d'ADAPT (enregistrés en .rtf ou .rtf.doc).</summary>
internal static class LectureAdapt
{
    public const string PlanCourant = "Current plane";

    public enum Genre { Inconnu, Precontrainte, Armatures }

    /// <summary>Contenu d'un export : en-tête (projet, plancher) et lignes des tableaux, cellule par cellule.</summary>
    public record Document(string Entete, List<List<string>> Lignes)
    {
        public Genre Genre => Lignes.Any(l => l.Any(c => c.Contains("REBAR TOTALS", StringComparison.OrdinalIgnoreCase))) ? Genre.Armatures
            : Lignes.Any(l => l.Any(c => c.Contains("PT TOTALS", StringComparison.OrdinalIgnoreCase))) ? Genre.Precontrainte : Genre.Inconnu;
    }

    public record Resultat(AdaptDalle? Donnees, string? Erreur);

    private static readonly HashSet<string> GroupesIgnores = ["fonttbl", "colortbl", "stylesheet", "info", "header", "footer", "headerf", "footerf", "headerl", "headerr", "footerl", "footerr", "pict", "object"];

    /// <summary>Découpe un RTF en lignes de tableau ; l'en-tête de page est gardé à part.</summary>
    public static Document Lire(string rtf)
    {
        var lignes = new List<List<string>>();
        var ligne = new List<string>();
        var cellule = new StringBuilder();
        var entete = new StringBuilder();
        var pile = new Stack<(bool Ignore, bool Entete)>();
        bool ignore = false, dansEntete = false;
        var i = 0;

        void FinCellule() { ligne.Add(Espaces(cellule.ToString())); cellule.Clear(); }
        void FinLigne()
        {
            if (cellule.Length > 0) FinCellule();
            if (ligne.Any(c => c.Length > 0)) lignes.Add(ligne);
            ligne = [];
        }
        void Texte(string t) { if (dansEntete) entete.Append(t); else if (!ignore) cellule.Append(t); }

        while (i < rtf.Length)
        {
            var c = rtf[i];
            if (c == '{') { pile.Push((ignore, dansEntete)); i++; continue; }
            if (c == '}') { if (pile.Count > 0) (ignore, dansEntete) = pile.Pop(); i++; continue; }
            if (c is '\r' or '\n') { i++; continue; }
            if (c != '\\') { Texte(c.ToString()); i++; continue; }

            // Mot de contrôle : \motNNN suivi d'un espace facultatif.
            var j = i + 1;
            if (j < rtf.Length && char.IsAsciiLetter(rtf[j]))
            {
                while (j < rtf.Length && char.IsAsciiLetter(rtf[j])) j++;
                var mot = rtf[(i + 1)..j];
                if (j < rtf.Length && (rtf[j] == '-' || char.IsAsciiDigit(rtf[j]))) { j++; while (j < rtf.Length && char.IsAsciiDigit(rtf[j])) j++; }
                if (j < rtf.Length && rtf[j] == ' ') j++;
                i = j;
                if (mot is "header" or "headerf") { dansEntete = true; continue; }
                if (GroupesIgnores.Contains(mot)) { ignore = true; continue; }
                if (dansEntete) { if (mot is "tab" or "par") entete.Append('\t'); continue; }
                if (ignore) continue;
                switch (mot)
                {
                    case "cell": FinCellule(); break;
                    case "row": FinLigne(); break;
                    // Un paragraphe hors tableau (titre) forme une ligne ; dans une cellule, il vaut un espace.
                    case "par": if (ligne.Count == 0) FinLigne(); else cellule.Append(' '); break;
                    case "tab": cellule.Append(' '); break;
                }
                continue;
            }
            if (j < rtf.Length && rtf[j] == '\'' && j + 2 < rtf.Length)
            {
                if (byte.TryParse(rtf.AsSpan(j + 1, 2), NumberStyles.HexNumber, null, out var octet)) Texte(((char)octet).ToString());
                i = j + 3; continue;
            }
            if (j < rtf.Length && rtf[j] != '*') Texte(rtf[j].ToString());
            i = j + 1;
        }
        FinLigne();
        return new Document(Espaces(entete.ToString().Replace('\t', ' ')), lignes);
    }

    private static string Espaces(string s) => string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Nombre au format d'ADAPT (point décimal, virgule éventuelle des milliers).</summary>
    private static decimal? Nombre(string s) =>
        decimal.TryParse(s.Replace(",", "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>Ratio en kg/m² du plan courant, dans le tableau « Summary » (colonnes Reference Plane, Floor Area, Rate).</summary>
    private static (decimal Ratio, decimal Surface)? Resume(Document doc)
    {
        for (var k = 0; k < doc.Lignes.Count - 1; k++)
        {
            var titres = doc.Lignes[k];
            if (titres.FirstOrDefault() != "Reference Plane" || !titres.Contains("Floor Area")) continue;
            var unites = doc.Lignes[k + 1];
            var colRatio = unites.IndexOf("kg/m2");
            var colSurface = titres.IndexOf("Floor Area");
            if (colRatio < 0) continue;
            var plan = doc.Lignes.Skip(k + 2).FirstOrDefault(l => l.FirstOrDefault() == PlanCourant);
            if (plan is null || plan.Count <= Math.Max(colRatio, colSurface)) return null;
            if (Nombre(plan[colRatio]) is decimal ratio && Nombre(plan[colSurface]) is decimal surface) return (ratio, surface);
            return null;
        }
        return null;
    }

    /// <summary>Câbles du plan courant regroupés par nombre de torons.</summary>
    private static List<SystemeAdapt>? Cables(Document doc, out string? erreur)
    {
        erreur = null;
        var k = doc.Lignes.FindIndex(l => l.Contains("Number Of Strands") && l.Contains("Strand Length") && l.Contains("Reference Plane"));
        if (k < 0) { erreur = "le tableau des câbles (Number Of Strands, Strand Length) est introuvable"; return null; }
        var t = doc.Lignes[k];
        int colPlan = t.IndexOf("Reference Plane"), colTorons = t.IndexOf("Number Of Strands"), colToron = t.IndexOf("Strand Length"), colGaine = t.IndexOf("Tendon/Duct Length");
        var groupes = new Dictionary<int, SystemeAdapt>();
        foreach (var l in doc.Lignes.Skip(k + 1))
        {
            // Le tableau s'arrête au premier titre qui ne commence pas par un numéro de ligne.
            if (l.Count == 0) continue;
            if (!int.TryParse(l[0], out _)) { if (l.Count <= 1 || l[0].Length > 0) break; continue; }
            if (l.Count <= Math.Max(colToron, Math.Max(colTorons, colPlan)) || l[colPlan] != PlanCourant) continue;
            if (Nombre(l[colTorons]) is not decimal torons || torons != Math.Floor(torons) || Nombre(l[colToron]) is not decimal longueur)
            { erreur = $"ligne {l[0]} du tableau des câbles illisible"; return null; }
            var s = groupes.TryGetValue((int)torons, out var g) ? g : groupes[(int)torons] = new SystemeAdapt { Torons = (int)torons };
            s.Cables++;
            s.LongueurTorons += longueur;
            if (colGaine >= 0 && Nombre(l[colGaine]) is decimal gaine) s.LongueurGaines += gaine;
        }
        foreach (var s in groupes.Values) { s.LongueurTorons = Math.Round(s.LongueurTorons, 2); s.LongueurGaines = Math.Round(s.LongueurGaines, 2); }
        return groupes.Values.OrderBy(s => s.Torons).ToList();
    }

    /// <summary>Champ de l'en-tête de page (« Project Name: », « Specific Data: », « File Name: »).</summary>
    private static string? Champ(string entete, string nom)
    {
        var i = entete.IndexOf(nom, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        var reste = entete[(i + nom.Length)..];
        var fin = new[] { "Project Name:", "Specific Data:", "Date of execution:", "File Name:", "FLOOR-PRO", "ADAPT" }
            .Select(m => reste.IndexOf(m, StringComparison.OrdinalIgnoreCase)).Where(x => x > 0).DefaultIfEmpty(reste.Length).Min();
        var v = reste[..fin].Trim();
        return v.Length == 0 ? null : v;
    }

    /// <summary>Un export lu seul : son genre, la surface et le ratio du plan courant, et pour PTTotal les câbles par type d'ancrage.</summary>
    public record Fichier(Genre Genre, decimal Surface, decimal Ratio, string? Plancher, List<SystemeAdapt> Systemes, string? Erreur)
    {
        public string Nom => Genre == Genre.Armatures ? "RebarTotal" : "PTTotal";
    }

    /// <summary>
    /// Lit un seul export, PTTotal ou RebarTotal : les deux fichiers d'une dalle peuvent être déposés séparément.
    /// Mêmes refus que la lecture des deux fichiers ensemble.
    /// </summary>
    public static Fichier AnalyserUn(string rtf)
    {
        Fichier Refus(Genre g, string erreur) => new(g, 0, 0, null, [], erreur);
        if (!rtf.StartsWith(@"{\rtf")) return Refus(Genre.Inconnu, "Ce fichier n'est pas un export RTF d'ADAPT (fichier .rtf ou .rtf.doc).");
        var doc = Lire(rtf);
        if (doc.Genre == Genre.Inconnu) return Refus(Genre.Inconnu, "Ce fichier n'est ni un PTTotal (PT TOTALS) ni un RebarTotal (REBAR TOTALS).");
        var cables = new List<SystemeAdapt>();
        if (doc.Genre == Genre.Precontrainte)
        {
            if (Cables(doc, out var erreur) is not { } lus) return Refus(doc.Genre, $"PTTotal : {erreur}.");
            if (lus.Count == 0) return Refus(doc.Genre, "PTTotal : aucun câble sur le plan courant (Current plane).");
            if (lus.FirstOrDefault(s => !SystemesAncrage.Existe(s.Torons)) is { } inconnu)
                return Refus(doc.Genre, $"PTTotal : câbles à {inconnu.Torons} torons (S{inconnu.Torons}), type d'ancrage inconnu ; les types existants sont {SystemesAncrage.Liste}.");
            cables = lus;
        }
        var nom = doc.Genre == Genre.Armatures ? "RebarTotal" : "PTTotal";
        if (Resume(doc) is not { } resume) return Refus(doc.Genre, $"{nom} : ratio du plan courant introuvable (tableau Summary, colonne Rate en kg/m2).");
        var plancher = string.Join(" · ", new[] { Champ(doc.Entete, "Project Name:"), Champ(doc.Entete, "Specific Data:"), Champ(doc.Entete, "File Name:") }.Where(s => s is not null));
        return new(doc.Genre, resume.Surface, resume.Ratio, plancher.Length == 0 ? null : plancher[..Math.Min(plancher.Length, 300)], cables, null);
    }

    /// <summary>
    /// Lit les deux exports, dans n'importe quel ordre. Refuse un type d'ancrage absent de la liste des systèmes existants.
    /// </summary>
    public static Resultat Analyser(string rtf1, string rtf2)
    {
        var docs = new[] { Lire(rtf1), Lire(rtf2) };
        var pt = docs.FirstOrDefault(d => d.Genre == Genre.Precontrainte);
        var rebar = docs.FirstOrDefault(d => d.Genre == Genre.Armatures);
        if (pt is null || rebar is null)
            return new(null, "Déposez les deux exports ADAPT de la dalle : PTTotal (PT TOTALS) et RebarTotal (REBAR TOTALS).");

        var cables = Cables(pt, out var erreur);
        if (cables is null) return new(null, $"PTTotal : {erreur}.");
        if (cables.Count == 0) return new(null, "PTTotal : aucun câble sur le plan courant (Current plane).");
        if (cables.FirstOrDefault(s => !SystemesAncrage.Existe(s.Torons)) is { } inconnu)
            return new(null, $"PTTotal : câbles à {inconnu.Torons} torons (S{inconnu.Torons}), type d'ancrage inconnu ; les types existants sont {SystemesAncrage.Liste}.");
        if (Resume(pt) is not { } actif) return new(null, "PTTotal : ratio du plan courant introuvable (tableau Summary, colonne Rate en kg/m2).");
        if (Resume(rebar) is not { } passif) return new(null, "RebarTotal : ratio du plan courant introuvable (tableau Summary, colonne Rate en kg/m2).");

        var plancher = string.Join(" · ", new[] { Champ(pt.Entete, "Project Name:"), Champ(pt.Entete, "Specific Data:"), Champ(pt.Entete, "File Name:") }
            .Where(s => s is not null));
        return new(new AdaptDalle
        {
            RatioActif = actif.Ratio, RatioPassif = passif.Ratio, SurfacePlancher = actif.Surface,
            Plancher = plancher.Length == 0 ? null : plancher[..Math.Min(plancher.Length, 300)], Systemes = cables,
        }, null);
    }

    /// <summary>Écarts à signaler sans bloquer : surface du plancher différente, exports de deux planchers différents.</summary>
    public static List<string> Avertissements(string rtf1, string rtf2, AdaptDalle a, decimal surfaceDalle)
    {
        var l = new List<string>();
        var docs = new[] { Lire(rtf1), Lire(rtf2) };
        var surfaces = docs.Select(Resume).Where(r => r is not null).Select(r => r!.Value.Surface).Distinct().ToList();
        if (surfaces.Count > 1) l.Add($"Les deux fichiers ne donnent pas la même surface de plancher ({string.Join(" et ", surfaces.Select(M2))}) : vérifiez qu'ils portent sur le même plancher.");
        var planchers = docs.Select(d => Champ(d.Entete, "Specific Data:")).Where(s => s is not null).Distinct().ToList();
        if (planchers.Count > 1) l.Add($"Les deux fichiers portent sur des planchers différents ({string.Join(" et ", planchers)}).");
        if (surfaceDalle > 0 && Math.Round(a.SurfacePlancher, 2) != surfaceDalle)
            l.Add($"Surface du plancher ADAPT : {M2(a.SurfacePlancher)}, surface de la dalle : {M2(surfaceDalle)}. La surface de la dalle est conservée.");
        return l;
    }

    /// <summary>
    /// Reporte un export lu sur les données de la dalle, sans toucher à celles de l'autre export.
    /// PTTotal apporte le ratio actif, les câbles et la surface du plancher ; RebarTotal le ratio passif.
    /// </summary>
    public static AdaptDalle Reporter(AdaptDalle? avant, Fichier f, string nom)
    {
        var a = new AdaptDalle
        {
            RatioActif = avant?.RatioActif ?? 0, RatioPassif = avant?.RatioPassif ?? 0, SurfacePlancher = avant?.SurfacePlancher ?? 0, Plancher = avant?.Plancher,
            Systemes = (avant?.Systemes ?? []).Select(s => new SystemeAdapt { Torons = s.Torons, Cables = s.Cables, LongueurTorons = s.LongueurTorons, LongueurGaines = s.LongueurGaines }).ToList(),
            FichierPt = avant?.FichierPt ?? "", FichierRebar = avant?.FichierRebar ?? "", FichierPtId = avant?.FichierPtId, FichierRebarId = avant?.FichierRebarId,
        };
        if (f.Genre == Genre.Precontrainte)
        {
            a.RatioActif = f.Ratio; a.Systemes = f.Systemes; a.SurfacePlancher = f.Surface; a.Plancher = f.Plancher ?? a.Plancher; a.FichierPt = nom; a.FichierPtId = null;
        }
        else
        {
            a.RatioPassif = f.Ratio; a.FichierRebar = nom; a.FichierRebarId = null;
            if (!a.AvecPt) { a.SurfacePlancher = f.Surface; a.Plancher = f.Plancher ?? a.Plancher; }
        }
        return a;
    }

    /// <summary>Écarts d'un export déposé seul : surface différente de celle de la dalle, ou de celle de l'autre export déjà lu.</summary>
    public static List<string> Avertissements(Fichier f, AdaptDalle? avant, decimal surfaceDalle)
    {
        var l = new List<string>();
        var autre = f.Genre == Genre.Precontrainte ? avant?.AvecRebar == true : avant?.AvecPt == true;
        if (autre && Math.Round(avant!.SurfacePlancher, 2) != Math.Round(f.Surface, 2))
            l.Add($"{f.Nom} donne {M2(f.Surface)} de plancher, l'autre export de la dalle {M2(avant.SurfacePlancher)} : vérifiez qu'ils portent sur le même plancher.");
        if (surfaceDalle > 0 && Math.Round(f.Surface, 2) != surfaceDalle)
            l.Add($"Surface du plancher ADAPT : {M2(f.Surface)}, surface de la dalle : {M2(surfaceDalle)}. La surface de la dalle est conservée.");
        return l;
    }

    private static string M2(decimal v) => v.ToString("N2", CultureInfo.GetCultureInfo("fr-FR")) + " m²";
}

/// <summary>
/// Rattachement d'un export ADAPT déposé sans dalle désignée : il se rapproche par la surface de plancher lue.
/// Dans le doute, rien n'est rattaché : la personne choisit la dalle.
/// </summary>
internal static class RapprochementAdapt
{
    /// <param name="Surface">Surface de la dalle, 0 si elle reste à renseigner.</param>
    /// <param name="SurfaceAdapt">Surface de plancher des exports déjà lus pour la dalle.</param>
    public record Candidat(Guid Id, decimal Surface, decimal? SurfaceAdapt, bool AvecPt, bool AvecRebar);

    /// <summary>
    /// La dalle sûre, ou null. Sûre : la seule dalle possible, ou la seule dont la surface, ou celle de ses exports déjà lus, égale celle du fichier.
    /// </summary>
    public static Guid? Deviner(decimal surfaceFichier, IReadOnlyList<Candidat> dalles)
    {
        if (dalles.Count == 1) return dalles[0].Id;
        var s = Math.Round(surfaceFichier, 2);
        var egales = dalles.Where(d => (d.Surface > 0 && d.Surface == s) || (d.SurfaceAdapt is decimal a && Math.Round(a, 2) == s)).ToList();
        return egales.Count == 1 ? egales[0].Id : null;
    }

    /// <summary>Dalle proposée quand rien n'est sûr : d'abord celles à qui cet export manque, puis la surface la plus proche.</summary>
    public static Guid? Proposer(decimal surfaceFichier, LectureAdapt.Genre genre, IReadOnlyList<Candidat> dalles) =>
        dalles.OrderBy(d => genre == LectureAdapt.Genre.Precontrainte ? d.AvecPt : d.AvecRebar)
            .ThenBy(d => Math.Abs((d.SurfaceAdapt ?? (d.Surface > 0 ? d.Surface : decimal.MaxValue / 4)) - surfaceFichier))
            .Select(d => (Guid?)d.Id).FirstOrDefault();
}
