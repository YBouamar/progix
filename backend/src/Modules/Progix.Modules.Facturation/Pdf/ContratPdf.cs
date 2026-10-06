using MigraDoc.DocumentObjectModel;
using Progix.Modules.Facturation.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Pdf;

/// <summary>Contrat de prestation de post-tension, d'après le modèle de l'entreprise. Les parties, la maîtrise d'œuvre, l'objet et les modalités de paiement viennent du projet et du devis.</summary>
public static class ContratPdf
{
    private static readonly string[] Missions =
    [
        "Calcul des éléments en Post Tension et établissement des plans d'exécution selon l'EN 1992-1-1.",
        "Validation des plans d'exécution PT par le BCT.",
        "Fourniture des Torons de diamètre 15.7mm classe 1860 TBR certifiés ASQPE suivant la norme Européenne prEN 10138-3.",
        "Fourniture des ancrages avec agrément suivant les normes Européenne EN 1992-1-1 et ayant fait l'objet d'un E.T.A (EUROPEAN TECHNICAL ASSESSMENT) suivant EN 1992-1-1 & prEN 10138-3 et conformément à la directive Européenne 98/456/EC pour le marquage CE.",
        "Fourniture des gaines et accessoires spéciaux pour la post-tension, norme SIA 262, et bénéficiant d'un avis technique d'un laboratoire.",
        "Mobilisation d'une ou plusieurs machines de mise en tension et/ou une machine d'injection.",
        "Moyens humains pour la mise en place des torons et ancrages, pour la mise en tension et éventuellement l'injection de coulis de ciment.",
        "Certificat de garantie : fin travaux.",
        "Fourniture des adjuvants pour injection de coulis de ciment.",
    ];

    private static readonly string[] ChargeClient =
    [
        "Magasin sécurisé de dimensions suffisantes (3 m x 2 m au minimum) pour le stockage des ancrages et accessoires de la post-tension.",
        "Manutention des matériaux et équipements de post-tension à pied d'œuvre.",
        "Fourniture et pose de tous les aciers passifs suivant les plans d'exécution fournis par le « Fournisseur » et approuvés par la maîtrise d'œuvre.",
        "Béton conforme aux conditions générales et plans d'exécution.",
        "Échafaudage et coffrages nécessaires avec 1,00 m de plateforme équipée d'un garde-corps de 1 m de hauteur, libre pour la mise en tension.",
        "Recépage et cachetage avec un mortier de ciment.",
        "Électricité pour la tension et l'injection.",
        "Essais d'écrasement sur éprouvettes cylindriques.",
        "Eau, ciment Portland CPJ 45 pour injection de coulis de ciment.",
    ];

    private static readonly Dictionary<string, string> TitresMoe = new()
    {
        ["bet"] = "BUREAU D'ÉTUDES TECHNIQUES (BET)", ["bct"] = "BUREAU DE CONTRÔLE (BCT)", ["architecte"] = "ARCHITECTE", ["laboratoire"] = "LABORATOIRE",
    };

