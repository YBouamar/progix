using System.Globalization;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Progix.Modules.Projets.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

/// <summary>
/// Import des tendons d'une commande usine depuis un fichier JSON, Excel ou CSV.
/// Trame : num, system (S1, S4...), orientation (x ou y), longueur (m), ancrages.
/// </summary>
public static class ImportTendons
{
    public const int Max = 3000;
    private static readonly string[] Colonnes = ["num", "system", "orientation", "longueur", "ancrages"];

    /// <summary>Numéro de tendon : des chiffres seulement, sans préfixe, sur deux chiffres au moins (01, 02... 2634). Null s'il n'est pas valide.</summary>
    public static string? Numero(string? s)
    {
        var t = (s ?? "").Trim();
        if (t.EndsWith(".0")) t = t[..^2];
        return t.Length is > 0 and <= 9 && t.All(char.IsAsciiDigit) ? int.Parse(t).ToString("00") : null;
    }

    public static string? Sens(string? s) => s?.Trim().ToUpperInvariant() is "X" or "Y" ? s.Trim().ToUpperInvariant() : null;

    /// <summary>Contrôle commun à la saisie et à l'enregistrement.</summary>
    /// <param name="deja">Types d'ancrage déjà enregistrés sur la commande : acceptés même s'ils ne figurent plus dans la liste.</param>
    public static string? Refus(IReadOnlyList<TendonInput> t, IReadOnlyCollection<int>? deja = null)
    {
        if (t.Count > Max) return $"Une commande compte au plus {Max} tendons.";
        if (t.FirstOrDefault(x => Numero(x.Numero) is null) is { } faux) return $"Numéro de tendon « {faux.Numero} » invalide : un numéro simple, sans préfixe (01, 02, 03...).";
        if (t.GroupBy(x => Numero(x.Numero)).FirstOrDefault(g => g.Count() > 1) is { } double_) return $"Le tendon {double_.Key} figure deux fois.";
        if (t.FirstOrDefault(x => !SystemesAncrage.Existe(x.Torons) && !(deja?.Contains(x.Torons) ?? false)) is { } inconnu)
            return $"Type d'ancrage S{inconnu.Torons} inconnu : les types existants sont {SystemesAncrage.Liste}.";
        if (t.Any(x => Sens(x.Sens) is null)) return "L'orientation d'un tendon est X ou Y.";
        if (t.Any(x => x.Longueur is <= 0 or > 500)) return "La longueur d'un tendon est supérieure à 0 et au plus de 500 m.";
        if (t.Any(x => x.Ancrages is < 0 or > 99)) return "Le nombre d'ancrages d'un tendon est compris entre 0 et 99.";
        return null;
    }

