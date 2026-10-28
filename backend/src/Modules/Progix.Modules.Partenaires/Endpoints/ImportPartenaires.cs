using System.Globalization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Partenaires.Application;
using Progix.Modules.Partenaires.Domain;
using Progix.Modules.Partenaires.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Partenaires.Endpoints;

/// <summary>
/// Import des clients et partenaires depuis un classeur Excel, suivant la trame téléchargeable.
/// Une première lecture renvoie l'aperçu ligne par ligne sans rien enregistrer ; l'enregistrement crée les lignes valides.
/// Mêmes règles que la fiche : ICE ou CIN déjà connus bloquent la ligne, un même nom ou un même téléphone est signalé.
/// </summary>
public static class ImportPartenaires
{
    public const int Max = 2000;
    private const int TailleMax = 5 * 1024 * 1024;

    /// <summary>Colonnes de la trame, dans l'ordre. Les astérisques marquent les colonnes obligatoires.</summary>
    public static readonly string[] Colonnes =
    [
        "Type*", "Catégorie*", "Raison sociale ou nom*", "Prénom", "Civilité", "ICE", "RC", "Identifiant fiscal", "Patente", "CIN",
        "Adresse", "Ville", "Téléphone", "Email", "Exonéré de TVA", "Contact", "Fonction du contact", "Téléphone du contact", "Email du contact",
    ];

    /// <summary>Colonnes saisies comme du texte dans la trame : un numéro garde ses zéros en tête.</summary>
    private static readonly string[] Textes = ["ICE", "RC", "Identifiant fiscal", "Patente", "CIN", "Téléphone", "Téléphone du contact"];

    public record LigneImport(int Ligne, string Nom, string? Type, string? Categorie, string Statut, string Message);

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/partenaires/import").WithTags("Clients et partenaires").RequireAuthorization(Policies.Commercial);

