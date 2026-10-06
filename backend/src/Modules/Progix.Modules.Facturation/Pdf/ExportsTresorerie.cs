using System.Globalization;
using ClosedXML.Excel;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Progix.Modules.Facturation.Domain;
using Progix.Modules.Facturation.Endpoints;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Pdf;

/// <summary>Exports du journal de trésorerie : classeur Excel et PDF, avec les filtres de l'écran.</summary>
public static class ExportsTresorerie
{
    private static readonly CultureInfo Fr = new("fr-FR");
    private static string Nb(decimal v) => v.ToString("N2", Fr).Replace(' ', ' ').Replace(' ', ' ');
    private static string Mode(string m) => ModesPaiement.Libelles.GetValueOrDefault(m, m);
    private static string Type(string t) => TresorerieEndpoints.Types.TryGetValue(t, out var x) ? x.Singulier : t;
    private static string Mois(DateOnly d) => d.ToString("MMMM yyyy", Fr);

    private static string Filtres(TresorerieEndpoints.Journal j)
    {
        var f = new List<string> { j.Entite is { Length: > 0 } e ? $"Entité : {Entites.Labels.GetValueOrDefault(e, e)}" : "Toutes les entités" };
        if (j.Type is { Length: > 0 } t && TresorerieEndpoints.Types.TryGetValue(t, out var tt)) f.Add(tt.Pluriel);
        if (j.Mode is { Length: > 0 } m) f.Add($"Mode : {Mode(m)}");
        if (!string.IsNullOrWhiteSpace(j.Recherche)) f.Add($"Recherche : {j.Recherche.Trim()}");
        return string.Join(" · ", f);
    }

    public static byte[] Excel(TresorerieEndpoints.Journal j)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Journal");
        ws.Cell(1, 1).Value = $"Journal de trésorerie, {Mois(j.Debut)}"; ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = Filtres(j);
        var titres = new[] { "Date", "Type", "Libellé", "Projet ou dalle", "Entité", "Mode", "Reçu", "Saisi par", "Montant (DH)" };
        for (var c = 0; c < titres.Length; c++) ws.Cell(4, c + 1).Value = titres[c];
        var entete = ws.Range(4, 1, 4, titres.Length);
        entete.Style.Font.Bold = true; entete.Style.Font.FontColor = XLColor.White; entete.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B2A6B");
        var r = 5;
        foreach (var m in j.Mouvements.OrderBy(x => x.Date).ThenBy(x => x.Type))
        {
            ws.Cell(r, 1).Value = m.Date.ToDateTime(TimeOnly.MinValue); ws.Cell(r, 1).Style.DateFormat.Format = "dd/mm/yyyy";
            ws.Cell(r, 2).Value = Type(m.Type); ws.Cell(r, 3).Value = m.Libelle; ws.Cell(r, 4).Value = m.Objet; ws.Cell(r, 5).Value = m.Entite;
            ws.Cell(r, 6).Value = Mode(m.Mode); ws.Cell(r, 7).Value = m.Recu ?? ""; ws.Cell(r, 8).Value = m.SaisiPar; ws.Cell(r, 9).Value = m.Montant;
            r++;
        }
        ws.Cell(r, 8).Value = "Total des mouvements affichés"; ws.Cell(r, 8).Style.Font.Bold = true;
        // Une formule plutôt qu'un total figé : il suit les lignes si le classeur est retouché.
        if (r > 5) ws.Cell(r, 9).FormulaA1 = $"SUM(I5:I{r - 1})"; else ws.Cell(r, 9).Value = 0;
        ws.Cell(r, 9).Style.Font.Bold = true;
        ws.Range(5, 9, r, 9).Style.NumberFormat.Format = "#,##0.00";
        ws.SheetView.FreezeRows(4);
        if (r > 5) ws.Range(4, 1, r - 1, titres.Length).SetAutoFilter();
        ws.Columns().AdjustToContents(4, r);
        ws.Column(3).Width = Math.Min(ws.Column(3).Width, 60);

