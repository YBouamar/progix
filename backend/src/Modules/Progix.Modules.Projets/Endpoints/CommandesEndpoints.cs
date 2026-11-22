using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

public record ChangementEtatInput(string Etat, string? Motif);
public record SurfaceDalleInput(decimal Surface);

/// <summary>
/// Les commandes usine d'une dalle : la commande initiale, puis des compléments envoyés plus tard au chantier.
/// Chaque commande se modifie après sa soumission, même envoyée : les changements sont tracés et signalés à l'usine.
/// Cycle : à préparer, préparée, envoyée au chantier, reçue au chantier ; annulation possible à tout moment.
/// </summary>
public static class CommandesEndpoints
{
    public static readonly (string Code, string Label)[] Etats =
    [
        (CommandeUsine.Attente, "En attente de validation"), (CommandeUsine.APreparer, "À préparer"), (CommandeUsine.Preparee, "Préparée"),
        (CommandeUsine.Envoyee, "Envoyée au chantier"), (CommandeUsine.Recue, "Reçue au chantier"), (CommandeUsine.Annulee_, "Annulée"),
    ];

    public static string LabelEtat(string code) => Etats.FirstOrDefault(e => e.Code == code).Label ?? code;

    // Qui fait quoi : la préparation et l'envoi reviennent au magasin et à la logistique ; la réception au chantier aussi ; l'annulation aux responsables.
    private static readonly string[] RolesPreparation = [Roles.GestionnaireStock, Roles.ResponsableLogistique, Roles.ChefProjet, Roles.Directeur];
    private static readonly string[] RolesReception = [Roles.GestionnaireStock, Roles.ResponsableLogistique, Roles.ChefChantier, Roles.ResponsableChantier, Roles.ChefProjet, Roles.Directeur];
    private static readonly string[] RolesAnnulation = [Roles.ResponsableLogistique, Roles.ResponsableChantier, Roles.ChefProjet, Roles.Directeur];

    public static bool PeutPreparer(ClaimsPrincipal u) => RolesPreparation.Any(u.IsInRole);
    public static bool PeutRecevoir(ClaimsPrincipal u) => RolesReception.Any(u.IsInRole);
    public static bool PeutAnnuler(ClaimsPrincipal u) => RolesAnnulation.Any(u.IsInRole);

    private static string Libelle(int torons, string sens, decimal longueur, int ancrages) =>
        $"S{torons} · {sens} · {longueur.ToString("0.00", Fr)} m · {ancrages} ancrage{(ancrages > 1 ? "s" : "")}";
    private static readonly System.Globalization.CultureInfo Fr = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
    private static string M(decimal v) => v.ToString("0.00", Fr) + " m";

    private static string? Blocage(Projet p) =>
        p.Supprime ? "Un projet supprimé est en lecture seule." : p.Bloque ? "Projet bloqué : les étapes de chantier sont suspendues." : null;

    /// <summary>Vrai dès que la commande initiale est soumise à validation (étape 4 faite) : les compléments et les modifications tracées deviennent possibles.</summary>
    private static Task<bool> Soumise(ProjetsDbContext db, Guid dalleId) =>
        db.Etapes.AnyAsync(e => e.DalleId == dalleId && e.Numero == Etapes.CommandeUsine && e.FaitLe != null);

    internal static object Resume(CommandeUsine c, bool validee) => new
    {
        id = c.Id, numero = c.Numero, etat = c.Etat(validee), etatLabel = LabelEtat(c.Etat(validee)),
        tendons = c.Lignes.Count, surface = c.Surface, modifiee = c.Modifications.Count > 0,
        fabricationLe = c.FabricationLe, livreeLe = c.LivreeLe, recueLe = c.RecueLe, annuleeLe = c.AnnuleeLe,
    };

