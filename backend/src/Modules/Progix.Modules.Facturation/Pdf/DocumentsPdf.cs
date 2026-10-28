using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Progix.Modules.Facturation.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Pdf;

/// <summary>Mise en page des factures et des avoirs, avec les mentions légales marocaines. Bibliothèque PDFsharp et MigraDoc, licence MIT.</summary>
public static class DocumentsPdf
{
    private static readonly CultureInfo Fr = new("fr-FR");
    internal static readonly Color Marine = new(0x1B, 0x2A, 0x6B);
    internal static readonly Color Gris = new(0x5B, 0x61, 0x78);
    private static readonly Color Trait = new(0xCF, 0xD4, 0xE2);
    private static readonly Color Fond = new(0xF4, 0xF5, 0xF9);
    private static readonly Color Blanc = new(0xFF, 0xFF, 0xFF);
    private static readonly Color Rouge = new(0xA6, 0x1B, 0x1B);
    internal const double Largeur = 17.4; // A4 moins deux marges de 1,8 cm

    public const string MentionRetard = "En cas de retard de paiement, des pénalités sont exigibles de plein droit au taux légal en vigueur, conformément à l'article 78-2 du Code de Commerce.";
    public const string MentionExoneration = "Facture établie en exonération de la TVA en vertu de l'article 94 du Code Général des Impôts (CGI).";

    internal static string DH(decimal v) => Nb(v) + " DH";
    private static string Nb(decimal v) => v.ToString("N2", Fr).Replace(' ', ' ').Replace(' ', ' ');
    internal static string Date(DateOnly d) => d.ToString("dd/MM/yyyy");

    public static byte[] Facture(Facture f, ParametresEntite e, IReadOnlyList<DateOnly> livraisons, string lienVerification)
    {
        var acompte = f.Type == TypesFacture.Acompte;
        var (doc, s) = Cadre(e, f.Numero);

        var refs = new List<(string, string)> { ("Numéro", f.Numero), ("Date d'émission", Date(f.DateFacture)) };
        if (livraisons.Count > 0 && livraisons.Any(d => d != f.DateFacture)) refs.Add(("Date de livraison", string.Join(", ", livraisons.Distinct().OrderBy(d => d).Select(Date))));
        refs.Add(("Projet", $"{f.ProjetCode} · {f.ProjetLibelle}"));
        if (!acompte) refs.Add(("Bon(s) de livraison", f.Origine));
        if (f.AncienNumero is not null) refs.Add(("Ancien numéro", f.AncienNumero));
        Entete(s, f.ClientNom, f.ClientAdresse, f.ClientIdentifiants, acompte ? "FACTURE D'ACOMPTE" : "FACTURE", refs);

        var t = Tableau(s, [(3.6, "Réf.", false), (6.0, "Désignation", false), (1.9, "Qté (m²)", true), (1.9, "P.U. HT", true), (2.6, "Total HT", true), (1.4, "TVA", true)]);
        var n = 0;
        foreach (var l in f.Lignes.OrderBy(x => x.Ordre))
        {
            // Les lignes de dalle portent « référence · emplacement ».
            var i = l.DalleId is null ? -1 : l.Libelle.IndexOf(" · ", StringComparison.Ordinal);
            LigneTableau(t,
                i > 0 ? l.Libelle[..i] : (++n).ToString("000"),
                i > 0 ? $"Dalle en béton précontraint par post-tension, {l.Libelle[(i + 3)..]}" : l.Libelle,
                l.Surface is null ? "1" : Nb(l.Surface.Value), Nb(l.PrixM2 ?? l.MontantHT), Nb(l.MontantHT), $"{f.TauxTva:0.##} %");
        }

        var totaux = new List<(string, string)>();
        if (f.AcompteDeduit > 0) { totaux.Add(("Total HT des prestations", DH(f.TotalHT))); totaux.Add(("Acompte déjà facturé, déduit", "- " + DH(f.AcompteDeduit))); }
        totaux.Add(("Total général hors taxes (HT)", DH(f.NetHT)));
        totaux.Add(($"Montant de la TVA ({f.TauxTva:0.##} %)", DH(f.Tva)));
        Totaux(s, totaux, "Total toutes taxes comprises (TTC)", DH(f.TotalTTC));
        EnToutesLettres(s, "Arrêtée la présente facture à la somme de : ", f.TotalTTC);

        var conditions = new List<(string, string)>
        {
            ("Date limite de paiement", Date(f.Echeance)),
            ("Mode de règlement", f.ModeReglement switch { ModesPaiement.Cheque => "Chèque", ModesPaiement.Effet => "Effet", ModesPaiement.Especes => "Espèces", _ => "Virement bancaire" }),
        };
        if (e.Rib is not null) conditions.Add(("Coordonnées bancaires (RIB)", $"{Rib(e.Rib)}{(e.Banque is null ? "" : $" · {e.Banque}")}"));
        var bloc = Bloc(s.AddTable(), Largeur, "CONDITIONS ET MODALITÉS DE PAIEMENT");
        foreach (var (label, valeur) in conditions) Mention(bloc.AddParagraph(), label, valeur);

        var m = s.AddParagraph(f.TauxTva == 0 ? $"{MentionExoneration} {MentionRetard}" : MentionRetard);
        m.Format.SpaceBefore = "0.4cm"; m.Format.Font.Size = 7.5; m.Format.Font.Italic = true; m.Format.Font.Color = Gris;
        Signature(s, f.SignePar ?? "", f.SigneLe ?? f.CreatedAt, lienVerification, "cette facture");
        return Rendu(doc);
    }

