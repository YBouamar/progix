using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Devis.Domain;
using Progix.Modules.Devis.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Devis.Endpoints;

/// <summary>
/// Import Excel d'un devis : une ligne par dalle, reconnue par sa référence. Les colonnes de systèmes sont reconnues par leur nom (« S4 ancrages », « S4 gaine »).
/// L'import ne modifie rien : il renvoie les lignes lues, que l'éditeur reporte dans le brouillon.
/// </summary>
public static partial class DevisImportEndpoints
{
    private const int TailleMax = 5 * 1024 * 1024;

    [GeneratedRegex(@"^S\s*(\d{1,2})\s*(ancrages?|gaines?)$", RegexOptions.IgnoreCase)]
    private static partial Regex Systeme();

    private static string Norme(string s) => new string(s.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/devis/{id:guid}/import").WithTags("Devis").RequireAuthorization(Policies.Devis);

        // Modèle prérempli avec les dalles du devis et les valeurs déjà saisies.
        g.MapGet("/modele", async (Guid id, DevisDbContext db) =>
        {
            var d = await db.Devis.AsNoTracking().Include(x => x.Lignes).FirstOrDefaultAsync(x => x.Id == id);
            if (d is null) return Results.NotFound();
            var systemes = d.Lignes.SelectMany(l => l.Systemes.Select(s => s.Torons)).Distinct().OrderBy(x => x).ToList();
            if (systemes.Count == 0) systemes = [4];
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Dalles");
            var titres = new List<string> { "Référence", "Niveau", "Surface", "Ratio actif", "Ratio passif" };
            foreach (var t in systemes) { titres.Add($"S{t} ancrages"); titres.Add($"S{t} gaine"); }
            for (var c = 0; c < titres.Count; c++) ws.Cell(1, c + 1).Value = titres[c];
            var entete = ws.Range(1, 1, 1, titres.Count); entete.Style.Font.Bold = true; entete.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B2A6B"); entete.Style.Font.FontColor = XLColor.White;
            var r = 2;
            foreach (var l in d.Lignes.OrderBy(x => x.Ordre))
            {
                ws.Cell(r, 1).Value = l.Reference; ws.Cell(r, 2).Value = l.Emplacement; ws.Cell(r, 3).Value = l.Surface; ws.Cell(r, 4).Value = l.RatioActif; ws.Cell(r, 5).Value = l.RatioPassif;
                for (var i = 0; i < systemes.Count; i++)
                {
                    var s = l.Systemes.FirstOrDefault(x => x.Torons == systemes[i]);
                    if (s is null) continue;
                    ws.Cell(r, 6 + i * 2).Value = s.Ancrages; ws.Cell(r, 7 + i * 2).Value = s.Gaine;
                }
                r++;
            }
            ws.Columns().AdjustToContents();
            var aide = wb.AddWorksheet("Aide");
            string[] lignes =
            [
                "Une ligne par dalle. La colonne Référence identifie la dalle : ne la modifiez pas.",
                "Niveau et Surface sont repris de la dalle pour vous repérer. La surface ne se change pas ici : elle vient de la dalle.",
                "Ratio actif et Ratio passif : en kg/m².",
                "Systèmes : ajoutez deux colonnes par système, nommées « S4 ancrages » et « S4 gaine » (le nombre est celui des torons).",
                "Ancrages : nombre d'ancrages. Gaine : longueur en mètres.",
            ];
            for (var i = 0; i < lignes.Length; i++) aide.Cell(i + 1, 1).Value = lignes[i];
            aide.Column(1).Width = 110;
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return Results.File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"modele-{d.Numero}.xlsx");
        });

