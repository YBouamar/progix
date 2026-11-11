using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

public record DalleInput(string? Bloc, string? Immeuble, int Niveau, string? Detail, decimal Surface);
public record DallesParNiveauxInput(string? Bloc, string? Immeuble, List<int>? Niveaux);
public record NiveauParamInput(int? Valeur, string? Libelle, bool Actif);
public record NiveauxParamInput(List<NiveauParamInput>? Niveaux);

public static class DallesEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        // Niveaux des dalles : liste ordonnée et libellés, réglés dans le Paramétrage par le directeur ou le chef de projet.
        var param = app.MapGroup("/api/parametres/niveaux").WithTags("Paramétrage").RequireAuthorization();
        param.MapGet("/", async (ProjetsDbContext db, ClaimsPrincipal user) => Results.Ok(await NiveauxParam(db, user)));
        param.MapPut("/", async (NiveauxParamInput input, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var liste = (input.Niveaux ?? []).Select(n => n with { Libelle = n.Libelle?.Trim() }).ToList();
            if (liste.Any(n => string.IsNullOrEmpty(n.Libelle))) return ProjetsEndpoints.Conflit("Chaque niveau a un libellé.");
            if (liste.Any(n => n.Libelle!.Length > 60)) return ProjetsEndpoints.Conflit("Un libellé de niveau compte au plus 60 caractères.");
            if (liste.GroupBy(n => n.Libelle!.ToUpperInvariant()).FirstOrDefault(x => x.Count() > 1) is { } double_)
                return ProjetsEndpoints.Conflit($"Le niveau « {double_.First().Libelle} » figure deux fois.");
            if (!liste.Any(n => n.Actif)) return ProjetsEndpoints.Conflit("Au moins un niveau doit rester proposé.");
            var existants = await db.Niveaux.ToListAsync();
            if (liste.Any(n => n.Valeur is int v && existants.All(e => e.Valeur != v))) return ProjetsEndpoints.Conflit("Un niveau de la liste n'existe plus : rechargez la page.");
            // Un niveau porté par une dalle ne se supprime pas : il se désactive, pour que la dalle garde son libellé.
            var retires = existants.Where(e => liste.All(n => n.Valeur != e.Valeur)).ToList();
            var valeursRetirees = retires.Select(r => r.Valeur).ToList();
            var utilises = await db.Dalles.Where(d => valeursRetirees.Contains(d.Niveau)).GroupBy(d => d.Niveau).Select(x => new { x.Key, Nb = x.Count() }).ToListAsync();
            if (utilises.FirstOrDefault() is { } u)
                return ProjetsEndpoints.Conflit($"Le niveau « {retires.First(r => r.Valeur == u.Key).Libelle} » est porté par {u.Nb} dalle(s) : décochez « Proposé » au lieu de le supprimer.");
            db.Niveaux.RemoveRange(retires);
            // Un nouveau niveau reçoit une valeur jamais utilisée ; sa place dans la liste vient de son rang.
            var suivante = existants.Select(e => e.Valeur).Append(0).Max() + 1;
            for (var i = 0; i < liste.Count; i++)
            {
                var n = existants.FirstOrDefault(e => e.Valeur == liste[i].Valeur);
                if (n is null) db.Niveaux.Add(n = new NiveauDalle { Valeur = suivante++ });
                n.Libelle = liste[i].Libelle!;
                n.Actif = liste[i].Actif;
                n.Ordre = i;
                n.UpdatedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
            NiveauxDalle.Charger(await db.Niveaux.AsNoTracking().ToListAsync());
            return Results.Ok(await NiveauxParam(db, user));
        }).RequireAuthorization(Policies.Direction);

        var g = app.MapGroup("/api/projets/{id:guid}/dalles").WithTags("Dalles").RequireAuthorization();

        g.MapGet("/", async (Guid id, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (projet is null) return Results.NotFound();
            var dalles = await db.Dalles.AsNoTracking().Where(d => d.ProjetId == id).OrderBy(d => d.Numero).ToListAsync();
            var niveaux = await db.Niveaux.AsNoTracking().OrderBy(n => n.Ordre).ToListAsync();
            return Results.Ok(new
            {
                // Niveaux proposés, plus ceux qui ne le sont plus mais que portent encore des dalles du projet.
                niveaux = niveaux.Where(n => n.Actif || dalles.Any(d => d.Niveau == n.Valeur))
                    .Select(n => new { valeur = n.Valeur, label = n.Actif ? n.Libelle : $"{n.Libelle} (n'est plus proposé)", actif = n.Actif }),
                dalles = dalles.Select(d => Dto(d, projet.Code)),
                peutImporterAdapt = !projet.Supprime && RolesAdapt.Any(user.IsInRole),
            });
        });

        g.MapPost("/", async (Guid id, DalleInput input, ProjetsDbContext db) =>
        {
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (projet is null) return Results.NotFound();
            if (projet.Supprime) return ProjetsEndpoints.Conflit("Un projet supprimé est en lecture seule.");
            var erreurs = await Valider(input, db, null);
            if (erreurs.Count > 0) return Results.ValidationProblem(erreurs);

            await using var tx = await db.Database.BeginTransactionAsync();
            var d = new Dalle { ProjetId = id, Numero = await db.NumeroSuivantAsync($"dalle:{id:N}") };
            Appliquer(d, input);
            db.Dalles.Add(d);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(Dto(d, projet.Code));
        }).RequireAuthorization(Policies.Commercial);

        g.MapPut("/{did:guid}", async (Guid id, Guid did, DalleInput input, ProjetsDbContext db, IDevisLookup devis) =>
        {
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            var d = await db.Dalles.FirstOrDefaultAsync(x => x.Id == did && x.ProjetId == id);
            if (projet is null || d is null) return Results.NotFound();
            if (projet.Supprime) return ProjetsEndpoints.Conflit("Un projet supprimé est en lecture seule.");
            var erreurs = await Valider(input, db, d.Niveau);
            // Toute modification de surface d'une dalle chiffrée dans un devis envoyé ou validé impose une nouvelle version de ce devis.
            if (!erreurs.ContainsKey("surface") && Math.Round(input.Surface, 2) != d.Surface
                && await devis.DevisSurAutreSurfaceAsync(d.Id, Math.Round(input.Surface, 2)) is { } engage)
                erreurs["surface"] = [$"La surface est chiffrée dans le devis {engage} : créez d'abord une nouvelle version de ce devis, puis modifiez la surface."];
            if (erreurs.Count > 0) return Results.ValidationProblem(erreurs);
            Appliquer(d, input);
            d.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(Dto(d, projet.Code));
        }).RequireAuthorization(Policies.Commercial);

        // Création des dalles d'un coup : une dalle par niveau coché, dans l'ordre de la liste des niveaux, surface à renseigner.
        g.MapPost("/niveaux", async (Guid id, DallesParNiveauxInput input, ProjetsDbContext db) =>
        {
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (projet is null) return Results.NotFound();
            if (projet.Supprime) return ProjetsEndpoints.Conflit("Un projet supprimé est en lecture seule.");
            var coches = (input.Niveaux ?? []).Distinct().ToList();
            if (coches.Count == 0) return ProjetsEndpoints.Conflit("Cochez au moins un niveau.");
            var niveaux = await db.Niveaux.AsNoTracking().Where(n => n.Actif && coches.Contains(n.Valeur)).OrderBy(n => n.Ordre).ToListAsync();
            if (niveaux.Count != coches.Count) return ProjetsEndpoints.Conflit("Un niveau coché est inconnu ou n'est plus proposé : rechargez la page.");

            await using var tx = await db.Database.BeginTransactionAsync();
            var crees = new List<Dalle>();
            foreach (var n in niveaux)
            {
                var d = new Dalle { ProjetId = id, Numero = await db.NumeroSuivantAsync($"dalle:{id:N}") };
                Appliquer(d, new DalleInput(input.Bloc, input.Immeuble, n.Valeur, null, 0));
                db.Dalles.Add(d);
                crees.Add(d);
            }
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(crees.Select(d => Dto(d, projet.Code)));
        }).RequireAuthorization(Policies.Commercial);

        // Import des deux exports ADAPT de la dalle (PTTotal et RebarTotal) : ratios, types d'ancrage, ancrages et gaines.
        g.MapPost("/{did:guid}/adapt", async (Guid id, Guid did, HttpRequest req, ProjetsDbContext db, IDevisLookup devis, IFichiersContenu depot, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!RolesAdapt.Any(user.IsInRole)) return Results.Forbid();
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            var d = await db.Dalles.FirstOrDefaultAsync(x => x.Id == did && x.ProjetId == id);
            if (projet is null || d is null) return Results.NotFound();
            if (projet.Supprime) return ProjetsEndpoints.Conflit("Un projet supprimé est en lecture seule.");
            if (!req.HasFormContentType) return ProjetsEndpoints.Conflit("Déposez les deux exports ADAPT de la dalle.");
            var fichiers = (await req.ReadFormAsync()).Files;
            if (fichiers.Count != 2) return ProjetsEndpoints.Conflit("Déposez les deux exports ADAPT de la dalle : PTTotal et RebarTotal.");
            if (fichiers.Any(f => f.Length is 0 or > TailleMaxAdapt)) return ProjetsEndpoints.Conflit("Chaque export ADAPT pèse au plus 5 Mo.");
            var contenus = new List<string>();
            var octets = new List<byte[]>();
            foreach (var f in fichiers)
            {
                var (brut, texte) = await LireAsync(f);
                if (!texte.StartsWith(@"{\rtf")) return ProjetsEndpoints.Conflit($"{f.FileName} n'est pas un export RTF d'ADAPT (fichier .rtf ou .rtf.doc).");
                contenus.Add(texte); octets.Add(brut);
            }
            var r = LectureAdapt.Analyser(contenus[0], contenus[1]);
            if (r.Donnees is not { } a) return ProjetsEndpoints.Conflit(r.Erreur!);

            var avertissements = LectureAdapt.Avertissements(contenus[0], contenus[1], a, d.Surface);
            // Une dalle sans surface prend celle du plancher ADAPT ; sinon sa surface est conservée et l'écart signalé.
            if (d.Surface == 0)
            {
                var surface = Math.Round(a.SurfacePlancher, 2);
                if (await devis.DevisSurAutreSurfaceAsync(d.Id, surface) is { } engage)
                    return ProjetsEndpoints.Conflit($"La surface est chiffrée dans le devis {engage} : créez d'abord une nouvelle version de ce devis.");
                d.Surface = surface;
                avertissements.Add("La surface de la dalle est reprise du plancher ADAPT.");
            }
            var pt = LectureAdapt.Lire(contenus[0]).Genre == LectureAdapt.Genre.Precontrainte ? 0 : 1;
            a.FichierPt = Nom(fichiers[pt].FileName);
            a.FichierRebar = Nom(fichiers[1 - pt].FileName);
            // Les fichiers d'origine sont conservés avec la dalle.
            a.FichierPtId = (await depot.DeposerAsync(AdaptDalle.Contexte, d.Id, AdaptDalle.CategoriePt, a.FichierPt, "application/rtf", new MemoryStream(octets[pt]), user, ct)).Id;
            a.FichierRebarId = (await depot.DeposerAsync(AdaptDalle.Contexte, d.Id, AdaptDalle.CategorieRebar, a.FichierRebar, "application/rtf", new MemoryStream(octets[1 - pt]), user, ct)).Id;
            a.ImporteLe = DateTime.UtcNow;
            a.ImportePar = user.UserName();
            d.Adapt = a;
            d.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { dalle = Dto(d, projet.Code), avertissements });
        }).DisableAntiforgery();

        // Dépôt groupé des exports ADAPT de plusieurs dalles (fiche du devis) : chaque fichier est reconnu (PTTotal ou RebarTotal),
        // lu, puis rattaché à la dalle désignée, ou à celle que sa surface désigne sans doute possible. Sinon il attend qu'on choisisse sa dalle.
        // Champs du formulaire : « fichiers » ; « dalles », un identifiant ou vide par fichier, dans le même ordre ;
        // « candidates », les dalles entre lesquelles deviner (celles du devis), toutes celles du projet par défaut.
        g.MapPost("/adapt", async (Guid id, HttpRequest req, ProjetsDbContext db, IDevisLookup devis, IFichiersContenu depot, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!RolesAdapt.Any(user.IsInRole)) return Results.Forbid();
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (projet is null) return Results.NotFound();
            if (projet.Supprime) return ProjetsEndpoints.Conflit("Un projet supprimé est en lecture seule.");
            if (!req.HasFormContentType) return ProjetsEndpoints.Conflit("Déposez les exports ADAPT : PTTotal et RebarTotal.");
            var form = await req.ReadFormAsync();
            if (form.Files.Count is 0 or > MaxFichiersAdapt) return ProjetsEndpoints.Conflit($"Déposez entre 1 et {MaxFichiersAdapt} exports ADAPT à la fois.");
            var dalles = await db.Dalles.Where(x => x.ProjetId == id).OrderBy(x => x.Numero).ToListAsync();
            var designees = form["dalles"].Select(v => Guid.TryParse(v, out var x) ? x : (Guid?)null).ToList();
            var restreintes = form["candidates"].Select(v => Guid.TryParse(v, out var x) ? x : Guid.Empty).ToHashSet();
            var possibles = restreintes.Count == 0 ? dalles : dalles.Where(x => restreintes.Contains(x.Id)).ToList();

            // Lecture de tous les fichiers, puis rattachement : les PTTotal d'abord, pour qu'un RebarTotal du même dépôt retrouve la dalle par la surface du plancher.
            var lus = new List<(int Rang, IFormFile Fichier, byte[] Octets, LectureAdapt.Fichier Lu, Guid? Dalle)>();
            for (var i = 0; i < form.Files.Count; i++)
            {
                var f = form.Files[i];
                if (f.Length is 0 or > TailleMaxAdapt) { lus.Add((i, f, [], new(LectureAdapt.Genre.Inconnu, 0, 0, null, [], "Chaque export ADAPT pèse au plus 5 Mo."), null)); continue; }
                var (brut, texte) = await LireAsync(f);
                lus.Add((i, f, brut, LectureAdapt.AnalyserUn(texte), i < designees.Count ? designees[i] : null));
            }

            var resultats = new object?[lus.Count];
            var recus = new HashSet<(Guid, LectureAdapt.Genre)>();
            var touchees = new HashSet<Guid>();
            object Resultat(IFormFile f, LectureAdapt.Fichier lu, string etat, Dalle? d, string? message, Guid? proposee = null, List<string>? avertissements = null) => new
            {
                nom = Nom(f.FileName), genre = lu.Genre == LectureAdapt.Genre.Precontrainte ? "pt" : lu.Genre == LectureAdapt.Genre.Armatures ? "rebar" : null,
                etat, dalleId = d?.Id, reference = d is null ? null : $"{projet.Code}-{d.Suffixe}", message, surface = lu.Surface, ratio = lu.Ratio, proposee,
                avertissements = avertissements ?? [],
            };
            RapprochementAdapt.Candidat Candidat(Dalle x) => new(x.Id, x.Surface, x.Adapt?.SurfacePlancher, x.Adapt?.AvecPt == true, x.Adapt?.AvecRebar == true);

            foreach (var (rang, f, octets, lu, designee) in lus.OrderBy(x => x.Lu.Genre == LectureAdapt.Genre.Precontrainte ? 0 : 1).ThenBy(x => x.Rang))
            {
                if (lu.Erreur is not null) { resultats[rang] = Resultat(f, lu, "refuse", null, lu.Erreur); continue; }
                Dalle? d;
                if (designee is Guid choisie)
                {
                    d = dalles.FirstOrDefault(x => x.Id == choisie);
                    if (d is null) { resultats[rang] = Resultat(f, lu, "refuse", null, "Dalle introuvable dans ce projet."); continue; }
                }
                else
                {
                    var sure = RapprochementAdapt.Deviner(lu.Surface, possibles.Select(Candidat).ToList());
                    d = sure is null ? null : possibles.First(x => x.Id == sure);
                    // Deux fichiers du même genre pour la même dalle dans un seul dépôt : le second attend, on ne devine pas lequel est le bon.
                    if (d is null || recus.Contains((d.Id, lu.Genre)))
                    {
                        resultats[rang] = Resultat(f, lu, "attente", null, $"{lu.Nom} lu ({lu.Surface.ToString("N2", Fr)} m² de plancher) : choisissez sa dalle.",
                            RapprochementAdapt.Proposer(lu.Surface, lu.Genre, possibles.Select(Candidat).ToList()));
                        continue;
                    }
                }

                var avertissements = LectureAdapt.Avertissements(lu, d.Adapt, d.Surface);
                // Une dalle sans surface prend celle du plancher ADAPT ; sinon sa surface est conservée et l'écart signalé.
                if (d.Surface == 0)
                {
                    var surface = Math.Round(lu.Surface, 2);
                    if (await devis.DevisSurAutreSurfaceAsync(d.Id, surface) is { } engage)
                    { resultats[rang] = Resultat(f, lu, "refuse", d, $"La surface est chiffrée dans le devis {engage} : créez d'abord une nouvelle version de ce devis."); continue; }
                    d.Surface = surface;
                    avertissements.Add("La surface de la dalle est reprise du plancher ADAPT.");
                }
                var nom = Nom(f.FileName);
                var a = LectureAdapt.Reporter(d.Adapt, lu, nom);
                var pt = lu.Genre == LectureAdapt.Genre.Precontrainte;
                var garde = await depot.DeposerAsync(AdaptDalle.Contexte, d.Id, pt ? AdaptDalle.CategoriePt : AdaptDalle.CategorieRebar, nom, "application/rtf", new MemoryStream(octets), user, ct);
                if (pt) a.FichierPtId = garde.Id; else a.FichierRebarId = garde.Id;
                a.ImporteLe = DateTime.UtcNow;
                a.ImportePar = user.UserName();
                d.Adapt = a;
                d.UpdatedAt = DateTime.UtcNow;
                recus.Add((d.Id, lu.Genre));
                touchees.Add(d.Id);
                resultats[rang] = Resultat(f, lu, "rattache", d, null, null, avertissements);
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { fichiers = resultats, dalles = dalles.Where(x => touchees.Contains(x.Id)).Select(x => Dto(x, projet.Code)) });
        }).DisableAntiforgery();
    }

    private static readonly System.Globalization.CultureInfo Fr = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
    private const int MaxFichiersAdapt = 40;

    /// <summary>Contenu d'un export : ses octets, gardés tels quels, et son texte (les exports ADAPT sont en Latin-1).</summary>
    private static async Task<(byte[] Octets, string Texte)> LireAsync(IFormFile f)
    {
        using var tampon = new MemoryStream();
        await f.CopyToAsync(tampon);
        var octets = tampon.ToArray();
        return (octets, System.Text.Encoding.Latin1.GetString(octets));
    }

    private const long TailleMaxAdapt = 5 * 1024 * 1024;

    /// <summary>Import ADAPT : rôles qui chiffrent les dalles, sans l'administrateur (données techniques).</summary>
    internal static readonly string[] RolesAdapt =
        [Roles.Directeur, Roles.ChefProjet, Roles.ResponsableAdministratif, Roles.IngenieurChef, Roles.Ingenieur];

    private static string Nom(string f) { var n = Path.GetFileName(f); return n.Length > 200 ? n[..200] : n; }

    /// <param name="niveauActuel">Niveau déjà porté par la dalle : il reste accepté même s'il n'est plus proposé.</param>
    private static async Task<Dictionary<string, string[]>> Valider(DalleInput i, ProjetsDbContext db, int? niveauActuel)
    {
        var e = new Dictionary<string, string[]>();
        // Surface laissée vide (0) : elle se renseigne plus tard, à la main ou par l'import ADAPT.
        if (i.Surface < 0) e["surface"] = ["La surface ne peut pas être négative."];
        if (i.Niveau != niveauActuel && !await db.Niveaux.AnyAsync(n => n.Valeur == i.Niveau && n.Actif)) e["niveau"] = ["Niveau inconnu ou plus proposé."];
        return e;
    }

    private static async Task<object> NiveauxParam(ProjetsDbContext db, ClaimsPrincipal user)
    {
        var niveaux = await db.Niveaux.AsNoTracking().OrderBy(n => n.Ordre).ToListAsync();
        var parNiveau = await db.Dalles.GroupBy(d => d.Niveau).Select(x => new { x.Key, Nb = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Nb);
        return new
        {
            modifiable = Policies.RolesDirection.Any(user.IsInRole),
            niveaux = niveaux.Select(n => new { valeur = n.Valeur, libelle = n.Libelle, actif = n.Actif, dalles = parNiveau.GetValueOrDefault(n.Valeur) }),
        };
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static void Appliquer(Dalle d, DalleInput i)
    {
        d.Bloc = Clean(i.Bloc);
        d.Immeuble = Clean(i.Immeuble);
        d.Niveau = i.Niveau;
        d.Detail = Clean(i.Detail);
        d.Surface = Math.Round(i.Surface, 2);
    }

    internal static object Dto(Dalle d, string codeProjet) => new
    {
        id = d.Id, reference = $"{codeProjet}-{d.Suffixe}", numero = d.Numero, version = d.Version.ToString(),
        produit = Dalle.Produit, bloc = d.Bloc, immeuble = d.Immeuble,
        niveau = d.Niveau, niveauLabel = Dalle.NiveauLabel(d.Niveau), detail = d.Detail, surface = d.Surface,
        dessinateurId = d.DessinateurId, dessinateur = d.DessinateurNom,
        etapesFaites = d.EtapesFaites,
        adapt = d.Adapt is not { } a ? null : new
        {
            ratioActif = a.RatioActif, ratioPassif = a.RatioPassif, surfacePlancher = a.SurfacePlancher, plancher = a.Plancher,
            fichierPt = a.FichierPt, fichierRebar = a.FichierRebar, fichierPtId = a.FichierPtId, fichierRebarId = a.FichierRebarId, importeLe = a.ImporteLe, importePar = a.ImportePar,
            systemes = a.Systemes.Select(s => new { torons = s.Torons, ancrages = s.Cables, gaine = s.LongueurTorons, longueurGaines = s.LongueurGaines }),
        },
        etape = d.EtapesFaites >= Etapes.Total ? "Réalisée" : d.EtapesFaites == 0 ? "Non commencée" : $"{d.EtapesFaites + 1} · {Etapes.Get(d.EtapesFaites + 1).Label}",
    };
}