    public static byte[] Avoir(Avoir a, Facture f, ParametresEntite e, string lienVerification)
    {
        var (doc, s) = Cadre(e, a.Numero);
        var refs = new List<(string, string)>
        {
            ("Numéro", a.Numero), ("Date d'émission", Date(a.DateAvoir)), ("Sur la facture", $"{f.Numero} du {Date(f.DateFacture)}"), ("Projet", $"{f.ProjetCode} · {f.ProjetLibelle}"),
        };
        if (a.AncienNumero is not null) refs.Add(("Ancien numéro", a.AncienNumero));
        Entete(s, f.ClientNom, f.ClientAdresse, f.ClientIdentifiants, a.Annule ? "AVOIR (ANNULÉ)" : "AVOIR", refs);

        var t = Tableau(s, [(13.4, "Désignation", false), (2.6, "Total HT", true), (1.4, "TVA", true)]);
        LigneTableau(t, $"{(a.Total ? "Avoir total" : "Avoir partiel")} sur la facture {f.Numero}. Motif : {a.Motif}", Nb(a.MontantHT), $"{a.TauxTva:0.##} %");
        Totaux(s, [("Total hors taxes (HT)", DH(a.MontantHT)), ($"Montant de la TVA ({a.TauxTva:0.##} %)", DH(a.Tva))], "Total de l'avoir (TTC)", DH(a.TotalTTC));
        EnToutesLettres(s, "Arrêté le présent avoir à la somme de : ", a.TotalTTC);
        if (a.Annule)
        {
            var p = s.AddParagraph($"Avoir annulé : {a.MotifAnnulation}");
            p.Format.SpaceBefore = "0.4cm"; p.Format.Font.Bold = true; p.Format.Font.Color = Rouge;
        }
        Signature(s, a.SignePar ?? "", a.SigneLe ?? a.CreatedAt, lienVerification, "cet avoir");
        return Rendu(doc);
    }

