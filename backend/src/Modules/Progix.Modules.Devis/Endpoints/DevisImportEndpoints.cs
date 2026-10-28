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
        // Trame et import : réservés aux rôles qui modifient les devis (pas l'administrateur).
        var g = app.MapGroup("/api/devis/{id:guid}/import").WithTags("Devis").RequireAuthorization(Policies.Devis)
            .RequireAuthorization(p => p.RequireAssertion(c => DevisEtatsEndpoints.PeutEcrire(c.User)));

        // Trame de calcul : dalles et projet préremplis, formules de PROGIX (prix au m², montants, forfait par tranche, totaux), à réimporter ensuite.
        g.MapGet("/modele", async (Guid id, DevisDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires) =>
        {
            var d = await db.Devis.AsNoTracking().Include(x => x.Lignes).FirstOrDefaultAsync(x => x.Id == id);
            if (d is null) return Results.NotFound();
            var projet = await projets.ProjetAsync(d.ProjetId);
            var client = projet is null ? null : (await partenaires.FichesAsync([projet.ClientId])).GetValueOrDefault(projet.ClientId);
            var grille = (await db.ParametresAsync()).Forfaits.OrderBy(t => t.APartirDe).ToList();
            var contenu = TrameCalcul(d, projet, client?.Nom, grille);
            return Results.File(contenu, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"trame-calcul-{d.Numero}-v{d.Version}.xlsx");
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
                else if (t.StartsWith("prix") || t.StartsWith("montant")) continue; // colonnes calculées de la trame : PROGIX recalcule lui-même
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
            var dejaSaisis = d.Lignes.SelectMany(x => x.Systemes.Select(s => s.Torons)).ToHashSet();
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
                    if ((a ?? 0) > 0 || (gn ?? 0) > 0)
                    {
                        // Types d'ancrage existants seulement, ou déjà présents sur ce devis.
                        if (!SystemesAncrage.Existe(grp.Key) && !dejaSaisis.Contains(grp.Key)) erreurs.Add($"S{grp.Key} : type d'ancrage inconnu ({SystemesAncrage.Liste}).");
                        else systemes.Add(new { torons = grp.Key, ancrages = a ?? 0, gaine = gn ?? 0 });
                    }
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

    /// <summary>Types d'ancrage proposés dans la trame : tous les types existants, plus ceux déjà saisis sur le devis.</summary>
    internal static IReadOnlyList<int> SystemesTrame => SystemesAncrage.Torons;

    private const string FeuilleSynthese = "Synthèse";

    /// <summary>
    /// Trame de calcul du devis : onglet « Dalles » (une ligne par dalle, à remplir, avec prix au m² et montant calculés),
    /// onglet « Synthèse » (projet, surface totale, forfait de la grille, totaux), onglet « Forfaits » (grille en vigueur) et « Aide ».
    /// Les formules reprennent celles de PROGIX : prix au m² = ENT((Σ ancrages × 100 × torons + Σ gaines × 3 × torons) / surface + ratio actif × 12 × 1,05 + forfait) + 1.
    /// </summary>
    internal static byte[] TrameCalcul(DevisEntete d, ProjetResume? projet, string? client, IReadOnlyList<TrancheForfait> grille)
    {
        var lignes = d.Lignes.OrderBy(x => x.Ordre).ToList();
        var systemes = lignes.SelectMany(l => l.Systemes.Select(s => s.Torons)).Union(SystemesTrame).Distinct().Order().ToList();
        var tranches = grille.Count > 0 ? grille.OrderBy(t => t.APartirDe).ToList() : ParametresDevis.ForfaitsParDefaut();
        var calcule = XLColor.FromHtml("#EEF1F8");
        using var wb = new XLWorkbook();

        // Onglet Dalles : en-têtes sur la première ligne, lus tels quels à l'import.
        var ws = wb.AddWorksheet("Dalles");
        var titres = new List<string> { "Référence", "Niveau", "Surface", "Ratio actif", "Ratio passif" };
        foreach (var t in systemes) { titres.Add($"S{t} ancrages"); titres.Add($"S{t} gaine"); }
        var colPrix = titres.Count + 1;
        titres.Add("Prix au m² (calculé)"); titres.Add("Montant HT (calculé)");
        for (var c = 0; c < titres.Count; c++) ws.Cell(1, c + 1).Value = titres[c];
        var entete = ws.Range(1, 1, 1, titres.Count);
        entete.Style.Font.Bold = true; entete.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B2A6B"); entete.Style.Font.FontColor = XLColor.White;
        string Col(int c) => XLHelper.GetColumnLetterFromNumber(c);
        var derniere = Math.Max(2, lignes.Count + 1);
        for (var i = 0; i < lignes.Count; i++)
        {
            var r = i + 2;
            var l = lignes[i];
            ws.Cell(r, 1).Value = l.Reference; ws.Cell(r, 2).Value = l.Emplacement; ws.Cell(r, 3).Value = l.Surface;
            ws.Cell(r, 4).Value = l.RatioActif; ws.Cell(r, 5).Value = l.RatioPassif;
            var termes = new List<string>();
            for (var k = 0; k < systemes.Count; k++)
            {
                var t = systemes[k];
                int ca = 6 + k * 2, cg = 7 + k * 2;
                if (l.Systemes.FirstOrDefault(x => x.Torons == t) is { } s) { ws.Cell(r, ca).Value = s.Ancrages; ws.Cell(r, cg).Value = s.Gaine; }
                // Une cellule vide compte pour zéro dans le calcul.
                termes.Add($"{Col(ca)}{r}*100*{t}+{Col(cg)}{r}*3*{t}");
            }
            var forfait = $"'{FeuilleSynthese}'!$B$8";
            ws.Cell(r, colPrix).FormulaA1 = $"IF(C{r}>0,INT(({string.Join("+", termes)})/C{r}+D{r}*12*1.05+{forfait})+1,0)";
            ws.Cell(r, colPrix + 1).FormulaA1 = $"ROUND({Col(colPrix)}{r}*C{r},2)";
        }
        // Repères : surface, niveau et colonnes calculées en fond gris, à ne pas modifier.
        ws.Range(2, 1, derniere, 3).Style.Fill.BackgroundColor = calcule;
        ws.Range(2, colPrix, derniere, colPrix + 1).Style.Fill.BackgroundColor = calcule;
        ws.Range(2, colPrix + 1, derniere, colPrix + 1).Style.NumberFormat.Format = "#,##0.00";
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        ws.SheetView.FreezeColumns(1);

        // Onglet Forfaits : la grille unique du Paramétrage, par tranche de surface totale.
        var wf = wb.AddWorksheet("Forfaits");
        wf.Cell(1, 1).Value = "À partir de (m²)"; wf.Cell(1, 2).Value = "Forfait (DH/m²)";
        wf.Range(1, 1, 1, 2).Style.Font.Bold = true;
        for (var i = 0; i < tranches.Count; i++) { wf.Cell(i + 2, 1).Value = tranches[i].APartirDe; wf.Cell(i + 2, 2).Value = tranches[i].Prix; }
        wf.Columns().AdjustToContents();

        // Onglet Synthèse : totaux du devis, calculés comme dans PROGIX.
        var sy = wb.AddWorksheet(FeuilleSynthese);
        var montant = $"Dalles!{Col(colPrix + 1)}2:{Col(colPrix + 1)}{derniere}";
        var taux = d.ExonereTva ? 0 : Calcul.TauxTva;
        (string Libelle, Action<IXLCell> Valeur)[] lignesSynthese =
        [
            ("Projet", c => c.Value = projet is null ? "" : $"{projet.Code} · {projet.Libelle}"),
            ("Client", c => c.Value = client ?? ""),
            ("Type de construction", c => c.Value = projet?.TypeLabel ?? projet?.TypeConstruction ?? ""),
            ("Devis", c => c.Value = $"{d.Numero} · version {d.Version}"),
            ("Ingénieur du devis", c => c.Value = d.IngenieurNom ?? ""),
            ("", _ => { }),
            ("Surface totale (m²)", c => c.FormulaA1 = $"SUM(Dalles!C2:C{derniere})"),
            ("Forfait (DH/m²), selon la grille", c => c.FormulaA1 = $"VLOOKUP(MAX(B7,{tranches[0].APartirDe.ToString(CultureInfo.InvariantCulture)}),Forfaits!$A$2:$B${tranches.Count + 1},2,TRUE)"),
            ("Total HT (DH)", c => c.FormulaA1 = $"SUM({montant})"),
            ("Remise au m² (DH)", c => c.Value = d.RemiseM2),
            ("Remise totale (DH)", c => c.FormulaA1 = "MIN(B9,ROUND(B10*B7,2))"),
            ("Net HT (DH)", c => c.FormulaA1 = "B9-B11"),
            ($"TVA {(taux * 100).ToString("0", CultureInfo.InvariantCulture)} % (DH)", c => c.FormulaA1 = $"ROUND(B12*{taux.ToString(CultureInfo.InvariantCulture)},2)"),
            ("Total TTC (DH)", c => c.FormulaA1 = "B12+B13"),
        ];
        for (var i = 0; i < lignesSynthese.Length; i++)
        {
            sy.Cell(i + 1, 1).Value = lignesSynthese[i].Libelle;
            lignesSynthese[i].Valeur(sy.Cell(i + 1, 2));
        }
        sy.Range(1, 1, lignesSynthese.Length, 1).Style.Font.Bold = true;
        sy.Range(7, 2, 14, 2).Style.NumberFormat.Format = "#,##0.00";
        sy.Range(7, 2, 14, 2).Style.Fill.BackgroundColor = calcule;
        sy.Cell(10, 2).Style.Fill.BackgroundColor = XLColor.NoColor;
        sy.Columns().AdjustToContents();

        var aide = wb.AddWorksheet("Aide");
        string[] textes =
        [
            "Trame de calcul du devis, préremplie avec les dalles du projet. Remplissez l'onglet « Dalles », puis réimportez le fichier depuis la fiche du devis (Importer depuis Excel).",
            "Une ligne par dalle. La colonne Référence identifie la dalle : ne la modifiez pas. Niveau et Surface viennent de la dalle et ne se changent pas ici.",
            $"À remplir : Ratio actif et Ratio passif (kg/m²), puis pour chaque type d'ancrage ({SystemesAncrage.Liste}) le nombre d'ancrages et la longueur de gaine en mètres. Laissez vides les types non utilisés.",
            "Le type d'ancrage S4 compte 4 torons, S12 en compte 12 : ce nombre entre dans le calcul du prix. Aucun autre type n'est accepté à l'import.",
            "Les colonnes sur fond gris sont calculées : prix au m² = ENT((Σ ancrages × 100 × torons + Σ gaines × 3 × torons) / surface + ratio actif × 12 × 1,05 + forfait) + 1.",
            "Le forfait vient de la grille de l'onglet « Forfaits » (Paramétrage de PROGIX), selon la surface totale du devis. L'onglet « Synthèse » donne les totaux du devis.",
            "À l'import, PROGIX relit les ratios et les systèmes et recalcule lui-même les prix : les colonnes calculées ne sont pas importées.",
        ];
        for (var i = 0; i < textes.Length; i++) aide.Cell(i + 1, 1).Value = textes[i];
        aide.Column(1).Width = 150;
        wb.Worksheet("Dalles").SetTabActive();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