        var syn = wb.AddWorksheet("Synthèse");
        syn.Cell(1, 1).Value = $"Synthèse, {Mois(j.Debut)}"; syn.Cell(1, 1).Style.Font.Bold = true; syn.Cell(1, 1).Style.Font.FontSize = 14;
        syn.Cell(2, 1).Value = j.Entite is { Length: > 0 } en ? $"Entité : {Entites.Labels.GetValueOrDefault(en, en)}" : "Toutes les entités";
        syn.Cell(4, 2).Value = Mois(j.Debut); syn.Cell(4, 3).Value = Mois(j.Debut.AddMonths(-1)); syn.Range(4, 1, 4, 3).Style.Font.Bold = true;
        var lignes = new (string, decimal, decimal)[] { ("Encaissé, remboursements déduits", j.Mois.Encaisse, j.Precedent.Encaisse), ("Dépenses", j.Mois.Depenses, j.Precedent.Depenses), ("Solde", j.Mois.Solde, j.Precedent.Solde) };
        for (var i = 0; i < lignes.Length; i++) { syn.Cell(5 + i, 1).Value = lignes[i].Item1; syn.Cell(5 + i, 2).Value = lignes[i].Item2; syn.Cell(5 + i, 3).Value = lignes[i].Item3; }
        syn.Range(5, 2, 7, 3).Style.NumberFormat.Format = "#,##0.00"; syn.Range(7, 1, 7, 3).Style.Font.Bold = true;
        syn.Cell(9, 1).Value = "La synthèse porte sur tout le mois, sans les filtres de type, de mode ou de recherche."; syn.Cell(9, 1).Style.Font.Italic = true;
        syn.Columns().AdjustToContents(4, 8);

