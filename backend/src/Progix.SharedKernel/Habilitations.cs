using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Progix.SharedKernel;

/// <summary>
/// Habilitations : pour chaque fonction, un niveau d'accès par module (aucun, lecture, écriture), réglé depuis l'écran de paramétrage.
/// Les douze fonctions d'origine gardent leurs règles de validation et de signature ; une fonction créée hérite de celles de sa fonction
/// de référence. Le niveau réglé s'ajoute à ces règles : il ouvre ou ferme un module, il ne déplace pas une validation.
/// </summary>
public static class Habilitations
{
    public const int Aucun = 0;
    public const int Lecture = 1;
    public const int Ecriture = 2;

    /// <summary>Revendication portant le code de la fonction de la personne (une fonction créée, ou l'une des douze d'origine).</summary>
    public const string ClaimFonction = "fonction";

    public const string Projets = "projets";
    public const string Devis = "devis";
    public const string Clients = "clients";
    public const string Livraisons = "livraisons";
    public const string Facturation = "facturation";
    public const string Tresorerie = "tresorerie";
    public const string Etude = "etude";
    public const string Execution = "execution";
    public const string Chantier = "chantier";
    public const string Logistique = "logistique";
    public const string RepartitionIngenieurs = "repartition_ingenieurs";
    public const string Performance = "performance";
    public const string Niveaux = "niveaux";
    public const string Parametrage = "parametrage";

    /// <param name="LectureSeule">Module de consultation : il n'a pas de niveau « écriture ».</param>
    /// <param name="Accordable">
    /// Vrai : le niveau réglé suffit à donner l'accès, quelle que soit la fonction de référence.
    /// Faux : le module contient des actions liées aux fonctions d'origine ; le niveau réglé ne peut que restreindre celui de la fonction de référence.
    /// </param>
    /// <param name="Ecrivent">Fonctions d'origine en écriture par défaut.</param>
    /// <param name="Lisent">Fonctions d'origine en lecture par défaut.</param>
    /// <param name="Rouvrable">Fonctions d'origine fermées par défaut sur ce module, mais auxquelles il peut être rouvert jusqu'à son niveau le plus haut.</param>
    public sealed record Module(string Code, string Label, string Aide, bool LectureSeule, bool Accordable, string[] Ecrivent, string[] Lisent, string[]? Rouvrable = null)
    {
        public int Maximum => LectureSeule ? Lecture : Ecriture;
    }

    private static readonly string[] Tous = Roles.All.Select(r => r.Code).ToArray();
    private static readonly string[] Direction = [Roles.Directeur, Roles.ChefProjet];
    // Les chantiers passent par leur menu : le menu Projets leur est fermé par défaut, et peut leur être rouvert.
    private static readonly string[] Chantiers = [Roles.ResponsableChantier, Roles.ChefChantier];

    /// <summary>Modules réglables, dans l'ordre du menu. Les niveaux par défaut reproduisent les droits d'avant les habilitations.</summary>
    public static readonly IReadOnlyList<Module> Modules =
    [
        new(Projets, "Projets", "Fiches de projet, dalles et suivi du chantier. Qui crée un projet, valide une étape ou signe reste fixé par la fonction de référence.", false, false, Tous.Except(Chantiers).ToArray(), [], Chantiers),
        new(Devis, "Devis", "Devis, leurs versions et leurs imports.", false, true, Policies.RolesDevis, []),
        new(Clients, "Clients et partenaires", "Fiches des clients et des partenaires.", false, true, Policies.RolesCommercial, []),
        new(Livraisons, "Livraisons", "Bons de livraison.", false, true, Policies.RolesCommercial, []),
        new(Facturation, "Facturation", "Factures, avoirs et encaissements.", false, true, Policies.RolesCommercial, []),
        new(Tresorerie, "Trésorerie", "Encaissements, dépenses et marge, avec leurs exports.", true, true, [], Policies.RolesCommercial),
        new(Etude, "Étude", "Études des dalles : ingénieurs, pièces, fond de plan. L'affectation des ingénieurs reste aux responsables.", false, false, [Roles.Directeur, Roles.ChefProjet, Roles.IngenieurChef, Roles.Ingenieur], []),
        new(Execution, "Exécution", "Exécution des dalles : dessinateurs, dossiers EXE et MODIF, suivi par jour. L'affectation des dessinateurs reste aux responsables.", false, false, [Roles.Directeur, Roles.ChefProjet, Roles.DessinateurChef, Roles.Dessinateur], []),
        new(Chantier, "Chantier", "Suivi des dalles sur le terrain : étapes, fiches, photos et dépenses de chantier. Un chef de chantier n'y voit que les projets où il est affecté ; l'affectation reste aux responsables.", false, false, [.. Direction, .. Chantiers], []),
        new(Logistique, "Logistique", "Commandes usine et stock.", false, true, Policies.RolesLogistique, []),
        new(RepartitionIngenieurs, "Répartition des ingénieurs", "Charge des ingénieurs par projet.", true, false, [], [.. Direction, Roles.IngenieurChef]),
        new(Performance, "Performance", "Graphes de performance.", true, false, [], [.. Direction, Roles.IngenieurChef, Roles.DessinateurChef]),
        new(Niveaux, "Niveaux des dalles", "Liste des niveaux proposés à la création d'une dalle.", false, false, Direction, []),
        new(Parametrage, "Paramétrage", "Réglages de l'application. Les utilisateurs et les habilitations restent au directeur et au chef de projet.", false, false, [.. Direction, Roles.ResponsableLogistique], []),
    ];