    private static object Detail(CommandeUsine c, bool validee) => new
    {
        id = c.Id, numero = c.Numero, etat = c.Etat(validee), etatLabel = LabelEtat(c.Etat(validee)),
        surface = c.Surface,
        tendons = c.Lignes.Select(l => new
        {
            numero = l.Numero, torons = l.Torons, sens = l.Sens, longueur = l.Longueur, ancrages = l.Ancrages,
            modifiee = l.ModifieLe != null && !l.Ajoutee, ajoutee = l.Ajoutee, avant = l.Avant, modifieLe = l.ModifieLe,
        }),
        accessoires = c.Accessoires.Select(a => new { libelle = a.Libelle, unite = a.Unite, quantite = a.Quantite }),
        modifications = c.Modifications.OrderByDescending(m => m.Le).Select(m => new { le = m.Le, par = m.Par, etat = LabelEtat(m.Etat), changements = m.Changements }),
        fabricationLe = c.FabricationLe, livreeLe = c.LivreeLe, recueLe = c.RecueLe, suiviPar = c.SuiviPar,
        annuleeLe = c.AnnuleeLe, annuleePar = c.AnnuleePar, motifAnnulation = c.MotifAnnulation,
    };

    /// <summary>Numéros de tendon déjà pris par les autres commandes non annulées de la dalle : un tendon ne se commande qu'une fois.</summary>
    private static async Task<string?> Doublon(ProjetsDbContext db, Guid dalleId, Guid? sauf, IEnumerable<TendonInput> tendons)
    {
        var autres = await db.CommandesUsine.AsNoTracking().Where(c => c.DalleId == dalleId && c.AnnuleeLe == null && c.Id != sauf).ToListAsync();
        foreach (var t in tendons)
        {
            var n = ImportTendons.Numero(t.Numero);
            if (autres.FirstOrDefault(c => c.Lignes.Any(l => l.Numero == n)) is { } pris)
                return $"Le tendon {n} figure déjà dans la commande n° {pris.Numero} de cette dalle.";
        }
        return null;
    }

    private static List<LigneCommande> NouvellesLignes(IEnumerable<TendonInput> tendons) => tendons.Select(t => new LigneCommande
    {
        Numero = ImportTendons.Numero(t.Numero)!, Torons = t.Torons, Sens = ImportTendons.Sens(t.Sens)!, Prefixe = "", Nombre = 1,
        Longueur = Math.Round(t.Longueur, 2), AncragesSaisis = t.Ancrages,
    }).ToList();

    private static async Task<List<AccessoireCommande>> Accessoires(ProjetsDbContext db, IEnumerable<AccessoireInput> saisis)
    {
        var parametres = await db.Accessoires.AsNoTracking().ToListAsync();
        return saisis.Where(a => !string.IsNullOrWhiteSpace(a.Libelle) && a.Quantite != 0).Select(a => new AccessoireCommande
        {
            AccessoireId = parametres.FirstOrDefault(x => x.Libelle == a.Libelle!.Trim())?.Id, Libelle = a.Libelle!.Trim(), Quantite = a.Quantite,
            Unite = string.IsNullOrWhiteSpace(a.Unite) ? null : a.Unite.Trim(),
        }).ToList();
    }

