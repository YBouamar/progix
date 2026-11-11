using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

/// <summary>
/// Exécution d'une dalle par les dessinateurs : le dessinateur en chef affecte un ou plusieurs dessinateurs à la dalle,
/// chaque version des plans d'exécution est un dossier (la première un dossier EXE, les suivantes des MODIF),
/// suivi jour par jour jusqu'à sa clôture et son envoi.
/// </summary>
public static class ExecutionDalle
{
    public const string Exe = "exe";
    public const string Modif = "modif";

    public static readonly IReadOnlyDictionary<string, string> Types = new Dictionary<string, string> { [Exe] = "EXE", [Modif] = "MODIF" };

    // États d'un dossier.
    public const string EnCours = "en_cours";
    public const string Termine = "termine";
    public const string Arrete = "arrete";

    // États d'une dalle sans dossier ouvert.
    public const string AAffecter = "a_affecter";
    public const string ALancer = "a_lancer";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [AAffecter] = "À affecter", [ALancer] = "À lancer", [EnCours] = "En cours", [Termine] = "Terminé", [Arrete] = "Arrêté",
    };

    /// <summary>Ordre des états dans les filtres de la liste.</summary>
    public static readonly string[] Etats = [AAffecter, ALancer, EnCours, Termine, Arrete];

    /// <summary>Rôles qui voient le menu Exécution.</summary>
    public static readonly string[] RolesVue = [Roles.Directeur, Roles.ChefProjet, Roles.DessinateurChef, Roles.Dessinateur];

    /// <summary>Rôles qui affectent les dessinateurs, planifient, arrêtent et rouvrent un dossier.</summary>
    public static readonly string[] RolesAffectation = [Roles.Directeur, Roles.ChefProjet, Roles.DessinateurChef];

    /// <summary>Rôles que l'on peut affecter à l'exécution d'une dalle.</summary>
    public static readonly string[] RolesDessinateur = [Roles.Dessinateur, Roles.DessinateurChef];

    /// <summary>Le premier dossier d'une dalle est un EXE, chaque nouvelle version des plans une MODIF.</summary>
    public static string Type(bool premier) => premier ? Exe : Modif;

    /// <summary>État de l'exécution d'une dalle : celui de son dernier dossier, sinon à affecter ou à lancer.</summary>
    public static string Etat(string? dernierDossier, int dessinateurs) =>
        dernierDossier ?? (dessinateurs > 0 ? ALancer : AAffecter);

    /// <summary>Motif de refus de l'ouverture d'un dossier, ou null : le dossier précédent doit être terminé ou arrêté.</summary>
    public static string? RefusOuvrir(string? dernierDossier, char? derniereLettre, bool agit) =>
        !agit ? "Seuls les dessinateurs affectés à cette dalle ouvrent un dossier."
        : dernierDossier == EnCours ? $"Le dossier {derniereLettre} est encore en cours : terminez-le avant d'ouvrir une modification."
        : null;

    /// <summary>
    /// Motif de refus de la clôture d'un dossier, ou null. La surface et les dessinateurs sont relevés à la clôture :
    /// ils servent au suivi par jour et à la performance.
    /// </summary>
    public static string? RefusTerminer(string etat, bool agit, int dessinateurs, decimal surface, string? manque) =>
        etat == Termine ? "Ce dossier est déjà terminé."
        : etat == Arrete ? "Ce dossier est arrêté : reprenez-le avant de le terminer."
        : !agit ? "Seuls les dessinateurs affectés à cette dalle terminent un dossier."
        : dessinateurs == 0 ? "Affectez au moins un dessinateur à la dalle avant de terminer le dossier."
        : surface <= 0 ? "Renseignez la surface de la dalle avant de terminer le dossier."
        : manque;

    /// <summary>Part d'un dessinateur dans un dossier partagé : le dossier et sa surface se divisent à parts égales.</summary>
    public static decimal Part(int dessinateurs) => dessinateurs <= 0 ? 0 : 1m / dessinateurs;

    /// <summary>Identifiants relevés à la clôture, séparés par des virgules.</summary>
    public static IReadOnlyList<Guid> Ids(string? releve) =>
        string.IsNullOrWhiteSpace(releve) ? [] : releve.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList();
}

/// <summary>Affectation d'un dessinateur à l'exécution d'une dalle. Le retrait renseigne la date de fin et garde l'historique.</summary>
public class AffectationExecution : Entity
{
    public Guid DalleId { get; set; }
    public Guid UserId { get; set; }
    public string UserNom { get; set; } = "";
    public string AffectePar { get; set; } = "";
    public DateOnly Debut { get; set; }
    public DateOnly? Fin { get; set; }
    public string? RetirePar { get; set; }
}