    public static Module? Trouver(string code) => Modules.FirstOrDefault(m => m.Code == code);

    /// <summary>Niveau d'une fonction d'origine sur un module, avant tout réglage.</summary>
    public static int ParDefaut(string role, string module) =>
        Trouver(module) is not { } m ? Aucun : m.Ecrivent.Contains(role) ? m.Maximum : m.Lisent.Contains(role) ? Lecture : Aucun;

    /// <summary>Niveau le plus haut qu'on peut régler pour une fonction dont la référence est <paramref name="roleDeReference"/>.</summary>
    public static int Plafond(string roleDeReference, string module) =>
        Trouver(module) is not { } m ? Aucun
        : m.Accordable || m.Rouvrable?.Contains(roleDeReference) == true ? m.Maximum
        : ParDefaut(roleDeReference, module);

    /// <summary>
    /// Niveau effectif : le directeur garde tout, pour ne jamais se retrouver enfermé dehors ; sinon le niveau réglé,
    /// ramené au plafond de la fonction de référence, ou à défaut le niveau d'origine de cette fonction.
    /// </summary>
    public static int Niveau(string roleDeReference, string module, int? regle) =>
        roleDeReference == Roles.Directeur ? Trouver(module)?.Maximum ?? Aucun
        : regle is { } n ? Math.Clamp(n, Aucun, Plafond(roleDeReference, module))
        : ParDefaut(roleDeReference, module);

    private enum Mode { Normal, EcrituresSeulement }

    // Du plus précis au plus général. Les familles absentes (authentification, fichiers, tableau de bord, documents signés par
    // identifiant) ne dépendent pas d'un module : leurs propres règles s'appliquent.
    private static readonly (Regex Chemin, string Module, Mode Mode)[] Regles =
    [
        (new(@"^/api/devis(/|$)", RegexOptions.Compiled), Devis, Mode.Normal),
        (new(@"^/api/partenaires(/|$)", RegexOptions.Compiled), Clients, Mode.Normal),
        (new(@"^/api/livraisons(/|$)", RegexOptions.Compiled), Livraisons, Mode.Normal),
        (new(@"^/api/(factures|avoirs|encaissements)(/|$)", RegexOptions.Compiled), Facturation, Mode.Normal),
        (new(@"^/api/tresorerie(/|$)", RegexOptions.Compiled), Tresorerie, Mode.Normal),
        (new(@"^/api/etudes(/|$)", RegexOptions.Compiled), Etude, Mode.Normal),
        (new(@"^/api/execution(/|$)", RegexOptions.Compiled), Execution, Mode.Normal),
        (new(@"^/api/chantier(/|$)", RegexOptions.Compiled), Chantier, Mode.Normal),
        (new(@"^/api/logistique(/|$)", RegexOptions.Compiled), Logistique, Mode.Normal),
        (new(@"^/api/repartition/ingenieurs(/|$)", RegexOptions.Compiled), RepartitionIngenieurs, Mode.Normal),
        (new(@"^/api/statistiques/performance(/|$)", RegexOptions.Compiled), Performance, Mode.Normal),
        // Les listes de paramètres se lisent partout (formulaires) : seule leur modification dépend du module.
        (new(@"^/api/parametres/niveaux(/|$)", RegexOptions.Compiled), Niveaux, Mode.EcrituresSeulement),
        (new(@"^/api/parametres(/|$)", RegexOptions.Compiled), Parametrage, Mode.EcrituresSeulement),
        (new(@"^/api/projets(/|$)", RegexOptions.Compiled), Projets, Mode.Normal),
    ];