    /// <summary>
    /// Applique une modification à une commande déjà soumise et trace chaque changement : tendon changé (valeurs d'avant gardées sur la ligne),
    /// ajouté ou retiré, accessoire, surface. Renvoie la liste des changements, vide si rien n'a changé.
    /// </summary>
    internal static List<string> Appliquer(CommandeUsine c, List<TendonInput> tendons, List<AccessoireCommande> accessoires, decimal? surface, DateTime maintenant)
    {
        var changements = new List<string>();
        var anciennes = c.Lignes.Where(l => l.Numero != null).ToDictionary(l => l.Numero!);
        var lignes = new List<LigneCommande>();
        foreach (var t in tendons)
        {
            var n = ImportTendons.Numero(t.Numero)!;
            var neuf = NouvellesLignes([t])[0];
            if (!anciennes.Remove(n, out var l)) { neuf.Ajoutee = true; neuf.ModifieLe = maintenant; lignes.Add(neuf); changements.Add($"Tendon {n} ajouté ({Libelle(neuf.Torons, neuf.Sens, neuf.Longueur, neuf.Ancrages)})."); continue; }
            var diff = new List<string>();
            if (l.Torons != neuf.Torons) diff.Add($"système S{l.Torons} → S{neuf.Torons}");
            if (l.Sens != neuf.Sens) diff.Add($"orientation {l.Sens} → {neuf.Sens}");
            if (l.Longueur != neuf.Longueur) diff.Add($"longueur {M(l.Longueur)} → {M(neuf.Longueur)}");
            if (l.Ancrages != neuf.AncragesSaisis) diff.Add($"ancrages {l.Ancrages} → {neuf.AncragesSaisis}");
            if (diff.Count == 0) { lignes.Add(l); continue; }
            neuf.Ajoutee = l.Ajoutee; neuf.ModifieLe = maintenant; neuf.Avant = l.Avant ?? Libelle(l.Torons, l.Sens, l.Longueur, l.Ancrages);
            lignes.Add(neuf);
            changements.Add($"Tendon {n} : {string.Join(", ", diff)}.");
        }
        foreach (var (n, l) in anciennes) changements.Add($"Tendon {n} retiré ({Libelle(l.Torons, l.Sens, l.Longueur, l.Ancrages)}).");

        foreach (var a in accessoires)
        {
            var avant = c.Accessoires.FirstOrDefault(x => x.Libelle == a.Libelle);
            if (avant is null) changements.Add($"Accessoire {a.Libelle} ajouté : {a.Quantite}.");
            else if (avant.Quantite != a.Quantite) changements.Add($"Accessoire {a.Libelle} : quantité {avant.Quantite} → {a.Quantite}.");
        }
        foreach (var a in c.Accessoires.Where(x => accessoires.All(y => y.Libelle != x.Libelle))) changements.Add($"Accessoire {a.Libelle} retiré.");
        if (surface != c.Surface) changements.Add($"Surface {(c.Surface is null ? "non saisie" : c.Surface.Value.ToString("0.##", Fr) + " m²")} → {(surface is null ? "non saisie" : surface.Value.ToString("0.##", Fr) + " m²")}.");

        c.Lignes = lignes.OrderBy(l => l.Numero, StringComparer.Ordinal).ToList();
        c.Accessoires = accessoires;
        c.Surface = surface;
        c.NbCables = c.Lignes.Count;
        return changements;
    }

    /// <summary>Contrôles communs à la création d'un complément et à la modification d'une commande soumise.</summary>
    private static async Task<(List<TendonInput>? Tendons, List<AccessoireCommande>? Accessoires, decimal? Surface, IResult? Refus)> Lire(
        ProjetsDbContext db, Guid dalleId, CommandeUsine? existante, CommandeInput input)
    {
        var tendons = input.Tendons ?? [];
        if (tendons.Count == 0) return (null, null, null, ProjetsEndpoints.Conflit("Une commande compte au moins un tendon."));
        if (ImportTendons.Refus(tendons, existante?.Lignes.Select(l => l.Torons).ToHashSet()) is { } refus) return (null, null, null, ProjetsEndpoints.Conflit(refus));
        if (await Doublon(db, dalleId, existante?.Id, tendons) is { } doublon) return (null, null, null, ProjetsEndpoints.Conflit(doublon));
        var saisis = input.Accessoires ?? [];
        if (saisis.Any(a => a.Quantite < 0)) return (null, null, null, ProjetsEndpoints.Conflit("La quantité d'un accessoire ne peut pas être négative."));
        if (input.Surface is <= 0) return (null, null, null, ProjetsEndpoints.Conflit("La surface d'une commande est supérieure à 0."));
        return (tendons, await Accessoires(db, saisis), input.Surface is null ? null : Math.Round(input.Surface.Value, 2), null);
    }

    /// <summary>Change l'état d'une commande. Commun à la logistique et au menu Chantier : seuls les rôles de chaque action la déclenchent.</summary>
    public static async Task<IResult> ChangerEtat(ProjetsDbContext db, CommandeUsine c, bool validee, ChangementEtatInput input, ClaimsPrincipal user)
    {
        var courant = c.Etat(validee);
        if (courant == CommandeUsine.Annulee_) return ProjetsEndpoints.Conflit("Cette commande est annulée.");
        var maintenant = DateTime.UtcNow;
        switch (input.Etat)
        {
            case CommandeUsine.Annulee_:
                if (!PeutAnnuler(user)) return Results.Forbid();
                if (string.IsNullOrWhiteSpace(input.Motif)) return ProjetsEndpoints.Conflit("Le motif d'annulation est obligatoire.");
                c.AnnuleeLe = maintenant; c.AnnuleePar = user.UserName(); c.MotifAnnulation = input.Motif.Trim();
                break;
            case CommandeUsine.Preparee:
                if (!PeutPreparer(user)) return Results.Forbid();
                if (courant != CommandeUsine.APreparer) return ProjetsEndpoints.Conflit(courant == CommandeUsine.Attente ? "La commande attend encore ses deux validations." : "Seule une commande à préparer peut être déclarée préparée.");
                c.FabricationLe = maintenant;
                break;
            case CommandeUsine.Envoyee:
                if (!PeutPreparer(user)) return Results.Forbid();
                if (courant != CommandeUsine.Preparee) return ProjetsEndpoints.Conflit("Seule une commande préparée peut être envoyée au chantier.");
                c.LivreeLe = maintenant;
                break;
            case CommandeUsine.Recue:
                if (!PeutRecevoir(user)) return Results.Forbid();
                if (courant != CommandeUsine.Envoyee) return ProjetsEndpoints.Conflit("Seule une commande envoyée au chantier peut être déclarée reçue.");
                c.RecueLe = maintenant;
                break;
            case CommandeUsine.APreparer:
                // Retour en arrière : réservé à ceux qui peuvent annuler.
                if (!PeutAnnuler(user)) return Results.Forbid();
                if (courant == CommandeUsine.Attente || courant == CommandeUsine.APreparer) return ProjetsEndpoints.Conflit("La commande est déjà à préparer.");
                c.FabricationLe = c.LivreeLe = c.RecueLe = null;
                break;
            default:
                return ProjetsEndpoints.Conflit("État de commande inconnu.");
        }
        c.SuiviPar = user.UserName();
        c.UpdatedAt = maintenant;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/projets/{id:guid}/dalles/{did:guid}/commandes").WithTags("Suivi de chantier").RequireAuthorization();

        // Liste des commandes de la dalle, avec le rapprochement des surfaces (dalle contre commandes).
        g.MapGet("/", async (Guid id, Guid did, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var (projet, dalle) = await CommandeEndpoints.Charger(db, id, did);
            if (projet is null || dalle is null) return Results.NotFound();
            var etape = await db.Etapes.AsNoTracking().FirstOrDefaultAsync(e => e.DalleId == did && e.Numero == Etapes.CommandeUsine);
            var validee = etape?.Validee ?? false;
            var commandes = await db.CommandesUsine.AsNoTracking().Where(c => c.DalleId == did).OrderBy(c => c.Numero).ToListAsync();
            var actives = commandes.Where(c => c.AnnuleeLe == null).ToList();
            var surfaceCommandes = actives.Sum(c => c.Surface ?? 0);
            return Results.Ok(new
            {
                commandes = commandes.Select(c => Resume(c, validee)),
                surfaceDalle = dalle.Surface, surfaceCommandes, typesAncrage = SystemesAncrage.Torons,
                proposes = (await db.Accessoires.AsNoTracking().Where(a => a.Actif).OrderBy(a => a.Ordre).ToListAsync()).Select(a => new { libelle = a.Libelle, unite = a.Unite }),
                ecartSurface = actives.Count > 0 && Math.Round(surfaceCommandes, 2) != Math.Round(dalle.Surface, 2),
                soumise = await Soumise(db, did),
                peutCompleter = await Soumise(db, did) && Blocage(projet) is null && CommandeEndpoints.PeutSaisir(user),
            });
        });

        // Lecture d'un fichier de tendons pour un complément ou une modification : rien n'est enregistré.
        g.MapPost("/import", async (Guid id, Guid did, IFormFile fichier, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var (projet, dalle) = await CommandeEndpoints.Charger(db, id, did);
            if (projet is null || dalle is null) return Results.NotFound();
            if (!CommandeEndpoints.PeutSaisir(user)) return Results.Forbid();
            if (Blocage(projet) is { } b) return ProjetsEndpoints.Conflit(b);
            if (fichier.Length == 0) return ProjetsEndpoints.Conflit("Le fichier est vide.");
            if (fichier.Length > 5 * 1024 * 1024) return ProjetsEndpoints.Conflit("Le fichier dépasse 5 Mo.");
            using var flux = new MemoryStream();
            await fichier.CopyToAsync(flux);
            var (tendons, erreurs, refus) = ImportTendons.Lire(fichier.FileName, flux.ToArray());
            if (refus is not null) return ProjetsEndpoints.Conflit(refus);
            return Results.Ok(new
            {
                tendons = tendons.Select(t => new { numero = t.Numero, torons = t.Torons, sens = t.Sens, longueur = t.Longueur, ancrages = t.Ancrages }),
                erreurs,
            });
        }).DisableAntiforgery();

        // Aperçu d'une commande tirée des plans de câblage DXF déposés par le dessinateur dans le dernier dossier de plans. Rien n'est enregistré.
        g.MapGet("/cablage/apercu", async (Guid id, Guid did, decimal? surlongueur, ProjetsDbContext db, IFichiersContenu fichiers, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var (projet, dalle) = await CommandeEndpoints.Charger(db, id, did);
            if (projet is null || dalle is null) return Results.NotFound();
            if (!CommandeEndpoints.PeutSaisir(user)) return Results.Forbid();
            var version = await db.VersionsPlans.AsNoTracking().Where(v => v.DalleId == did).OrderByDescending(v => v.Lettre).FirstOrDefaultAsync(ct);
            if (version is null) return ProjetsEndpoints.Conflit("Aucun dossier de plans pour cette dalle : le dessinateur doit y déposer les plans de câblage DXF.");

            var plans = new List<PlanCablage>();
            foreach (var (categorie, sens) in new[] { (VersionPlan.CablageX, "X"), (VersionPlan.CablageY, "Y") })
            {
                var dernier = (await fichiers.ListerAsync(VersionPlan.Contexte, version.Id, categorie, ct)).FirstOrDefault();
                if (dernier is null) continue;
                if (await fichiers.LireAsync(VersionPlan.Contexte, dernier.Id, ct) is not { } lu) continue;
                using (lu.Contenu) plans.Add(LectureCablageAdapt.Lire(lu.Contenu, dernier.Nom, sens));
            }
            var a = ApercuCablage.Composer(plans, dalle.Adapt?.Systemes, surlongueur ?? 0, dalle.Surface);
            return Results.Ok(new
            {
                version = version.Lettre, importable = a.Importable, erreurs = a.Erreurs, avertissements = a.Avertissements,
                plans = plans.Select(p => new { nom = p.Nom, sens = p.Orientation, cables = p.Cables.Count, reference = p.Reference, niveau = p.Niveau, surface = p.SurfaceNette }),
                lignes = a.Lignes.Select(l => new { numero = l.Numero, torons = l.Torons, sens = l.Sens, tracee = l.Tracee, actives = l.Actives, surlongueur = l.Surlongueur, longueur = l.Longueur, ancrages = l.Ancrages }),
                recap = a.Recap.Select(r => new { sens = r.Sens, torons = r.Torons, cables = r.Cables, longueur = r.Longueur }),
                surfaceDxf = a.SurfaceDxf, surfaceDalle = dalle.Surface, surfaceDifferente = a.SurfaceDifferente,
            });
        });

        // Aligne la surface de la dalle sur celle des plans de câblage, après confirmation de l'utilisateur.
        g.MapPut("/surface-dalle", async (Guid id, Guid did, SurfaceDalleInput input, ProjetsDbContext db, IDevisLookup devis, ClaimsPrincipal user) =>
        {
            var (projet, dalle) = await CommandeEndpoints.Charger(db, id, did);
            if (projet is null || dalle is null) return Results.NotFound();
            if (!CommandeEndpoints.PeutSaisir(user)) return Results.Forbid();
            if (Blocage(projet) is { } b) return ProjetsEndpoints.Conflit(b);
            var surface = Math.Round(input.Surface, 2);
            if (surface <= 0 || surface > 1_000_000) return ProjetsEndpoints.Conflit("La surface doit être un nombre positif.");
            if (surface == dalle.Surface) return Results.Ok(new { surface });
            if (await devis.DevisSurAutreSurfaceAsync(did, surface) is { } engage)
                return ProjetsEndpoints.Conflit($"La surface est chiffrée dans le devis {engage} : créez d'abord une nouvelle version de ce devis, puis modifiez la surface.");
            var d = await db.Dalles.FirstAsync(x => x.Id == did);
            d.Surface = surface; d.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { surface });
        });

        g.MapGet("/{cid:guid}", async (Guid id, Guid did, Guid cid, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var (projet, dalle) = await CommandeEndpoints.Charger(db, id, did);
            if (projet is null || dalle is null) return Results.NotFound();
            var c = await db.CommandesUsine.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cid && x.DalleId == did);
            if (c is null) return Results.NotFound();
            var etape = await db.Etapes.AsNoTracking().FirstOrDefaultAsync(e => e.DalleId == did && e.Numero == Etapes.CommandeUsine);
            var etat = c.Etat(etape?.Validee ?? false);
            var soumise = etape?.FaitLe != null;
            var modifiable = soumise && Blocage(projet) is null && CommandeEndpoints.PeutSaisir(user) && etat is not (CommandeUsine.Annulee_ or CommandeUsine.Recue);
            var proposes = modifiable
                ? (await db.Accessoires.AsNoTracking().Where(a => a.Actif).OrderBy(a => a.Ordre).ToListAsync()).Select(a => (object)new { libelle = a.Libelle, unite = a.Unite })
                : [];
            return Results.Ok(new
            {
                commande = Detail(c, etape?.Validee ?? false), modifiable, typesAncrage = SystemesAncrage.Torons, proposes, surfaceDalle = dalle.Surface,
                peutPreparer = PeutPreparer(user), peutRecevoir = PeutRecevoir(user), peutAnnuler = PeutAnnuler(user),
            });
        });