        g.MapGet("/trame.xlsx", () => Results.File(Trame(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "trame-clients-partenaires.xlsx"));

        // enregistrer=false : aperçu seul. enregistrer=true : création des lignes valides, les autres sont ignorées.
        g.MapPost("/", async (IFormFile fichier, bool? enregistrer, PartenairesDbContext db) =>
        {
            if (fichier.Length == 0) return Conflit("Le fichier est vide.");
            if (fichier.Length > TailleMax) return Conflit("Le fichier dépasse 5 Mo.");
            if (!fichier.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) return Conflit("Déposez un classeur Excel (.xlsx) suivant la trame.");
            List<Dictionary<string, string?>> lignes;
            try
            {
                using var flux = new MemoryStream();
                await fichier.CopyToAsync(flux);
                lignes = Lire(flux.ToArray(), out var manquantes);
                if (manquantes.Count > 0) return Conflit($"Colonnes absentes du fichier : {string.Join(", ", manquantes)}. Téléchargez la trame pour repartir d'un modèle juste.");
            }
            catch (Exception) { return Conflit("Le fichier n'a pas pu être lu : vérifiez qu'il s'agit d'un classeur Excel suivant la trame."); }
            if (lignes.Count == 0) return Conflit("Le fichier ne contient aucune ligne à importer.");
            if (lignes.Count > Max) return Conflit($"Le fichier contient {lignes.Count} lignes : {Max} au plus par import.");

            var existants = await db.Partenaires.AsNoTracking().Select(p => new { p.Ice, p.Cin, p.RaisonSociale, p.Nom, p.Prenom, p.Civilite, p.Nature, p.Telephone }).ToListAsync();
            var ices = existants.Where(p => p.Ice != null).Select(p => p.Ice!).ToHashSet();
            var cins = existants.Where(p => p.Cin != null).Select(p => p.Cin!).ToHashSet();
            var noms = existants.Select(p => PartenairesEndpoints.NomNormalise(p.Nature != Natures.Physique ? p.RaisonSociale : $"{p.Civilite} {p.Prenom} {p.Nom}"))
                .Where(n => n.Length >= 3).ToHashSet();
            var telephones = existants.Select(p => PartenairesEndpoints.Chiffres(p.Telephone)).Where(t => t.Length >= 9).ToHashSet();

            var resultat = new List<LigneImport>();
            var aCreer = new List<Partenaire>();
            for (var i = 0; i < lignes.Count; i++)
            {
                var l = lignes[i];
                var numero = i + 2; // ligne Excel : la ligne 1 porte les titres
                var nomBrut = (l["Raison sociale ou nom*"] ?? "").Trim();
                var (input, refus) = VersInput(l);
                if (refus is not null || input is null)
                {
                    resultat.Add(new LigneImport(numero, nomBrut, l["Type*"], l["Catégorie*"], "erreur", refus ?? "Ligne illisible."));
                    continue;
                }
                var erreurs = PartenairesEndpoints.Valider(input);
                var nom = input.Nature == Natures.Physique ? $"{input.Prenom} {input.NomFamille}".Trim() : nomBrut;
                if (erreurs.Count > 0)
                {
                    resultat.Add(new LigneImport(numero, nom, TypesPartenaire.Labels[input.Type], input.Nature, "erreur", string.Join(" ", erreurs.Values.SelectMany(x => x))));
                    continue;
                }
                var ice = PartenairesEndpoints.Clean(input.Ice);
                var cin = PartenairesEndpoints.Clean(input.Cin)?.ToUpperInvariant();
                if ((input.Nature == Natures.Morale && ice is not null && !ices.Add(ice)) || (input.Nature == Natures.Physique && cin is not null && !cins.Add(cin)))
                {
                    resultat.Add(new LigneImport(numero, nom, TypesPartenaire.Labels[input.Type], input.Nature, "doublon",
                        input.Nature == Natures.Morale ? $"ICE {ice} déjà connu (dans PROGIX ou plus haut dans le fichier) : ligne ignorée." : $"CIN {cin} déjà connue (dans PROGIX ou plus haut dans le fichier) : ligne ignorée."));
                    continue;
                }
                // Même nom ou même téléphone : la ligne est créée, l'avertissement invite à vérifier.
                var cle = PartenairesEndpoints.NomNormalise(input.Nature == Natures.Physique ? $"{input.Civilite} {input.Prenom} {input.NomFamille}" : input.RaisonSociale);
                var tel = PartenairesEndpoints.Chiffres(input.Telephone);
                var avertissements = new List<string>();
                if (cle.Length >= 3 && !noms.Add(cle)) avertissements.Add("même nom qu'un partenaire existant");
                if (tel.Length >= 9 && !telephones.Add(tel)) avertissements.Add("même téléphone qu'un partenaire existant");

                var p = new Partenaire();
                PartenairesEndpoints.Appliquer(p, input);
                p.Contacts = (input.Contacts ?? []).Select(c => new Contact { Nom = c.Nom.Trim(), Fonction = PartenairesEndpoints.Clean(c.Fonction), Telephone = PartenairesEndpoints.Clean(c.Telephone), Email = PartenairesEndpoints.Clean(c.Email) }).ToList();
                aCreer.Add(p);
                resultat.Add(new LigneImport(numero, nom, TypesPartenaire.Labels[input.Type], input.Nature, enregistrer == true ? "cree" : "a_creer",
                    avertissements.Count == 0 ? "" : $"Doublon possible : {string.Join(", ", avertissements)}."));
            }

            if (enregistrer == true && aCreer.Count > 0)
            {
                db.Partenaires.AddRange(aCreer);
                await db.SaveChangesAsync();
            }
            return Results.Ok(new
            {
                enregistre = enregistrer == true,
                valides = aCreer.Count,
                doublons = resultat.Count(r => r.Statut == "doublon"),
                erreurs = resultat.Count(r => r.Statut == "erreur"),
                avertissements = resultat.Count(r => r.Message.StartsWith("Doublon possible")),
                lignes = resultat,
            });
        }).DisableAntiforgery();
    }

    private static IResult Conflit(string message) => Results.Conflict(new { message });

