using System.Globalization;
using MigraDoc.DocumentObjectModel;
using Progix.Modules.Facturation.Domain;

namespace Progix.Modules.Facturation.Pdf;

/// <summary>Bordereaux de plans et attestation de conformité, d'après les modèles de l'entreprise.</summary>
public static class CourrierPdf
{
    private static readonly CultureInfo Fr = new("fr-FR");

    public static byte[] Generer(Courrier c, ParametresEntite e, string lienVerification)
    {
        var (doc, s) = DocumentsPdf.Cadre(e, c.Numero);
        var date = s.AddParagraph($"{c.Lieu}, le {c.DateCourrier:dd/MM/yyyy}"); date.Format.Alignment = ParagraphAlignment.Right; date.Format.Font.Size = 9.5;
        void Champ(string label, string? valeur)
        {
            if (string.IsNullOrWhiteSpace(valeur)) return;
            var x = s.AddParagraph(); x.Format.Font.Size = 10; x.Format.SpaceBefore = "0.35cm";
            x.AddFormattedText($"{label} : ", TextFormat.Bold); x.AddText(valeur);
        }
        void Titre(string texte)
        {
            var x = s.AddParagraph(texte); x.Format.Font.Size = 14; x.Format.Font.Bold = true; x.Format.Font.Color = DocumentsPdf.Marine; x.Format.Alignment = ParagraphAlignment.Center;
            x.Format.SpaceBefore = "0.7cm"; x.Format.SpaceAfter = "0.4cm";
        }
        if (c.Annule)
        {
            var a = s.AddParagraph($"COURRIER ANNULÉ : {c.MotifAnnulation}"); a.Format.Font.Bold = true; a.Format.Font.Color = new Color(0xA6, 0x1B, 0x1B); a.Format.SpaceBefore = "0.2cm";
        }
        var reference = $"{c.Numero}{(c.Modification ? "   ·   MODIFICATION" : "")}{(c.AncienNumero is null ? "" : $"   ·   ancien numéro {c.AncienNumero}")}";

        if (c.Type == TypesCourrier.Attestation)
        {
            Titre("ATTESTATION DE CONFORMITÉ");
            Champ("N/Réf", reference);
            Champ("Objet", c.Objet);
            Champ("Entreprise de construction", c.Entreprise);
            var intro = s.AddParagraph($"Nous soussignés, {e.RaisonSociale}, attestons par la présente que les dalles suivantes :"); intro.Format.Font.Size = 10; intro.Format.SpaceBefore = "0.6cm"; intro.Format.Alignment = ParagraphAlignment.Justify;
            foreach (var l in c.Lignes)
            {
                var x = s.AddParagraph($"•  {l.Designation}{(l.Reference is null ? "" : $" ({l.Reference})")}."); x.Format.Font.Size = 10; x.Format.LeftIndent = "1cm"; x.Format.SpaceBefore = "0.12cm";
            }
            var corps = s.AddParagraph("ont été réalisées par notre procédé en post-tension. Les plans d'exécution ont reçu le visa favorable du Bureau de Contrôle Technique " + c.Bct
                + ". L'installation, la mise en tension et l'injection ont été réalisées par nos équipes conformément aux plans et selon les règles de l'art et normes en vigueur. "
                + "En ce sens, les dalles ne présentent aucun risque de ruine dans la limite de leur utilisation prévue aux plans d'exécution.");
            corps.Format.Font.Size = 10; corps.Format.SpaceBefore = "0.4cm"; corps.Format.Alignment = ParagraphAlignment.Justify;
            var fin = s.AddParagraph("Cette attestation est délivrée à l'intéressé pour servir et faire valoir ce que de droit."); fin.Format.Font.Size = 10; fin.Format.SpaceBefore = "0.4cm"; fin.Format.SpaceAfter = "0.3cm";
            DocumentsPdf.Signature(s, c.SignePar ?? "", c.SigneLe ?? c.CreatedAt, lienVerification, "cette attestation");
            Cases(s, "Responsable étude : signature", "Responsable d'exécution : signature");
            return DocumentsPdf.Rendu(doc);
        }

        var bct = c.Type == TypesCourrier.BordereauBct;
        Titre(bct ? "BORDEREAU D'ENVOI" : $"BORDEREAU DE DÉPÔT : {c.Numero}");
        if (bct || c.Modification || c.AncienNumero is not null) Champ("Réf", reference);
        if (bct) Champ("Bureau de contrôle", c.Bct);
        Champ("Bureau d'études", c.Bet);
        Champ("Objet", c.Objet);
        Champ("ETP", c.Entreprise);
        Champ("Projet", c.Ville);
        var p = s.AddParagraph("Nous vous prions de trouver ci-dessous :"); p.Format.Font.Size = 10; p.Format.SpaceBefore = "0.6cm"; p.Format.SpaceAfter = "0.2cm";

        var t = s.AddTable(); t.Borders.Width = 0.5; t.Borders.Color = new Color(0xCF, 0xD4, 0xE2); t.LeftPadding = 5; t.RightPadding = 5;
        var colonnes = bct ? new (double, string, bool)[] { (5.4, "Plan n°", false), (7.2, "Désignation", false), (2.0, "Nombre", true), (2.8, "Surface en m²", true) }
                           : [(6.0, "Plan n°", false), (9.0, "Désignation", false), (2.4, "Nombre", true)];
        foreach (var col in colonnes) t.AddColumn($"{col.Item1.ToString(CultureInfo.InvariantCulture)}cm").Format.Alignment = col.Item3 ? ParagraphAlignment.Right : ParagraphAlignment.Left;
        var h = t.AddRow(); h.HeadingFormat = true; h.Shading.Color = DocumentsPdf.Marine; h.TopPadding = 4; h.BottomPadding = 4;
        for (var i = 0; i < colonnes.Length; i++) { var x = h.Cells[i].AddParagraph(colonnes[i].Item2); x.Format.Font.Color = new Color(0xFF, 0xFF, 0xFF); x.Format.Font.Bold = true; }
        foreach (var l in c.Lignes)
        {
            var r = t.AddRow(); r.TopPadding = 5; r.BottomPadding = 5;
            r.Cells[0].AddParagraph(l.NumeroPlan ?? ""); r.Cells[1].AddParagraph(l.Designation); r.Cells[2].AddParagraph(l.Nombre.ToString());
            if (bct) r.Cells[3].AddParagraph(l.Surface is null ? "" : l.Surface.Value.ToString("N2", Fr).Replace(' ', ' '));
        }
        var bonne = s.AddParagraph("Nous vous en souhaitons une bonne réception."); bonne.Format.Font.Size = 10; bonne.Format.SpaceBefore = "0.6cm"; bonne.Format.SpaceAfter = "0.3cm";
        DocumentsPdf.Signature(s, c.SignePar ?? "", c.SigneLe ?? c.CreatedAt, lienVerification, "ce bordereau", "La Direction : cachet et signature");
        Cases(s, "Reçu par : nom, date et signature", "");
        return DocumentsPdf.Rendu(doc);
    }

    /// <summary>Cases de signature manuscrite, apposée après impression.</summary>
    private static void Cases(Section s, string gauche, string droite)
    {
        s.AddParagraph().Format.SpaceAfter = "0.2cm";
        var t = s.AddTable(); t.KeepTogether = true; t.Borders.Width = 0.5; t.Borders.Color = new Color(0xCF, 0xD4, 0xE2); t.LeftPadding = 6; t.RightPadding = 6;
        var n = droite.Length > 0 ? 2 : 1;
        for (var i = 0; i < n; i++) t.AddColumn($"{(DocumentsPdf.Largeur / n).ToString(CultureInfo.InvariantCulture)}cm");
        var r = t.AddRow(); r.Height = "3cm"; r.TopPadding = 6;
        var textes = new[] { gauche, droite };
        for (var i = 0; i < n; i++) { var x = r.Cells[i].AddParagraph(textes[i]); x.Format.Font.Size = 8; x.Format.Font.Bold = true; x.Format.Font.Color = DocumentsPdf.Gris; }
    }
}
