using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

/// <summary>
/// Étude d'une dalle par le bureau d'études : chaque dalle d'un projet a son étude, dès sa création.
/// L'étude est à affecter tant qu'aucun ingénieur ne la porte, en cours ensuite, terminée quand un ingénieur affecté la déclare terminée.
/// </summary>
public static class EtudeDalle
{
    /// <summary>Contexte de dépôt des pièces de l'étude, rattachées à la dalle.</summary>
    public const string Contexte = "etude_dalle";

    public const string Coffrage = "coffrage";
    public const string FondPlan = "fond_plan";
    public const string Adapt = "adapt";
    public const string Principe = "principe";

    /// <summary>Pièces de l'étude, dans l'ordre du travail : plan reçu, fond de plan, sortie du calcul, plan de principe.</summary>
    public static readonly (string Code, string Label)[] Pieces =
    [
        (Coffrage, "Plan de coffrage"),
        (FondPlan, "Fond de plan"),
        (Adapt, "Sortie ADAPT"),
        (Principe, "Plan de principe"),
    ];

    public const string AAffecter = "a_affecter";
    public const string EnCours = "en_cours";
    public const string Terminee = "terminee";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [AAffecter] = "À affecter", [EnCours] = "En cours", [Terminee] = "Terminée",
    };

    /// <summary>Rôles qui voient le menu Étude.</summary>
    public static readonly string[] RolesVue = [Roles.Directeur, Roles.ChefProjet, Roles.IngenieurChef, Roles.Ingenieur];

    /// <summary>Rôles qui affectent les ingénieurs aux études et rouvrent une étude terminée.</summary>
    public static readonly string[] RolesAffectation = [Roles.Directeur, Roles.ChefProjet, Roles.IngenieurChef];

    /// <summary>Rôles que l'on peut affecter à une étude.</summary>
    public static readonly string[] RolesIngenieur = [Roles.Ingenieur, Roles.IngenieurChef];

    public static string Etat(bool terminee, int ingenieurs) => terminee ? Terminee : ingenieurs > 0 ? EnCours : AAffecter;

    /// <summary>Motif de refus de la clôture, ou null : seul un ingénieur affecté à l'étude la déclare terminée.</summary>
    public static string? RefusTerminer(bool terminee, bool affecte) =>
        terminee ? "Cette étude est déjà terminée."
        : !affecte ? "Seul un ingénieur affecté à cette étude peut la déclarer terminée."
        : null;

    /// <summary>Motif de refus d'un changement d'ingénieurs, ou null.</summary>
    public static string? RefusAffecter(bool terminee) =>
        terminee ? "Cette étude est terminée : rouvrez-la pour changer ses ingénieurs." : null;

    /// <summary>Motif de refus d'un dépôt de pièce pour une personne, ou null : les responsables, sinon les ingénieurs affectés.</summary>
    public static string? RefusDeposer(bool responsable, bool affecte) =>
        responsable || affecte ? null : "Seuls les ingénieurs affectés à cette étude y déposent des pièces.";
}

/// <summary>Affectation d'un ingénieur à l'étude d'une dalle. Le retrait renseigne la date de fin et garde l'historique.</summary>
public class AffectationEtude : Entity
{
    public Guid DalleId { get; set; }
    public Guid UserId { get; set; }
    public string UserNom { get; set; } = "";
    public string AffectePar { get; set; } = "";
    public DateOnly Debut { get; set; }
    public DateOnly? Fin { get; set; }
    public string? RetirePar { get; set; }
}