        // Complément : une partie de la commande envoyée plus tard au chantier. Possible dès que la commande initiale est soumise.
        g.MapPost("/", async (Guid id, Guid did, CommandeInput input, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var (projet, dalle) = await CommandeEndpoints.Charger(db, id, did);
            if (projet is null || dalle is null) return Results.NotFound();
            if (!CommandeEndpoints.PeutSaisir(user)) return Results.Forbid();
            if (Blocage(projet) is { } b) return ProjetsEndpoints.Conflit(b);
            if (!await Soumise(db, did)) return ProjetsEndpoints.Conflit("La commande initiale n'est pas encore soumise : complétez-la directement.");
            var (tendons, accessoires, surface, refus) = await Lire(db, did, null, input);
            if (refus is not null) return refus;
            var numero = (await db.CommandesUsine.Where(c => c.DalleId == did).MaxAsync(c => (int?)c.Numero) ?? 0) + 1;
            var c = new CommandeUsine { DalleId = did, Numero = numero, Surface = surface, Lignes = NouvellesLignes(tendons!), Accessoires = accessoires! };
            c.NbCables = c.Lignes.Count;
            c.Modifications.Add(new ModificationCommande { Le = DateTime.UtcNow, Par = user.UserName(), Etat = CommandeUsine.APreparer, Changements = [$"Commande complémentaire n° {numero} créée : {c.Lignes.Count} tendon(s)."] });
            db.CommandesUsine.Add(c);
            await db.SaveChangesAsync();
            return Results.Created($"/api/projets/{id}/dalles/{did}/commandes/{c.Id}", Resume(c, true));
        });