    /// <summary>Module et niveau exigés par une requête, ou null si la requête ne dépend d'aucun module.</summary>
    public static (string Module, int Requis)? Resoudre(string methode, string chemin)
    {
        var lecture = HttpMethods.IsGet(methode) || HttpMethods.IsHead(methode) || HttpMethods.IsOptions(methode);
        foreach (var (re, module, mode) in Regles)
        {
            if (!re.IsMatch(chemin)) continue;
            if (lecture && mode == Mode.EcrituresSeulement) return null;
            return (module, lecture ? Lecture : Trouver(module)!.Maximum);
        }
        return null;
    }

    // Suivi d'une dalle et dépenses de chantier : ces points d'accès du module Projets servent aussi le menu Chantier.
    private static readonly Regex SuiviDeChantier = new(
        @"^/api/projets/(?<projet>[0-9a-fA-F-]{36})/(dalles/[0-9a-fA-F-]{36}/(etapes|tension|commande|injection|auto-controle|rapports-ecrasement|versions)|depenses)(/|$)", RegexOptions.Compiled);

    /// <summary>
    /// Second module qui ouvre une requête du module Projets : le menu Chantier pour le suivi d'une dalle et les dépenses de chantier.
    /// Renvoie aussi le projet visé, pour vérifier qu'un chef de chantier y est affecté.
    /// </summary>
    public static (string Module, Guid ProjetId)? Alternative(string chemin) =>
        SuiviDeChantier.Match(chemin) is { Success: true } m && Guid.TryParse(m.Groups["projet"].Value, out var id) ? (Chantier, id) : null;

    /// <summary>Message du refus, prêt à afficher.</summary>
    public static string Refus(string module, int niveau) =>
        niveau == Aucun ? $"Votre fonction n'a pas accès au module « {Trouver(module)?.Label ?? module} »."
        : $"Votre fonction a un accès en lecture seule au module « {Trouver(module)?.Label ?? module} ».";

    /// <summary>Contrôle chaque requête authentifiée : le niveau de la fonction sur le module de la requête doit suffire.</summary>
    public static IApplicationBuilder UseHabilitations(this IApplicationBuilder app) => app.Use(async (http, suite) =>
    {
        if (http.User.Identity?.IsAuthenticated == true && Resoudre(http.Request.Method, http.Request.Path) is { } r)
        {
            var habilitations = http.RequestServices.GetRequiredService<IHabilitations>();
            var niveau = await habilitations.NiveauAsync(http.User, r.Module, http.RequestAborted);
            // Le menu Chantier ouvre le suivi des dalles sans le menu Projets ; un chef de chantier seulement sur les projets où il est affecté.
            if (niveau < r.Requis && Alternative(http.Request.Path) is { } autre)
            {
                var niveauAutre = await habilitations.NiveauAsync(http.User, autre.Module, http.RequestAborted);
                var refus = niveauAutre < r.Requis ? (niveauAutre > niveau ? Refus(autre.Module, niveauAutre) : null)
                    : http.User.IsInRole(Roles.ChefChantier)
                      && !await http.RequestServices.GetRequiredService<IProjetsLookup>().EstChefChantierAsync(autre.ProjetId, http.User.UserId(), http.RequestAborted)
                        ? "Vous n'êtes pas affecté à ce chantier." : "";
                if (refus == "")
                {
                    await suite();
                    return;
                }
                if (refus is not null)
                {
                    http.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await http.Response.WriteAsJsonAsync(new { message = refus }, http.RequestAborted);
                    return;
                }
            }
            if (niveau < r.Requis)
            {
                http.Response.StatusCode = StatusCodes.Status403Forbidden;
                await http.Response.WriteAsJsonAsync(new { message = Refus(r.Module, niveau) }, http.RequestAborted);
                return;
            }
        }
        await suite();
    });

    /// <summary>
    /// Politique d'un module accordable : passe pour les fonctions d'origine prévues, ou pour toute fonction dont le niveau réglé
    /// suffit sur le module de la requête. Hors d'un module accordable, seules les fonctions d'origine passent.
    /// </summary>
    public static async Task<bool> AccordeAsync(ClaimsPrincipal user, object? ressource, string[] rolesDorigine)
    {
        if (rolesDorigine.Any(user.IsInRole)) return true;
        if (ressource is not HttpContext http || Resoudre(http.Request.Method, http.Request.Path) is not { } r || Trouver(r.Module) is not { Accordable: true }) return false;
        return await http.RequestServices.GetRequiredService<IHabilitations>().NiveauAsync(user, r.Module, http.RequestAborted) >= r.Requis;
    }
}

/// <summary>Niveau d'accès d'une personne sur un module, d'après sa fonction et les habilitations réglées.</summary>
public interface IHabilitations
{
    Task<int> NiveauAsync(ClaimsPrincipal user, string module, CancellationToken ct = default);
}
