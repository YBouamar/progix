using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using Progix.Modules.Facturation.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Pdf;

/// <summary>Mise en page commune des fiches de chantier : en-tête de l'entité, repères du chantier, tableau de la fiche, synthèse et signatures.</summary>
public static class FichePdf
{
    public static byte[] Generer(FicheChantier f, ProjetResume p, string client, ParametresEntite e, SignatureFiche signature, string lienVerification)
    {
        var (doc, s) = DocumentsPdf.Cadre(e, $"{f.Titre} {f.Reference}");
        var trait = new Color(0xCF, 0xD4, 0xE2); var fond = new Color(0xF4, 0xF5, 0xF9); var blanc = new Color(0xFF, 0xFF, 0xFF);
        var titre = s.AddParagraph(f.Titre); titre.Format.Font.Size = 14; titre.Format.Font.Bold = true; titre.Format.Font.Color = DocumentsPdf.Marine; titre.Format.Alignment = ParagraphAlignment.Center; titre.Format.SpaceAfter = "0.3cm";

        // Repères : deux colonnes de « libellé : valeur ».
        var reperes = new List<KeyValuePair<string, string>>
        {
            new("Client", client), new("Chantier", p.Libelle), new("Projet", p.Code), new("Dalle", f.Reference),
            new("Réalisée par", f.FaitPar ?? ""), new("Date", f.FaitLe is null ? "" : f.FaitLe.Value.AddHours(1).ToString("dd/MM/yyyy")),
        };
        reperes.AddRange(f.Entete);
        var r = s.AddTable(); r.Borders.Width = 0.5; r.Borders.Color = trait; r.LeftPadding = 6; r.RightPadding = 6;
        var demi = (DocumentsPdf.Largeur / 2).ToString(CultureInfo.InvariantCulture);
        r.AddColumn($"{demi}cm"); r.AddColumn($"{demi}cm");
        var utiles = reperes.Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToList();
        for (var i = 0; i < utiles.Count; i += 2)
        {
            var row = r.AddRow(); row.TopPadding = 3; row.BottomPadding = 3;
            for (var j = 0; j < 2 && i + j < utiles.Count; j++)
            {
                var x = row.Cells[j].AddParagraph(); x.AddFormattedText($"{utiles[i + j].Key} : ", new Font { Color = DocumentsPdf.Gris }); x.AddFormattedText(utiles[i + j].Value, TextFormat.Bold);
            }
        }
        s.AddParagraph().Format.SpaceAfter = "0.25cm";

        // Tableau : la première colonne prend la place restante quand elle porte un libellé long.
        var n = f.Colonnes.Count;
        var large = f.Type == "auto-controle";
        var t = s.AddTable(); t.Borders.Width = 0.5; t.Borders.Color = trait; t.LeftPadding = 3; t.RightPadding = 3;
        double[] largeurs = large ? [7.2, 1.1, 1.1, 1.1, DocumentsPdf.Largeur - 10.5] : Enumerable.Repeat(DocumentsPdf.Largeur / n, n).ToArray();
        for (var i = 0; i < n; i++) t.AddColumn($"{largeurs[i].ToString(CultureInfo.InvariantCulture)}cm").Format.Alignment = large && i is 0 or 4 ? ParagraphAlignment.Left : ParagraphAlignment.Center;
        var h = t.AddRow(); h.HeadingFormat = true; h.Shading.Color = DocumentsPdf.Marine; h.TopPadding = 4; h.BottomPadding = 4; h.VerticalAlignment = VerticalAlignment.Center;
        // Un tableau très large passe dans une police plus petite.
        if (n > 9) t.Format.Font.Size = 7.5;
        for (var i = 0; i < n; i++) { var x = h.Cells[i].AddParagraph(f.Colonnes[i]); x.Format.Font.Color = blanc; x.Format.Font.Bold = true; x.Format.Font.Size = n > 9 ? 7 : 7.5; }
        foreach (var ligne in f.Lignes)
        {
            var row = t.AddRow(); row.TopPadding = 3.5; row.BottomPadding = 3.5;
            if (ligne.Count == 1)
            {
                // Titre de section sur toute la largeur.
                row.Cells[0].MergeRight = n - 1; row.Shading.Color = fond;
                var x = row.Cells[0].AddParagraph(ligne[0]); x.Format.Font.Bold = true; x.Format.Alignment = ParagraphAlignment.Left; x.Format.Font.Color = DocumentsPdf.Marine;
                continue;
            }
            for (var i = 0; i < Math.Min(n, ligne.Count); i++)
            {
                var x = row.Cells[i].AddParagraph(ligne[i]);
                if (ligne[i] is "X" or "Non") x.Format.Font.Bold = true;
            }
        }
        if (f.Lignes.Count == 0) { var vide = t.AddRow(); vide.Cells[0].MergeRight = n - 1; vide.Cells[0].AddParagraph("Aucune ligne saisie."); }

        if (f.Synthese.Any(x => !string.IsNullOrWhiteSpace(x.Value)))
        {
            s.AddParagraph().Format.SpaceAfter = "0.2cm";
            var bloc = DocumentsPdf.Bloc(s.AddTable(), DocumentsPdf.Largeur, "SYNTHÈSE");
            foreach (var x in f.Synthese.Where(x => !string.IsNullOrWhiteSpace(x.Value))) DocumentsPdf.Mention(bloc.AddParagraph(), x.Key, x.Value);
        }
        if (!string.IsNullOrWhiteSpace(f.Remarque))
        {
            var rem = s.AddParagraph(); rem.Format.SpaceBefore = "0.25cm"; rem.AddFormattedText("Remarque : ", TextFormat.Bold); rem.AddText(f.Remarque);
        }

        DocumentsPdf.Signature(s, signature.SignePar, signature.SigneLe, lienVerification, "cette fiche", $"Exécution, {e.RaisonSociale} : nom, date et signature");
        // Signature de l'entreprise ou du chantier : celle saisie au doigt sur la tablette si elle existe, sinon une case à signer.
        s.AddParagraph().Format.SpaceAfter = "0.2cm";
        var c = s.AddTable(); c.KeepTogether = true; c.Borders.Width = 0.5; c.Borders.Color = trait; c.LeftPadding = 6; c.RightPadding = 6;
        c.AddColumn($"{DocumentsPdf.Largeur.ToString(CultureInfo.InvariantCulture)}cm");
        var ligneC = c.AddRow(); ligneC.Height = "3cm"; ligneC.TopPadding = 6;
        var lib = ligneC.Cells[0].AddParagraph($"{(f.Type == "auto-controle" ? "Chantier" : "Entreprise, client")} : nom, date et signature{(string.IsNullOrWhiteSpace(f.ClientNom) ? "" : $" · {f.ClientNom}")}");
        lib.Format.Font.Size = 8; lib.Format.Font.Bold = true; lib.Format.Font.Color = DocumentsPdf.Gris;
        const string prefixe = "data:image/png;base64,";
        if (f.ClientSignature is { Length: > 0 } sig && sig.StartsWith(prefixe))
        {
            var img = ligneC.Cells[0].AddParagraph().AddImage("base64:" + sig[prefixe.Length..]); img.LockAspectRatio = true; img.Height = "2cm";
        }
        return DocumentsPdf.Rendu(doc);
    }
}