    public static (byte[] Contenu, string Type, string Nom)? Trame(string format)
    {
        (string, string, string, string, string)[] exemples = [("1", "S4", "x", "12.50", "4"), ("2", "S1", "y", "22.20", "1")];
        switch (format.ToLowerInvariant())
        {
            case "json":
                var json = JsonSerializer.Serialize(new { cables = exemples.Select(e => new { num = int.Parse(e.Item1), system = e.Item2, orientation = e.Item3, longueur = decimal.Parse(e.Item4, CultureInfo.InvariantCulture), ancrages = int.Parse(e.Item5) }) },
                    new JsonSerializerOptions { WriteIndented = true });
                return (Encoding.UTF8.GetBytes(json), "application/json", "trame-tendons.json");
            case "csv":
                var csv = string.Join("\r\n", new[] { string.Join(";", Colonnes) }.Concat(exemples.Select(e => $"{e.Item1};{e.Item2};{e.Item3};{e.Item4.Replace('.', ',')};{e.Item5}"))) + "\r\n";
                return (Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(), "text/csv", "trame-tendons.csv");
            case "xlsx":
                using (var wb = new XLWorkbook())
                {
                    var ws = wb.AddWorksheet("Tendons");
                    for (var c = 0; c < Colonnes.Length; c++) ws.Cell(1, c + 1).Value = Colonnes[c];
                    ws.Row(1).Style.Font.Bold = true;
                    for (var r = 0; r < exemples.Length; r++)
                    {
                        var e = exemples[r];
                        ws.Cell(r + 2, 1).Value = int.Parse(e.Item1); ws.Cell(r + 2, 2).Value = e.Item2; ws.Cell(r + 2, 3).Value = e.Item3;
                        ws.Cell(r + 2, 4).Value = decimal.Parse(e.Item4, CultureInfo.InvariantCulture); ws.Cell(r + 2, 5).Value = int.Parse(e.Item5);
                    }
                    var aide = wb.AddWorksheet("Aide");
                    string[] lignes = ["Une ligne par tendon. Remplacez les deux lignes d'exemple.", "num : numéro du tendon, des chiffres seulement, unique dans la dalle (1, 2, 3...).", $"system : type d'ancrage, parmi {SystemesAncrage.Liste} (le nombre est celui des torons).",
                        "orientation : x ou y.", "longueur : en mètres.", "ancrages : nombre d'ancrages du tendon."];
                    for (var r = 0; r < lignes.Length; r++) aide.Cell(r + 1, 1).Value = lignes[r];
                    ws.Columns().AdjustToContents(); aide.Column(1).AdjustToContents();
                    using var ms = new MemoryStream();
                    wb.SaveAs(ms);
                    return (ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "trame-tendons.xlsx");
                }
            default: return null;
        }
    }

    public static (List<Tendon> Tendons, List<string> Erreurs, string? Refus) Lire(string nom, byte[] contenu)
    {
        List<string?[]> lignes;
        try
        {
            lignes = Path.GetExtension(nom).ToLowerInvariant() switch
            {
                ".json" => Json(contenu),
                ".csv" or ".txt" => Csv(contenu),
                ".xlsx" => Excel(contenu),
                _ => throw new FormatException("Format non reconnu : déposez un fichier .json, .xlsx ou .csv."),
            };
        }
        catch (FormatException e) { return ([], [], e.Message); }
        catch (Exception) { return ([], [], "Le fichier n'a pas pu être lu : vérifiez qu'il respecte la trame."); }
        if (lignes.Count == 0) return ([], [], "Le fichier ne contient aucun tendon.");
        if (lignes.Count > Max) return ([], [], $"Le fichier contient {lignes.Count} lignes : une commande compte au plus {Max} tendons.");

        var tendons = new List<Tendon>();
        var erreurs = new List<string>();
        var vus = new HashSet<string>();
        for (var i = 0; i < lignes.Count; i++)
        {
            var l = lignes[i];
            var brut = l[0]?.Trim() ?? "";
            var qui = $"Ligne {i + 1}{(brut.Length > 0 ? $", tendon {brut}" : "")}";
            if (Numero(brut) is not { } num) { erreurs.Add($"{qui} : numéro absent ou invalide, attendu un numéro simple sans préfixe."); continue; }
            if (!vus.Add(num)) { erreurs.Add($"{qui} : numéro en double."); continue; }
            var sys = (l[1] ?? "").Trim().TrimStart('S', 's');
            if (!int.TryParse(sys, out var torons) || !SystemesAncrage.Existe(torons)) { erreurs.Add($"{qui} : type d'ancrage « {l[1]} » inconnu, attendu {SystemesAncrage.Liste}."); continue; }
            if (Sens(l[2]) is not { } sens) { erreurs.Add($"{qui} : orientation « {l[2]} » inconnue, attendu x ou y."); continue; }
            if (Nombre(l[3]) is not { } longueur || longueur is <= 0 or > 500) { erreurs.Add($"{qui} : longueur « {l[3]} » invalide."); continue; }
            var ancrages = 2;
            if (!string.IsNullOrWhiteSpace(l[4]) && (Nombre(l[4]) is not { } a || a != Math.Floor(a) || a is < 0 or > 99 || (ancrages = (int)a) < 0)) { erreurs.Add($"{qui} : ancrages « {l[4]} » invalide."); continue; }
            tendons.Add(new Tendon(num, torons, sens, Math.Round(longueur, 2), ancrages));
        }
        if (erreurs.Count > 30) erreurs = [.. erreurs.Take(30), $"... et {erreurs.Count - 30} autres lignes en erreur."];
        return (tendons, erreurs, null);
    }