        // Modification d'une commande déjà soumise, envoyée comprise : tracée et signalée à l'usine.
        g.MapPut("/{cid:guid}", async (Guid id, Guid did, Guid cid, CommandeInput input, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var (projet, dalle) = await CommandeEndpoints.Charger(db, id, did);
            if (projet is null || dalle is null) return Results.NotFound();
            if (!CommandeEndpoints.PeutSaisir(user)) return Results.Forbid();
            if (Blocage(projet) is { } b) return ProjetsEndpoints.Conflit(b);
            var c = await db.CommandesUsine.FirstOrDefaultAsync(x => x.Id == cid && x.DalleId == did);
            if (c is null) return Results.NotFound();
            var etape = await db.Etapes.AsNoTracking().FirstOrDefaultAsync(e => e.DalleId == did && e.Numero == Etapes.CommandeUsine);
            if (etape?.FaitLe is null) return ProjetsEndpoints.Conflit("La commande n'est pas encore soumise : modifiez-la depuis l'étape Commande usine.");
            var etat = c.Etat(etape.Validee);
            if (etat == CommandeUsine.Annulee_) return ProjetsEndpoints.Conflit("Cette commande est annulée.");
            if (etat == CommandeUsine.Recue) return ProjetsEndpoints.Conflit("Cette commande est reçue au chantier : créez une commande complémentaire.");
            if (c.Numero == 1 && input.Surface is not > 0) return ProjetsEndpoints.Conflit("Saisissez la surface totale de la commande (m²) : elle est reprise sur le bon de livraison et la facture.");
            var (tendons, accessoires, surface, refus) = await Lire(db, did, c, input);
            if (refus is not null) return refus;
            var changements = Appliquer(c, tendons!, accessoires!, surface, DateTime.UtcNow);
            if (changements.Count > 0)
            {
                c.Modifications.Add(new ModificationCommande { Le = DateTime.UtcNow, Par = user.UserName(), Etat = etat, Changements = changements });
                c.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            return Results.Ok(new { commande = Detail(c, etape.Validee), changements = changements.Count });
        });

        // Préparée, envoyée au chantier, reçue au chantier, annulée : accessible au menu Chantier pour la réception.
        g.MapPost("/{cid:guid}/etat", async (Guid id, Guid did, Guid cid, ChangementEtatInput input, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var (projet, dalle) = await CommandeEndpoints.Charger(db, id, did);
            if (projet is null || dalle is null) return Results.NotFound();
            if (Blocage(projet) is { } b) return ProjetsEndpoints.Conflit(b);
            var c = await db.CommandesUsine.FirstOrDefaultAsync(x => x.Id == cid && x.DalleId == did);
            var etape = await db.Etapes.AsNoTracking().FirstOrDefaultAsync(e => e.DalleId == did && e.Numero == Etapes.CommandeUsine);
            if (c is null || etape is null) return Results.NotFound();
            return await ChangerEtat(db, c, etape.Validee, input, user);
        });
    }
}