        if (j.AEchoir.Count > 0)
        {
            var ef = wb.AddWorksheet("Effets à échoir");
            var t = new[] { "Échéance", "Reçu le", "Client", "Facture", "Numéro de l'effet", "Banque", "Entité", "Montant (DH)" };
            for (var c = 0; c < t.Length; c++) ef.Cell(1, c + 1).Value = t[c];
            ef.Range(1, 1, 1, t.Length).Style.Font.Bold = true;
            var x = 2;
            foreach (var e in j.AEchoir.OrderBy(v => v.Echeance))
            {
                ef.Cell(x, 1).Value = e.Echeance.ToDateTime(TimeOnly.MinValue); ef.Cell(x, 2).Value = e.RecuLe.ToDateTime(TimeOnly.MinValue);
                ef.Cell(x, 3).Value = e.Client; ef.Cell(x, 4).Value = e.Facture; ef.Cell(x, 5).Value = e.Reference ?? ""; ef.Cell(x, 6).Value = e.Banque ?? ""; ef.Cell(x, 7).Value = e.Entite; ef.Cell(x, 8).Value = e.Montant;
                x++;
            }
            ef.Range(2, 1, x, 2).Style.DateFormat.Format = "dd/mm/yyyy"; ef.Range(2, 8, x, 8).Style.NumberFormat.Format = "#,##0.00";
            ef.Columns().AdjustToContents();
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static byte[] Pdf(TresorerieEndpoints.Journal j, string editePar)
    {
        Polices.Installer();
        var marine = new Color(0x1B, 0x2A, 0x6B); var gris = new Color(0x5B, 0x61, 0x78); var trait = new Color(0xCF, 0xD4, 0xE2); var blanc = new Color(0xFF, 0xFF, 0xFF);
        var doc = new Document { Info = { Title = $"Journal de trésorerie, {Mois(j.Debut)}" } };
        var normal = doc.Styles[StyleNames.Normal]!; normal.Font.Name = Polices.Famille; normal.Font.Size = 8;
        var s = doc.AddSection();
        s.PageSetup = doc.DefaultPageSetup.Clone();
        // A4 paysage : dimensions données explicitement, la copie des réglages par défaut fige le format portrait.
        s.PageSetup.PageWidth = "29.7cm"; s.PageSetup.PageHeight = "21cm";
        s.PageSetup.LeftMargin = s.PageSetup.RightMargin = "1.5cm"; s.PageSetup.TopMargin = "1.5cm"; s.PageSetup.BottomMargin = "1.8cm";
        const double largeur = 26.7;

        var titre = s.AddParagraph($"Journal de trésorerie, {Mois(j.Debut)}"); titre.Format.Font.Size = 15; titre.Format.Font.Bold = true; titre.Format.Font.Color = marine;
        var sous = s.AddParagraph(Filtres(j)); sous.Format.Font.Color = gris; sous.Format.SpaceAfter = "0.4cm";

        var k = s.AddTable(); k.Borders.Width = 0.5; k.Borders.Color = trait; k.LeftPadding = 6; k.RightPadding = 6;
        for (var i = 0; i < 4; i++) k.AddColumn($"{(largeur / 4).ToString(CultureInfo.InvariantCulture)}cm");
        var kr = k.AddRow(); kr.TopPadding = 5; kr.BottomPadding = 5;
        var cartes = new (string, decimal)[] { ("Encaissé, remboursements déduits", j.Mois.Encaisse), ("Dépenses", j.Mois.Depenses), ("Solde du mois", j.Mois.Solde), ($"Solde de {Mois(j.Debut.AddMonths(-1))}", j.Precedent.Solde) };
        for (var i = 0; i < 4; i++)
        {
            var l = kr.Cells[i].AddParagraph(cartes[i].Item1); l.Format.Font.Color = gris;
            var v = kr.Cells[i].AddParagraph(Nb(cartes[i].Item2) + " DH"); v.Format.Font.Size = 11; v.Format.Font.Bold = true;
        }
        s.AddParagraph().Format.SpaceAfter = "0.3cm";

        var t = s.AddTable(); t.Borders.Visible = false; t.LeftPadding = 4; t.RightPadding = 4;
        var colonnes = new (double, string, bool)[] { (2.2, "Date", false), (3.4, "Type", false), (7.3, "Libellé", false), (4.2, "Projet ou dalle", false), (1.5, "Entité", false), (2.2, "Mode", false), (2.8, "Reçu", false), (3.1, "Montant (DH)", true) };
        foreach (var c in colonnes) t.AddColumn($"{c.Item1.ToString(CultureInfo.InvariantCulture)}cm").Format.Alignment = c.Item3 ? ParagraphAlignment.Right : ParagraphAlignment.Left;
        var h = t.AddRow(); h.HeadingFormat = true; h.Shading.Color = marine; h.TopPadding = 4; h.BottomPadding = 4;
        for (var i = 0; i < colonnes.Length; i++) { var p = h.Cells[i].AddParagraph(colonnes[i].Item2); p.Format.Font.Color = blanc; p.Format.Font.Bold = true; }
        foreach (var m in j.Mouvements.OrderBy(x => x.Date).ThenBy(x => x.Type))
        {
            var r = t.AddRow(); r.TopPadding = 3; r.BottomPadding = 3; r.Borders.Bottom.Width = 0.5; r.Borders.Bottom.Color = trait;
            var v = new[] { m.Date.ToString("dd/MM/yyyy"), Type(m.Type), m.Libelle, m.Objet, m.Entite, Mode(m.Mode), m.Recu ?? "", Nb(m.Montant) };
            for (var i = 0; i < v.Length; i++) r.Cells[i].AddParagraph(v[i]);
        }
        var tot = t.AddRow(); tot.TopPadding = 4; tot.BottomPadding = 4; tot.Shading.Color = new Color(0xF4, 0xF5, 0xF9);
        tot.Cells[0].MergeRight = 6; var tl = tot.Cells[0].AddParagraph($"Total des {j.Mouvements.Count} mouvement(s) affiché(s)"); tl.Format.Font.Bold = true;
        tot.Cells[7].AddParagraph(Nb(j.Mouvements.Sum(m => m.Montant))).Format.Font.Bold = true;

        if (j.AEchoir.Count > 0)
        {
            var e = s.AddParagraph($"Effets à échoir : {j.AEchoir.Count} effet(s), {Nb(j.AEchoir.Sum(x => x.Montant))} DH, comptés à leur date d'échéance.");
            e.Format.SpaceBefore = "0.4cm"; e.Format.Font.Bold = true;
            foreach (var x in j.AEchoir.OrderBy(v => v.Echeance))
                s.AddParagraph($"Échéance le {x.Echeance:dd/MM/yyyy} · {x.Client} · {x.Facture}{(x.Reference is null ? "" : $" · effet {x.Reference}")} · {Nb(x.Montant)} DH");
        }

        var pied = s.Footers.Primary.AddParagraph(); pied.Format.Font.Size = 7; pied.Format.Font.Color = gris;
        pied.Format.TabStops.AddTabStop($"{largeur.ToString(CultureInfo.InvariantCulture)}cm", TabAlignment.Right);
        pied.AddText($"PROGIX · document interne, édité par {editePar} le {DateTime.UtcNow.AddHours(1):dd/MM/yyyy 'à' HH:mm}");
        pied.AddTab(); pied.AddText("Page "); pied.AddPageField(); pied.AddText(" sur "); pied.AddNumPagesField();

        var rendu = new PdfDocumentRenderer { Document = doc };
        rendu.RenderDocument();
        using var ms = new MemoryStream();
        rendu.PdfDocument.Save(ms);
        return ms.ToArray();
    }
}