    /// <summary>Texte comparable : minuscules, sans accents, sans astérisque ni espaces superflus.</summary>
    private static string Norme(string? s) => new string((s ?? "").Replace("*", "").Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).Replace("  ", " ");

    private static readonly Dictionary<string, string> Types = TypesPartenaire.Labels
        .SelectMany(t => new[] { (Norme(t.Key), t.Key), (Norme(t.Value), t.Key), (Norme(t.Key.Replace('_', ' ')), t.Key) })
        .DistinctBy(x => x.Item1).ToDictionary(x => x.Item1, x => x.Item2);

    private static string? Nature(string? s) => Norme(s) switch
    {
        "personne morale" or "morale" or "societe" => Natures.Morale,
        "personne physique" or "physique" or "particulier" => Natures.Physique,
        "autre" => Natures.Autre,
        _ => null,
    };

    private static bool? OuiNon(string? s) => Norme(s) switch
    {
        "" => false,
        "oui" or "o" or "x" or "1" or "vrai" => true,
        "non" or "n" or "0" or "faux" => false,
        _ => null,
    };

    /// <summary>Convertit une ligne lue en saisie de fiche, ou renvoie le motif de refus.</summary>
    internal static (PartenaireInput? Input, string? Refus) VersInput(IReadOnlyDictionary<string, string?> l)
    {
        string? V(string colonne) => PartenairesEndpoints.Clean(l.GetValueOrDefault(colonne));
        if (!Types.TryGetValue(Norme(V("Type*")), out var type))
            return (null, $"Type « {V("Type*")} » inconnu : {string.Join(", ", TypesPartenaire.Labels.Values)}.");
        if (Nature(V("Catégorie*")) is not { } nature)
            return (null, $"Catégorie « {V("Catégorie*")} » inconnue : Personne morale, Personne physique ou Autre.");
        if (OuiNon(V("Exonéré de TVA")) is not { } exonere)
            return (null, $"Exonéré de TVA : « {V("Exonéré de TVA")} » non compris, attendu Oui ou Non.");
        var physique = nature == Natures.Physique;
        List<ContactDto> contacts = V("Contact") is { } contact
            ? [new ContactDto(null, contact, V("Fonction du contact"), V("Téléphone du contact"), V("Email du contact"))] : [];
        return (new PartenaireInput(type, nature,
            RaisonSociale: physique ? null : V("Raison sociale ou nom*"), Ice: V("ICE")?.Replace(" ", ""), Rc: V("RC"), IdentifiantFiscal: V("Identifiant fiscal"), Patente: V("Patente"),
            Civilite: physique ? V("Civilité") ?? "M." : null, NomFamille: physique ? V("Raison sociale ou nom*") : null, Prenom: physique ? V("Prénom") : null,
            Cin: V("CIN"), Nationalite: physique ? "Marocaine" : null, DateNaissance: null, LieuNaissance: null,
            Adresse: V("Adresse"), Ville: V("Ville"), Telephone: V("Téléphone"), Email: V("Email"), ExonereTva: exonere, Contacts: contacts), null);
    }