    public static byte[] Generer(Contrat c, ProjetResume p, PartenaireIdentite client, IReadOnlyList<(string Role, PartenaireIdentite Qui)> moe,
        DevisDocument devis, ParametresEntite e, string lienVerification)
    {
        var (doc, s) = DocumentsPdf.Cadre(e, c.Numero);
        void P(string texte, bool gras = false, string avant = "0.15cm", ParagraphAlignment align = ParagraphAlignment.Justify)
        {
            var x = s.AddParagraph(texte); x.Format.Font.Size = 9; x.Format.Font.Bold = gras; x.Format.SpaceBefore = avant; x.Format.Alignment = align;
        }
        void Article(string titre)
        {
            var x = s.AddParagraph(titre); x.Format.Font.Size = 9.5; x.Format.Font.Bold = true; x.Format.Font.Color = DocumentsPdf.Marine; x.Format.SpaceBefore = "0.45cm"; x.Format.KeepWithNext = true;
        }
        void Puces(IEnumerable<string> lignes)
        {
            foreach (var l in lignes)
            {
                var x = s.AddParagraph(); x.Format.Font.Size = 9; x.Format.LeftIndent = "0.8cm"; x.Format.FirstLineIndent = "-0.4cm"; x.Format.SpaceBefore = "0.08cm"; x.Format.Alignment = ParagraphAlignment.Justify;
                x.AddText("•  " + l);
            }
        }

        var titre = s.AddParagraph(c.Annule ? "CONTRAT (ANNULÉ)" : "CONTRAT"); titre.Format.Font.Size = 16; titre.Format.Font.Bold = true; titre.Format.Alignment = ParagraphAlignment.Center; titre.Format.Font.Color = DocumentsPdf.Marine;
        var num = s.AddParagraph($"N° {c.Numero}{(c.AncienNumero is null ? "" : $" · ancien numéro {c.AncienNumero}")}"); num.Format.Alignment = ParagraphAlignment.Center; num.Format.Font.Color = DocumentsPdf.Gris; num.Format.SpaceAfter = "0.3cm";

        P("Entre :", true);
        var siege = string.Join(", ", new[] { e.Siege, e.Ville }.Where(x => !string.IsNullOrWhiteSpace(x)));
        P($"La société {e.RaisonSociale}{(siege.Length > 0 ? $", sise à {siege}" : "")}{(e.Rc is null ? "" : $", RC : {e.Rc}{(e.TribunalRc is null ? "" : $" - {e.TribunalRc}")}")}{(e.Ice is null ? "" : $", ICE : {e.Ice}")}.");
        P("Ci-après dénommée « Le Fournisseur »");
        P("D'une part ;", align: ParagraphAlignment.Right);
        P("Et :", true);
        P(Partie(client));
        P("Ci-après dénommé « Le Client »");
        P("D'autre part.", align: ParagraphAlignment.Right);
        P("Il a été convenu et arrêté ce qui suit.", true, "0.3cm");

        if (moe.Count > 0)
        {
            P("Le « Fournisseur » a pris connaissance que la maîtrise d'œuvre du projet est composée de :");
            foreach (var (role, qui) in moe)
            {
                var x = s.AddParagraph(); x.Format.Font.Size = 9; x.Format.LeftIndent = "0.8cm"; x.Format.SpaceBefore = "0.1cm";
                x.AddFormattedText($"{TitresMoe.GetValueOrDefault(role, role.ToUpperInvariant())} : ", TextFormat.Bold);
                x.AddText($"{qui.Nom}{(string.IsNullOrWhiteSpace(qui.Telephone) ? "" : $", tél. {qui.Telephone}")}");
            }
        }

        Article("ARTICLE 1 : OBJET DU CONTRAT");
        P("Les travaux faisant l'objet du présent contrat s'inscrivent dans le cadre de la réalisation des planchers en post-tension du projet de :");
        P($"« {p.Libelle.ToUpperInvariant()} »", true, "0.2cm", ParagraphAlignment.Center);

        Article("ARTICLE 2 : MISSIONS ET LIMITES DE PRESTATION");
        P("La mission et les limites de prestation confiées au Fournisseur consistent en :");
        Puces(Missions);
        P("Prestations à la charge du client :", true, "0.25cm");
        Puces(ChargeClient);

        Article("ARTICLE 3 : DÉLAIS DE RÉALISATION DES PRESTATIONS");
        P("Le « Fournisseur » définit les dates d'établissement des plans d'exécution, de fourniture et de pose de chaque plancher. Le respect de ces dates est tributaire des éléments suivants :");
        Puces([
            "Validation par « Le Client » des plans de principe (incluant les réservations) par la maîtrise d'œuvre.",
            "Réalisation par « Le Client » de la totalité du coffrage, de la nappe inférieure de l'acier passif, des chaises, des aciers de frettage des ancrages, du coffrage périphérique de la dalle, de la plateforme et de son garde-corps durant l'intervention de l'équipe de pose du « Fournisseur ».",
            "Fourniture par le client des résultats de compression des éprouvettes conformes avant la mise en tension.",
        ]);
        P("Il est à noter que tout changement de plan par la maîtrise d'œuvre et/ou « Le Client » entraînera un changement des délais.");

        Article("ARTICLE 4 : DÉROULEMENT DE LA PRESTATION ET VALIDATION");
        P("L'approbation des plans de principe par la maîtrise d'œuvre est préalable à l'établissement des plans d'exécution par le « Fournisseur ». « Le Client » se chargera exclusivement de cette validation.");
        P("Le « Fournisseur » se chargera de la validation des plans d'exécution par le BCT.");
        P("À la mise en place des coffrages, ferraillages, câbles et ancrages, « Le Client » se charge d'aviser la maîtrise d'œuvre et le « Fournisseur » pour assister à la réception de chaque plancher. Cette réception sera sanctionnée par un PV qui sera signé conjointement par « Le Client », le « Fournisseur » et la maîtrise d'œuvre.");
        P("L'absence d'un des intervenants précités le jour des réceptions se traduit par un accord de la réception du plancher.");

        Article("ARTICLE 5 : PRIX");
        P($"Le prix de ces prestations est défini selon le devis {devis.Numero}, version {devis.Version}, joint en annexe à ce contrat. Ce devis, comprenant plan de principe et conditions de l'offre, doit être signé et cacheté par « Le Client » pour faire effet de bon de commande.");
        P("Le prix de chaque plancher est défini suivant les plans et charges (réservations, charges, etc.) transmis par « Le Client » au « Fournisseur ». Ces éléments sont récapitulés dans les plans de principe.");
        P("Tout changement ou modification des plans de la maîtrise d'œuvre entraînera une modification des prix. Le « Fournisseur » transmettra un nouveau devis, y compris plan de principe, au client pour validation ; il fera partie intégrante de ce contrat.");
        P("En cas d'augmentation des prix de la matière première ou des charges sociales, le « Fournisseur » avisera « Le Client » et lui transmettra un nouveau devis pour approbation, qui fera partie intégrante de ce contrat.");
        if (devis.Echeances.Count > 0)
        {
            P("Les modalités de paiement sont définies d'un commun accord entre « Le Client » et le « Fournisseur » :");
            Puces(devis.Echeances.Select(x => $"{x.Value:0.##} % : {x.Key}."));
        }
        P("La facture de chaque plancher sera transmise au client à la mise en tension pour paiement selon les modalités objet d'un accord préalable.");

        Article("ARTICLE 6 : ARRÊT ET ABANDON");
        P("En cas d'arrêt ou d'abandon du chantier par « Le Client », le « Fournisseur » sera payé pour les planchers exécutés en totalité, et à hauteur de 20 % (vingt pour cent) pour les planchers dont les plans d'exécution ont été établis.");

        Article("ARTICLE 7 : CONTESTATIONS ET LITIGES");
        P("« Le Client » et le « Fournisseur » s'engagent à régler à l'amiable les difficultés auxquelles pourraient donner lieu l'interprétation et l'exécution du présent contrat, quelle que soit la nature du différend. Si le conflit ne trouve pas de résolution complète, il sera porté devant le tribunal de commerce de Casablanca.");

        P($"Fait à {c.Lieu}, en double exemplaire, le {c.DateContrat:dd/MM/yyyy}.", true, "0.5cm");
        if (c.Annule) { var a = s.AddParagraph($"Contrat annulé : {c.MotifAnnulation}"); a.Format.Font.Bold = true; a.Format.Font.Color = new Color(0xA6, 0x1B, 0x1B); a.Format.SpaceBefore = "0.2cm"; }
        DocumentsPdf.Signature(s, c.SignePar ?? "", c.SigneLe ?? c.CreatedAt, lienVerification, "ce contrat", "Signé électroniquement pour le « Fournisseur »");
        // Deux cases pour les signatures manuscrites et les cachets, apposés après impression.
        s.AddParagraph().Format.SpaceAfter = "0.2cm";
        var cases = s.AddTable();
        cases.KeepTogether = true; cases.Borders.Width = 0.5; cases.Borders.Color = new Color(0xCF, 0xD4, 0xE2); cases.LeftPadding = 6; cases.RightPadding = 6;
        cases.AddColumn($"{(DocumentsPdf.Largeur / 2).ToString(System.Globalization.CultureInfo.InvariantCulture)}cm"); cases.AddColumn($"{(DocumentsPdf.Largeur / 2).ToString(System.Globalization.CultureInfo.InvariantCulture)}cm");
        var ligne = cases.AddRow(); ligne.Height = "3.6cm"; ligne.TopPadding = 6;
        foreach (var (i, texte) in new[] { (0, "Pour le « Client » : signature et cachet"), (1, "Pour le « Fournisseur » : signature et cachet") })
        {
            var x = ligne.Cells[i].AddParagraph(texte); x.Format.Font.Size = 8; x.Format.Font.Bold = true; x.Format.Font.Color = DocumentsPdf.Gris;
        }
        return DocumentsPdf.Rendu(doc);
    }

    /// <summary>Désignation du client : société avec son siège et ses identifiants, ou personne avec son état civil.</summary>
    private static string Partie(PartenaireIdentite c)
    {
        var adresse = string.Join(", ", new[] { c.Adresse, c.Ville }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (c.Nature == "morale")
            return $"L'entreprise {c.Nom}{(adresse.Length > 0 ? $", sise {adresse}" : "")}{(c.Rc is null ? "" : $", inscrite au registre du commerce sous le numéro {c.Rc}")}{(c.Ice is null ? "" : $", ICE : {c.Ice}")}.";
        var parts = new List<string> { c.Nom };
        if (!string.IsNullOrWhiteSpace(c.Nationalite)) parts.Add($"de nationalité {c.Nationalite}");
        if (c.DateNaissance is { } n) parts.Add($"né(e) le {n:dd/MM/yyyy}{(string.IsNullOrWhiteSpace(c.LieuNaissance) ? "" : $" à {c.LieuNaissance}")}");
        if (adresse.Length > 0) parts.Add($"demeurant à {adresse}");
        if (!string.IsNullOrWhiteSpace(c.Cin)) parts.Add($"titulaire de la CIN n° {c.Cin}");
        return string.Join(", ", parts) + ".";
    }
}