    public static byte[] Devis(DevisDocument d, ProjetResume p, PartenaireLegal client, ParametresEntite e, string lienVerification)
    {
        var (doc, s) = Cadre(e, d.Numero);
        var refs = new List<(string, string)> { ("Numéro", $"{d.Numero} · version {d.Version}"), ("Date du devis", Date(d.DateDevis)), ("Valable jusqu'au", Date(d.Echeance)), ("Projet", $"{p.Code} · {p.Libelle}") };
        if (d.Commercial is not null) refs.Add(("Votre interlocuteur", d.Commercial));
        // Brouillon : document de travail imprimé avant l'envoi, sans signature ni QR code de vérification.
        var brouillon = d.Etat == "brouillon";
        var titre = d.Etat switch { "brouillon" => "DEVIS (BROUILLON)", "annule" => "DEVIS (REJETÉ)", "remplace" => "DEVIS (REMPLACÉ)", "expire" => "DEVIS (EXPIRÉ)", _ => "DEVIS" };
        Entete(s, client.Nom, client.Adresse, client.Identifiants, titre, refs);
        if (brouillon)
        {
            var avis = s.AddParagraph("BROUILLON : document de travail non signé, sans valeur contractuelle. Les montants peuvent encore changer avant l'envoi du devis.");
            avis.Format.Font.Bold = true; avis.Format.Font.Size = 9; avis.Format.Font.Color = new Color(0xB4, 0x23, 0x18);
            avis.Format.Shading.Color = new Color(0xFD, 0xEC, 0xEA); avis.Format.Borders.Distance = 4; avis.Format.SpaceAfter = "0.3cm";
        }

        var objet = s.AddParagraph("Objet : dalles en béton précontraint par post-tension, étude et réalisation.");
        objet.Format.SpaceAfter = "0.3cm"; objet.Format.Font.Bold = true;

        // Les ratios d'acier (actif, passif) servent au chiffrage interne : ils ne figurent pas sur le devis remis au client.
        var t = Tableau(s, [(4.0, "Réf.", false), (7.4, "Désignation", false), (1.8, "Surface (m²)", true), (1.6, "P.U. HT", true), (2.6, "Total HT", true)]);
        foreach (var l in d.Lignes)
            LigneTableau(t, l.Reference, l.Emplacement, Nb(l.Surface), Nb(l.PrixM2), Nb(l.MontantHT));

        var totaux = new List<(string, string)> { ("Surface totale", Nb(d.SurfaceTotale) + " m²") };
        if (d.RemiseTotale > 0) { totaux.Add(("Total HT avant remise", DH(d.TotalHT))); totaux.Add(($"Remise de {Nb(d.RemiseM2)} DH par m²", "- " + DH(d.RemiseTotale))); }
        totaux.Add(("Total hors taxes (HT)", DH(d.NetHT)));
        totaux.Add((d.ExonereTva ? "TVA (0 %, client exonéré)" : "Montant de la TVA (20 %)", DH(d.Tva)));
        Totaux(s, totaux, "Total toutes taxes comprises (TTC)", DH(d.TotalTTC));
        EnToutesLettres(s, "Arrêté le présent devis à la somme de : ", d.TotalTTC);

        if (d.Echeances.Count > 0)
        {
            var bloc = Bloc(s.AddTable(), Largeur, "MODALITÉS DE PAIEMENT");
            foreach (var x in d.Echeances) Mention(bloc.AddParagraph(), x.Key, $"{x.Value:0.##} %, soit {DH(Math.Round(d.TotalTTC * x.Value / 100m, 2))} TTC");
        }
        if (!brouillon) Signature(s, d.SignePar ?? "", d.SigneLe ?? DateTime.UtcNow, lienVerification, "ce devis", "Bon pour accord : date, cachet et signature du client");

        // Conditions d'offre : elles font partie du devis et sont signées avec lui.
        s.AddPageBreak();
        var titreC = s.AddParagraph("CONDITIONS D'OFFRE"); titreC.Format.Font.Size = 14; titreC.Format.Font.Bold = true; titreC.Format.Font.Color = Marine; titreC.Format.Alignment = ParagraphAlignment.Center;
        var refC = s.AddParagraph($"Devis {d.Numero}, version {d.Version}, du {Date(d.DateDevis)}"); refC.Format.Alignment = ParagraphAlignment.Center; refC.Format.Font.Color = Gris; refC.Format.SpaceAfter = "0.4cm";
        void Rubrique(string texte) { var x = s.AddParagraph(texte); x.Format.Font.Bold = true; x.Format.Font.Size = 9.5; x.Format.Font.Color = Marine; x.Format.SpaceBefore = "0.4cm"; x.Format.SpaceAfter = "0.1cm"; x.Format.KeepWithNext = true; }
        void Puce(string texte) { var x = s.AddParagraph("•  " + texte); x.Format.Font.Size = 9; x.Format.LeftIndent = "0.8cm"; x.Format.FirstLineIndent = "-0.4cm"; x.Format.SpaceBefore = "0.08cm"; x.Format.Alignment = ParagraphAlignment.Justify; }

        Rubrique("Conditions générales :");
        Puce("Charges d'exploitation et permanentes : suivant le plan de charges transmis lors de la consultation.");
        if (d.Echeances.Count > 0) Puce("Modalités de paiement : " + string.Join(" ; ", d.Echeances.Select(x => $"{x.Value:0.##} % {char.ToLower(x.Key[0])}{x.Key[1..]}")) + ".");
        Puce($"Validité de l'offre : {d.Echeance.DayNumber - d.DateDevis.DayNumber} jours, soit jusqu'au {Date(d.Echeance)}.");
        foreach (var ligne in (d.Modalites ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) Puce(ligne);
        Puce("Quantités : mesurées selon le plan ; seules les ouvertures entourées par des supports (voiles ou poutres) seront déduites.");
        Puce("Modification du prix : ces prix peuvent être modifiés en fonction des cours de la matière première.");

        Rubrique("Prestations à notre charge :");
        foreach (var x in new[]
        {
            "Calcul des éléments en post-tension et établissement des plans d'exécution selon l'EC2.",
            "Validation des plans d'exécution PT par le BCT.",
            "Fourniture des torons de diamètre 15,7 mm, classe 1860 TBR, certifiés ASQPE suivant la norme européenne.",
            "Fourniture des ancrages avec agrément suivant les normes européennes.",
            "Fourniture des gaines et accessoires spéciaux pour la post-tension.",
            "Mobilisation d'une ou plusieurs machines de mise en tension et/ou d'une machine d'injection.",
            "Moyens humains pour la mise en place des torons et ancrages, pour la mise en tension et éventuellement l'injection de coulis.",
            "Adjuvants pour injection de coulis de ciment.",
            "Certificat de garantie : fin de travaux.",
        }) Puce(x);

        Rubrique("Prestations à la charge du client :");
        foreach (var x in new[]
        {
            "Manutention des matériaux et équipements de post-tension à pied d'œuvre.",
            "Fourniture et pose de tous les aciers passifs.",
            "Béton conforme aux conditions générales et plans d'exécution.",
            "Eau, ciment Portland CPJ 45.",
            "Échafaudage et coffrages nécessaires avec 1,00 m de plateforme libre pour la mise en tension.",
            "Recépage et cachetage avec un mortier de ciment.",
            "Électricité pour la tension et l'injection.",
            "Essais d'écrasement sur éprouvettes cylindriques.",
        }) Puce(x);
        return Rendu(doc);
    }

    public static byte[] Livraison(BonLivraison b, ProjetResume p, PartenaireLegal client, ParametresEntite e, string lienVerification)
    {
        var (doc, s) = Cadre(e, b.Numero);
        var refs = new List<(string, string)> { ("Numéro", b.Numero), ("Date de livraison", Date(b.DateLivraison)), ("Projet", $"{p.Code} · {p.Libelle}") };
        if (b.AncienNumero is not null) refs.Add(("Ancien numéro", b.AncienNumero));
        Entete(s, client.Nom, client.Adresse, client.Identifiants, "BON DE LIVRAISON", refs);

        var t = Tableau(s, [(4.2, "Réf.", false), (10.4, "Dalle livrée", false), (2.8, "Surface (m²)", true)]);
        foreach (var l in b.Lignes.OrderBy(x => x.Ordre))
            LigneTableau(t, l.Reference, $"Dalle en béton précontraint par post-tension, {l.Emplacement}", Nb(l.Surface));
        Totaux(s, [("Nombre de dalles", b.Lignes.Count.ToString())], "Surface totale livrée", Nb(b.Surface) + " m²");

        Signature(s, b.SignePar ?? "", b.SigneLe ?? b.CreatedAt, lienVerification, "ce bon de livraison", "Reçu par le client : nom, date, cachet et signature");
        return Rendu(doc);
    }

    /// <param name="facture">Facture réglée, pour un règlement saisi avant le suivi par dalle ; sinon null.</param>
    public static byte[] Recu(Encaissement x, Facture? facture, ParametresEntite e, string lienVerification)
    {
        var (doc, s) = Cadre(e, x.NumeroRecu!);
        var refs = new List<(string, string)> { ("Numéro", x.NumeroRecu!), (x.Remboursement ? "Date du remboursement" : "Date du paiement", Date(x.DatePaiement)), ("Projet", $"{x.ProjetCode} · {x.ProjetLibelle}") };
        if (facture is not null) refs.Add((x.Remboursement ? "Trop-perçu sur la facture" : "En règlement de la facture", $"{facture.Numero} du {Date(facture.DateFacture)}"));
        if (x.AncienNumeroRecu is not null) refs.Add(("Ancien numéro", x.AncienNumeroRecu));
        Entete(s, x.ClientNom, x.ClientAdresse, x.ClientIdentifiants, (x.Remboursement ? "REÇU DE REMBOURSEMENT" : "REÇU DE PAIEMENT") + (x.Annule ? " (ANNULÉ)" : ""), refs);

        var detail = new List<(string, string)> { (x.Remboursement ? "Mode de remboursement" : "Mode de paiement", ModesPaiement.Libelles.GetValueOrDefault(x.Mode, x.Mode)) };
        if (x.Reference is not null) detail.Add((x.Mode == ModesPaiement.Cheque ? "Numéro du chèque" : x.Mode == ModesPaiement.Effet ? "Numéro de l'effet" : "Référence", x.Reference));
        if (x.Banque is not null) detail.Add(("Banque", x.Banque));
        if (x.EcheanceEffet is not null) detail.Add(("Échéance de l'effet", $"{Date(x.EcheanceEffet.Value)}, date à laquelle il est encaissé"));
        var bloc = Bloc(s.AddTable(), Largeur, x.Remboursement ? "DÉTAIL DU REMBOURSEMENT" : "DÉTAIL DU RÈGLEMENT");
        foreach (var (label, valeur) in detail) Mention(bloc.AddParagraph(), label, valeur);

        // Répartition entre les dalles du projet.
        if (x.Dalles.Count > 0)
        {
            s.AddParagraph().Format.SpaceAfter = "0.2cm";
            var t = s.AddTable();
            t.Borders.Width = 0.5; t.Borders.Color = Trait; t.LeftPadding = 5; t.RightPadding = 5;
            t.AddColumn($"{(Largeur - 4.2).ToString(CultureInfo.InvariantCulture)}cm"); t.AddColumn("4.2cm");
            var h = t.AddRow(); h.HeadingFormat = true; h.Shading.Color = Fond; h.Format.Font.Bold = true; h.Format.Font.Size = 7.5;
            h.Cells[0].AddParagraph(x.Remboursement ? "DALLE CONCERNÉE" : "DALLE"); h.Cells[1].AddParagraph("MONTANT AFFECTÉ").Format.Alignment = ParagraphAlignment.Right;
            foreach (var d in x.Dalles)
            {
                var r = t.AddRow();
                r.Cells[0].AddParagraph(d.Reference);
                r.Cells[1].AddParagraph(DH(d.Montant)).Format.Alignment = ParagraphAlignment.Right;
            }
        }

        Totaux(s, [], x.Remboursement ? "Montant remboursé au client" : "Montant reçu", DH(x.Montant));
        EnToutesLettres(s, x.Remboursement ? "Remboursé au client la somme de : " : "Reçu la somme de : ", x.Montant);
        if (x.Annule)
        {
            var p = s.AddParagraph($"{(x.Remboursement ? "Remboursement" : "Règlement")} annulé : {x.MotifAnnulation}");
            p.Format.SpaceAfter = "0.4cm"; p.Format.Font.Bold = true; p.Format.Font.Color = Rouge;
        }

        Signature(s, x.SaisiPar, x.SigneLe ?? x.CreatedAt, lienVerification, "ce reçu", x.Remboursement ? "Cachet de la société, et signature du client pour acquit" : "Cachet de la société");
        return Rendu(doc);
    }

    /// <summary>Signature électronique, QR code de vérification, et place du cachet à apposer après impression.</summary>
    internal static void Signature(Section s, string par, DateTime le, string lien, string quoi, string cachetLabel = "Cachet de la société")
    {
        s.AddParagraph().Format.SpaceAfter = "0.3cm";
        var t = s.AddTable();
        t.KeepTogether = true;
        t.Borders.Width = 0.5; t.Borders.Color = Trait; t.LeftPadding = 6; t.RightPadding = 6;
        t.AddColumn("3.2cm"); t.AddColumn($"{(Largeur - 3.2 - 5.4).ToString(CultureInfo.InvariantCulture)}cm"); t.AddColumn("5.4cm");
        var r = t.AddRow(); r.Height = "3.4cm"; r.TopPadding = 6; r.BottomPadding = 6;
        using (var gen = new QRCoder.QRCodeGenerator())
        using (var data = gen.CreateQrCode(lien, QRCoder.QRCodeGenerator.ECCLevel.M))
        {
            var img = r.Cells[0].AddImage("base64:" + Convert.ToBase64String(new QRCoder.BitmapByteQRCode(data).GetGraphic(8)));
            img.Width = "2.8cm"; img.LockAspectRatio = true;
        }
        var c = r.Cells[1]; c.VerticalAlignment = VerticalAlignment.Center;
        var titre = c.AddParagraph("SIGNATURE ÉLECTRONIQUE"); titre.Format.Font.Size = 7.5; titre.Format.Font.Bold = true; titre.Format.Font.Color = Marine; titre.Format.SpaceAfter = 3;
        var signe = c.AddParagraph(); signe.AddText("Signé électroniquement par "); signe.AddFormattedText(par, TextFormat.Bold);
        signe.AddText(" le "); signe.AddFormattedText(Maroc(le).ToString("dd/MM/yyyy 'à' HH:mm"), TextFormat.Bold); signe.AddText(".");
        var aide = c.AddParagraph($"Scannez le QR code pour vérifier l'authenticité de {quoi}."); aide.Format.Font.Size = 7.5; aide.Format.Font.Color = Gris; aide.Format.SpaceBefore = 3;
        var url = c.AddParagraph(lien); url.Format.Font.Size = 6.5; url.Format.Font.Color = Gris;
        var cachet = r.Cells[2].AddParagraph(cachetLabel); cachet.Format.Font.Size = 7.5; cachet.Format.Font.Color = Gris;
    }

    /// <summary>Heure légale du Maroc : UTC+1 (le passage à UTC pendant le ramadan n'est pas géré).</summary>
    private static DateTime Maroc(DateTime utc) => utc.AddHours(1);

    /// <summary>Page A4, en-tête (identité de l'entité, logo s'il est paramétré) et pied de page (identifiants légaux).</summary>
    internal static (Document, Section) Cadre(ParametresEntite e, string titre)
    {
        Polices.Installer();
        var doc = new Document { Info = { Title = titre, Author = e.RaisonSociale } };
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = Polices.Famille; normal.Font.Size = 8.5; normal.Font.Color = new Color(0x1A, 0x1D, 0x2E);

        var s = doc.AddSection();
        s.PageSetup = doc.DefaultPageSetup.Clone();
        s.PageSetup.PageFormat = PageFormat.A4;
        s.PageSetup.LeftMargin = s.PageSetup.RightMargin = "1.8cm";
        s.PageSetup.TopMargin = "5.1cm"; s.PageSetup.HeaderDistance = "1.1cm";
        s.PageSetup.BottomMargin = "2cm"; s.PageSetup.FooterDistance = "0.9cm";

        var h = s.Headers.Primary.AddTable();
        h.Borders.Visible = false;
        h.AddColumn("3.8cm"); h.AddColumn($"{Largeur - 3.8}cm");
        var r = h.AddRow(); r.Height = "2.1cm"; r.VerticalAlignment = VerticalAlignment.Center;
        // La place du logo reste réservée ; il ne s'affiche qu'une fois déposé dans le paramétrage.
        if (e.Logo is not null)
        {
            var img = r.Cells[0].AddImage("base64:" + Convert.ToBase64String(e.Logo));
            img.LockAspectRatio = true; img.Height = "1.9cm";
        }
        var c = r.Cells[1];
        var nom = c.AddParagraph(e.RaisonSociale); nom.Format.Font.Size = 16; nom.Format.Font.Bold = true; nom.Format.Font.Color = Marine;
        foreach (var ligne in new[]
        {
            string.Join(" · ", new[] { e.FormeJuridique, e.Capital is null ? null : $"Capital social : {DH(e.Capital.Value)}" }.Where(x => x is not null)),
            string.Join(", ", new[] { e.Siege, e.Ville }.Where(x => x is not null)) is { Length: > 0 } siege ? $"Siège social : {siege}" : "",
            string.Join(" · ", new[] { e.Telephone is null ? null : $"Tél. {e.Telephone}", e.Email }.Where(x => x is not null)),
        }.Where(x => x.Length > 0))
        {
            var p = c.AddParagraph(ligne); p.Format.Font.Color = Gris;
        }
        var ids = s.Headers.Primary.AddParagraph();
        ids.Format.SpaceBefore = "0.25cm"; ids.Format.Borders.Top.Width = 1.2; ids.Format.Borders.Top.Color = Marine; ids.Format.Borders.DistanceFromTop = "0.15cm"; ids.Format.Font.Size = 7.5;
        var premier = true;
        foreach (var (label, valeur) in new[] { ("ICE", e.Ice), ("I.F.", e.IdentifiantFiscal), ("R.C.", e.Rc is null ? null : $"{e.Rc}{(e.TribunalRc is null ? "" : $" - Tribunal de {e.TribunalRc}")}"), ("T.P.", e.Patente), ("C.N.S.S.", e.Cnss) })
        {
            if (valeur is null) continue;
            if (!premier) ids.AddText("    ·    ");
            ids.AddFormattedText($"{label} : ", new Font { Color = Gris }); ids.AddFormattedText(valeur, TextFormat.Bold);
            premier = false;
        }

        var pied = s.Footers.Primary.AddParagraph();
        pied.Format.Font.Size = 7; pied.Format.Font.Color = Gris; pied.Format.Borders.Top.Width = 0.5; pied.Format.Borders.Top.Color = Trait; pied.Format.Borders.DistanceFromTop = "0.15cm";
        pied.Format.TabStops.AddTabStop($"{Largeur}cm", TabAlignment.Right);
        pied.AddText($"{e.RaisonSociale}{(e.FormeJuridique is null ? "" : $" {e.FormeJuridique}")}{(e.Ice is null ? "" : $" · ICE {e.Ice}")}{(e.Rc is null ? "" : $" · RC {e.Rc}")}{(e.IdentifiantFiscal is null ? "" : $" · IF {e.IdentifiantFiscal}")}");
        pied.AddTab(); pied.AddText("Page "); pied.AddPageField(); pied.AddText(" sur "); pied.AddNumPagesField();
        return (doc, s);
    }

    /// <summary>Client à gauche, références du document à droite.</summary>
    private static void Entete(Section s, string client, string? adresse, string? identifiants, string titre, List<(string, string)> refs)
    {
        var t = s.AddTable();
        t.Borders.Visible = false;
        const double moitie = (Largeur - 0.5) / 2;
        t.AddColumn($"{moitie}cm"); t.AddColumn("0.5cm"); t.AddColumn($"{moitie}cm");
        var r = t.AddRow();
        var g = Bloc(r.Cells[0].Elements.AddTable(), moitie, "CLIENT");
        var nom = g.AddParagraph(client); nom.Format.Font.Bold = true; nom.Format.Font.Size = 10;
        if (adresse is not null) g.AddParagraph(adresse);
        if (identifiants is not null) { var p = g.AddParagraph(identifiants); p.Format.Font.Color = Gris; }
        var d = Bloc(r.Cells[2].Elements.AddTable(), moitie, titre);
        foreach (var (label, valeur) in refs) Mention(d.AddParagraph(), label, valeur);
        s.AddParagraph().Format.SpaceAfter = "0.2cm";
    }

    /// <summary>Encadré à bandeau de titre ; renvoie la cellule de contenu.</summary>
    internal static Cell Bloc(Table t, double largeurCm, string titre)
    {
        t.Borders.Width = 0.5; t.Borders.Color = Trait;
        t.AddColumn($"{largeurCm}cm");
        var haut = t.AddRow(); haut.Shading.Color = Fond; haut.TopPadding = 3; haut.BottomPadding = 3;
        var p = haut.Cells[0].AddParagraph(titre); p.Format.Font.Size = 7.5; p.Format.Font.Bold = true; p.Format.Font.Color = Marine;
        var corps = t.AddRow(); corps.TopPadding = 5; corps.BottomPadding = 5;
        t.LeftPadding = 6; t.RightPadding = 6;
        return corps.Cells[0];
    }

    internal static void Mention(Paragraph p, string label, string valeur)
    {
        p.Format.SpaceAfter = 1.5;
        p.AddFormattedText($"{label} : ", new Font { Color = Gris }); p.AddFormattedText(valeur, TextFormat.Bold);
    }

    private static Table Tableau(Section s, (double Largeur, string Titre, bool Droite)[] colonnes)
    {
        var t = s.AddTable();
        t.Borders.Visible = false; t.LeftPadding = 4; t.RightPadding = 4;
        foreach (var c in colonnes) t.AddColumn($"{c.Largeur.ToString(CultureInfo.InvariantCulture)}cm").Format.Alignment = c.Droite ? ParagraphAlignment.Right : ParagraphAlignment.Left;
        var h = t.AddRow(); h.HeadingFormat = true; h.Shading.Color = Marine; h.TopPadding = 4; h.BottomPadding = 4;
        for (var i = 0; i < colonnes.Length; i++)
        {
            var p = h.Cells[i].AddParagraph(colonnes[i].Titre); p.Format.Font.Color = Blanc; p.Format.Font.Bold = true; p.Format.Font.Size = 7.5;
        }
        return t;
    }

    private static void LigneTableau(Table t, params string[] cellules)
    {
        var r = t.AddRow(); r.TopPadding = 4; r.BottomPadding = 4; r.Borders.Bottom.Width = 0.5; r.Borders.Bottom.Color = Trait;
        for (var i = 0; i < cellules.Length; i++) r.Cells[i].AddParagraph(cellules[i]);
    }

    private static void Totaux(Section s, List<(string, string)> lignes, string titreTotal, string total)
    {
        s.AddParagraph().Format.SpaceAfter = "0.1cm";
        var t = s.AddTable();
        t.KeepTogether = true;
        t.Borders.Visible = false; t.LeftPadding = 5; t.RightPadding = 5;
        t.Rows.LeftIndent = $"{(Largeur - 9.6).ToString(CultureInfo.InvariantCulture)}cm";
        t.AddColumn("6.4cm"); t.AddColumn("3.2cm").Format.Alignment = ParagraphAlignment.Right;
        foreach (var (label, valeur) in lignes)
        {
            var r = t.AddRow(); r.TopPadding = 3.5; r.BottomPadding = 3.5; r.Borders.Bottom.Width = 0.5; r.Borders.Bottom.Color = Trait;
            r.Cells[0].AddParagraph(label).Format.Font.Color = Gris;
            r.Cells[1].AddParagraph(valeur).Format.Font.Bold = true;
        }
        var f = t.AddRow(); f.Shading.Color = Marine; f.TopPadding = 5; f.BottomPadding = 5;
        var a = f.Cells[0].AddParagraph(titreTotal); a.Format.Font.Color = Blanc; a.Format.Font.Bold = true;
        var b = f.Cells[1].AddParagraph(total); b.Format.Font.Color = Blanc; b.Format.Font.Bold = true;
    }

    private static void EnToutesLettres(Section s, string amorce, decimal montant)
    {
        var p = s.AddParagraph();
        p.Format.SpaceBefore = "0.4cm"; p.Format.SpaceAfter = "0.4cm";
        p.AddFormattedText(amorce, TextFormat.Italic);
        p.AddFormattedText(EnLettres.Montant(montant) + ".", TextFormat.Bold | TextFormat.Italic);
    }

    internal static byte[] Rendu(Document doc)
    {
        var rendu = new PdfDocumentRenderer { Document = doc };
        rendu.RenderDocument();
        using var ms = new MemoryStream();
        rendu.PdfDocument.Save(ms);
        return ms.ToArray();
    }

    /// <summary>RIB marocain groupé : banque, ville, compte, clé.</summary>
    private static string Rib(string rib) => rib.Length == 24 ? $"{rib[..3]} {rib[3..6]} {rib[6..22]} {rib[22..]}" : rib;
}