        g.MapPost("/", async (Guid id, IFormFile fichier, DevisDbContext db) =>
        {
            var d = await db.Devis.AsNoTracking().Include(x => x.Lignes).FirstOrDefaultAsync(x => x.Id == id);
            if (d is null) return Results.NotFound();
            if (d.Etat != EtatsDevis.Brouillon) return DevisEndpoints.Conflit("Seul un devis en brouillon peut recevoir un import.");
            if (fichier.Length is 0 or > TailleMax) return DevisEndpoints.Conflit("Le fichier Excel pèse au plus 5 Mo.");
            XLWorkbook wb;
            try { wb = new XLWorkbook(fichier.OpenReadStream()); }
            catch { return DevisEndpoints.Conflit("Fichier illisible : envoyez un classeur Excel au format .xlsx."); }
            using var _ = wb;
            var ws = wb.Worksheets.First();
            var derniere = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
            int colRef = 0, colSurface = 0, colActif = 0, colPassif = 0, colNiveau = 0;
            var colSystemes = new List<(int Col, int Torons, bool Ancrages)>();
            var inconnues = new List<string>();
            for (var c = 1; c <= derniere; c++)
            {
                var brut = ws.Cell(1, c).GetString();
                if (string.IsNullOrWhiteSpace(brut)) continue;
                var t = Norme(brut);
                if (t.StartsWith("reference")) colRef = c;
                else if (t.StartsWith("niveau") || t.StartsWith("emplacement")) colNiveau = c;
                else if (t.StartsWith("surface")) colSurface = c;
                else if (t.StartsWith("ratio actif")) colActif = c;
                else if (t.StartsWith("ratio passif")) colPassif = c;
                else if (Systeme().Match(t) is { Success: true } m) colSystemes.Add((c, int.Parse(m.Groups[1].Value), m.Groups[2].Value.StartsWith('a')));
                else inconnues.Add(brut.Trim());
            }
            if (colRef == 0) return DevisEndpoints.Conflit("Colonne « Référence » introuvable sur la première ligne. Partez du modèle Excel.");
            if (colActif == 0) return DevisEndpoints.Conflit("Colonne « Ratio actif » introuvable sur la première ligne. Partez du modèle Excel.");

            decimal? Nombre(IXLCell cell, out bool invalide)
            {
                invalide = false;
                if (cell.IsEmpty()) return null;
                // Un nombre saisi comme texte (« 4,1 ») se lit avec la virgule décimale, pas comme 41.
                if (cell.DataType == XLDataType.Number) return (decimal)cell.GetDouble();
                var s = cell.GetString().Trim().Replace(' ', ' ').Replace(" ", "").Replace(',', '.');
                if (s.Length == 0) return null;
                if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) return x;
                invalide = true; return null;
            }

            var vues = new HashSet<Guid>();
            var lignes = new List<object>();
            foreach (var row in ws.RowsUsed().Where(r => r.RowNumber() > 1))
            {
                var reference = row.Cell(colRef).GetString().Trim();
                if (reference.Length == 0 && row.CellsUsed().All(c => string.IsNullOrWhiteSpace(c.GetString()))) continue;
                var erreurs = new List<string>();
                var l = d.Lignes.FirstOrDefault(x => string.Equals(x.Reference, reference, StringComparison.OrdinalIgnoreCase));
                if (l is null) erreurs.Add(reference.Length == 0 ? "Référence manquante." : "Aucune dalle du devis ne porte cette référence.");
                else if (!vues.Add(l.Id)) erreurs.Add("Cette dalle figure déjà plus haut dans le fichier.");
                var actif = Nombre(row.Cell(colActif), out var ko1);
                if (ko1) erreurs.Add("Ratio actif illisible."); else if (actif is null) erreurs.Add("Ratio actif manquant."); else if (actif < 0) erreurs.Add("Ratio actif négatif.");
                decimal? passif = null;
                if (colPassif > 0) { passif = Nombre(row.Cell(colPassif), out var ko2); if (ko2) erreurs.Add("Ratio passif illisible."); else if (passif < 0) erreurs.Add("Ratio passif négatif."); }
                if (colSurface > 0 && l is not null)
                {
                    var surface = Nombre(row.Cell(colSurface), out var ko3);
                    if (ko3) erreurs.Add("Surface illisible.");
                    else if (surface is decimal sf && Math.Abs(sf - l.Surface) > 0.005m) erreurs.Add($"Surface différente de celle de la dalle ({l.Surface.ToString("N2", CultureInfo.GetCultureInfo("fr-FR"))} m²) : elle se modifie sur la dalle, pas dans le devis.");
                }
                var systemes = new List<object>();
                foreach (var grp in colSystemes.GroupBy(x => x.Torons).OrderBy(x => x.Key))
                {
                    decimal? a = null, gn = null;
                    foreach (var col in grp)
                    {
                        var v = Nombre(row.Cell(col.Col), out var ko);
                        if (ko) erreurs.Add($"S{grp.Key} : valeur illisible."); else if (v < 0) erreurs.Add($"S{grp.Key} : valeur négative.");
                        if (col.Ancrages) a = v; else gn = v;
                    }
                    if ((a ?? 0) > 0 || (gn ?? 0) > 0) systemes.Add(new { torons = grp.Key, ancrages = a ?? 0, gaine = gn ?? 0 });
                }
                lignes.Add(new
                {
                    ligne = row.RowNumber(), id = l?.Id, reference, niveau = l?.Emplacement ?? (colNiveau > 0 ? row.Cell(colNiveau).GetString() : ""), surface = l?.Surface,
                    ratioActif = actif, ratioPassif = passif, systemes, valide = erreurs.Count == 0, message = string.Join(" ", erreurs),
                });
            }
            return Results.Ok(new { fichier = fichier.FileName, colonnesIgnorees = inconnues, lignes });
        }).DisableAntiforgery();
    }
}