    /// <summary>Lignes du premier onglet, par colonne de la trame. Les lignes entièrement vides sont ignorées.</summary>
    private static List<Dictionary<string, string?>> Lire(byte[] contenu, out List<string> manquantes)
    {
        using var flux = new MemoryStream(contenu);
        using var wb = new XLWorkbook(flux);
        var ws = wb.Worksheets.First();
        var derniere = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        var positions = new Dictionary<string, int>();
        for (var c = 1; c <= derniere; c++)
        {
            var titre = Norme(ws.Cell(1, c).GetString());
            if (Colonnes.FirstOrDefault(x => Norme(x) == titre) is { } colonne) positions.TryAdd(colonne, c);
        }
        manquantes = Colonnes.Where(c => c.EndsWith('*') && !positions.ContainsKey(c)).Select(c => c.TrimEnd('*')).ToList();
        var lignes = new List<Dictionary<string, string?>>();
        if (manquantes.Count > 0) return lignes;
        var fin = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= fin; r++)
        {
            var l = Colonnes.ToDictionary(c => c, c => positions.TryGetValue(c, out var col) ? Texte(ws.Cell(r, col)) : null);
            if (l.Values.All(string.IsNullOrWhiteSpace)) continue;
            lignes.Add(l);
        }
        return lignes;
    }

    /// <summary>Valeur d'une cellule en texte. Un nombre saisi sans format texte (ICE, téléphone) est écrit sans décimales ni exposant.</summary>
    private static string? Texte(IXLCell c) => c.DataType switch
    {
        XLDataType.Blank => null,
        XLDataType.Number => c.GetDouble().ToString("0.##########", CultureInfo.InvariantCulture),
        XLDataType.Boolean => c.GetBoolean() ? "oui" : "non",
        _ => c.GetString(),
    };

    private static byte[] Trame()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Clients et partenaires");
        for (var c = 0; c < Colonnes.Length; c++)
        {
            ws.Cell(1, c + 1).Value = Colonnes[c];
            if (Textes.Contains(Colonnes[c])) ws.Column(c + 1).Style.NumberFormat.Format = "@";
        }
        var entete = ws.Range(1, 1, 1, Colonnes.Length);
        entete.Style.Font.Bold = true; entete.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B2A6B"); entete.Style.Font.FontColor = XLColor.White;
        string[][] exemples =
        [
            ["Client final", "Personne morale", "Immobilière Exemple SARL", "", "", "001234567000089", "123456", "40123456", "30123456", "", "12, rue d'exemple", "Casablanca", "0522000000", "contact@exemple.ma", "Non", "Karim Exemple", "Gérant", "0661000000", "k.exemple@exemple.ma"],
            ["Client final", "Personne physique", "Bennani", "Youssef", "M.", "", "", "", "", "AB123456", "Villa 4, quartier exemple", "Rabat", "0661111111", "", "Non", "", "", "", ""],
            ["BET", "Autre", "Bureau d'études exemple", "", "", "", "", "", "", "", "", "Marrakech", "", "", "Non", "", "", "", ""],
        ];
        for (var r = 0; r < exemples.Length; r++)
            for (var c = 0; c < Colonnes.Length; c++)
                ws.Cell(r + 2, c + 1).SetValue(exemples[r][c]);
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);

        var aide = wb.AddWorksheet("Aide");
        string[] lignes =
        [
            "Une ligne par client ou partenaire, dans le premier onglet. Remplacez les trois lignes d'exemple. Les colonnes marquées * sont obligatoires.",
            $"Type* : {string.Join(", ", TypesPartenaire.Labels.Values)}.",
            "Catégorie* : Personne morale, Personne physique ou Autre (un nom suffit, aucun identifiant légal exigé).",
            "Raison sociale ou nom* : raison sociale d'une personne morale ou d'un partenaire « Autre » ; nom de famille d'une personne physique (avec le prénom, obligatoire, dans la colonne Prénom).",
            "Personne morale : ICE de 15 chiffres. ICE et RC seront exigés pour établir une facture ; identifiant fiscal et patente en plus pour un contrat.",
            "Personne physique : CIN, civilité (M. ou Mme, M. par défaut). La nationalité est mise à Marocaine, modifiable ensuite dans la fiche.",
            "Exonéré de TVA : Oui ou Non (vide = Non). L'attestation d'exonération se renseigne ensuite dans la fiche.",
            "Contact : un contact principal facultatif ; les autres se saisissent dans la fiche.",
            "Doublons : une ligne dont l'ICE ou la CIN est déjà connu est ignorée ; un même nom ou un même téléphone est signalé mais la ligne est créée.",
            "Les colonnes ICE, CIN et téléphones sont au format texte pour garder les zéros en tête : collez-y les valeurs sans les retaper en nombre.",
        ];
        for (var r = 0; r < lignes.Length; r++) aide.Cell(r + 1, 1).Value = lignes[r];
        aide.Column(1).Width = 140;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