    private static decimal? Nombre(string? s) =>
        decimal.TryParse((s ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static List<string?[]> Json(byte[] contenu)
    {
        using var doc = JsonDocument.Parse(contenu, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var racine = doc.RootElement;
        JsonElement liste;
        if (racine.ValueKind == JsonValueKind.Array) liste = racine;
        else if (racine.ValueKind == JsonValueKind.Object && (racine.TryGetProperty("cables", out liste) || racine.TryGetProperty("tendons", out liste)) && liste.ValueKind == JsonValueKind.Array) { }
        else throw new FormatException("Le fichier JSON doit contenir une liste « cables ».");
        string? Champ(JsonElement e, params string[] noms)
        {
            foreach (var p in e.EnumerateObject())
                if (noms.Contains(p.Name.ToLowerInvariant()))
                    return p.Value.ValueKind switch { JsonValueKind.String => p.Value.GetString(), JsonValueKind.Number => p.Value.GetRawText(), _ => null };
            return null;
        }
        return liste.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object)
            .Select(e => new[] { Champ(e, "num", "numero", "numéro"), Champ(e, "system", "systeme", "système"), Champ(e, "orientation", "sens"), Champ(e, "longueur"), Champ(e, "ancrages") }).ToList();
    }

    /// <summary>Repère les colonnes par leur titre : l'ordre est libre, « ancrages » est facultative.</summary>
    private static List<string?[]> Tableau(List<string[]> lignes)
    {
        if (lignes.Count == 0) return [];
        string Cle(string s) => s.Trim().ToLowerInvariant().Replace("é", "e").Replace("è", "e");
        var titres = lignes[0].Select(Cle).ToList();
        int Col(params string[] noms) => titres.FindIndex(noms.Contains);
        int[] cols = [Col("num", "numero", "n°"), Col("system", "systeme"), Col("orientation", "sens"), Col("longueur", "longueur m", "longueur (m)"), Col("ancrages")];
        if (cols.Take(4).Any(c => c < 0)) throw new FormatException("La première ligne doit porter les titres num, system, orientation, longueur (et ancrages).");
        return lignes.Skip(1).Where(l => l.Any(c => !string.IsNullOrWhiteSpace(c)))
            .Select(l => cols.Select(c => c >= 0 && c < l.Length ? l[c] : null).ToArray()).ToList();
    }

    private static List<string?[]> Csv(byte[] contenu)
    {
        var texte = new UTF8Encoding(false).GetString(contenu).TrimStart('﻿');
        var lignes = texte.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
        if (lignes.Count == 0) return [];
        var sep = lignes[0].Contains(';') ? ';' : lignes[0].Contains('\t') ? '\t' : ',';
        return Tableau(lignes.Select(l => l.Split(sep).Select(c => c.Trim().Trim('"')).ToArray()).ToList());
    }

    private static List<string?[]> Excel(byte[] contenu)
    {
        using var wb = new XLWorkbook(new MemoryStream(contenu));
        var ws = wb.Worksheets.First();
        var plage = ws.RangeUsed();
        if (plage is null) return [];
        return Tableau(plage.Rows().Select(r => r.Cells(1, plage.ColumnCount()).Select(c =>
            c.DataType == XLDataType.Number ? c.GetDouble().ToString("0.####", CultureInfo.InvariantCulture) : c.GetString()).ToArray()).ToList());
    }
}
