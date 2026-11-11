using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

/// <summary>
/// Suivi des dalles vu du chantier : le responsable des chantiers affecte les chefs de chantier aux projets,
/// un chef de chantier ne voit que les dalles de ses chantiers et y réalise les étapes qui lui reviennent.
/// </summary>
public static class ChantierDalle
{
    public const string AAffecter = "a_affecter";
    public const string Preparation = "preparation";
    public const string ARealiser = "a_realiser";
    public const string Commande = "commande";
    public const string Terminee = "terminee";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [AAffecter] = "À affecter", [Preparation] = "Avant chantier", [ARealiser] = "À réaliser", [Commande] = "Commande usine", [Terminee] = "Terminée",
    };

    /// <summary>Ordre des états dans les filtres de la liste.</summary>
    public static readonly string[] Etats = [AAffecter, Preparation, ARealiser, Commande, Terminee];

    /// <summary>Rôles qui voient le menu Chantier.</summary>
    public static readonly string[] RolesVue = [Roles.Directeur, Roles.ChefProjet, Roles.ResponsableChantier, Roles.ChefChantier];

    /// <summary>Rôles qui voient toutes les dalles et affectent les chefs de chantier aux projets.</summary>
    public static readonly string[] RolesAffectation = [Roles.Directeur, Roles.ChefProjet, Roles.ResponsableChantier];

    /// <summary>Rôles que l'on peut affecter à un chantier.</summary>
    public static readonly string[] RolesChef = [Roles.ChefChantier];

    /// <summary>
    /// État d'une dalle pour le chantier, d'après ses étapes faites et les chefs de chantier de son projet :
    /// à affecter tant que le projet n'a pas de chef de chantier, avant chantier jusqu'à la validation du BCT,
    /// commande usine pendant l'étape 4 (elle revient à la logistique), à réaliser pour les autres étapes, terminée après la onzième.
    /// </summary>
    public static string Etat(int etapesFaites, int chefs) =>
        etapesFaites >= Etapes.Total ? Terminee
        : chefs == 0 ? AAffecter
        : etapesFaites < Etapes.ValidationBct ? Preparation
        : etapesFaites + 1 == Etapes.CommandeUsine ? Commande
        : ARealiser;

    /// <summary>Ce qui retient l'étape en cours, ou null si le chantier peut la réaliser.</summary>
    public static string? Attente(bool projetBloque, bool devisValide, Etapes.Definition etape, bool realisee, bool validee) =>
        projetBloque ? "Projet bloqué : les étapes sont suspendues."
        : !devisValide ? "Le suivi commence après la validation d'un devis."
        : etape.ValidationAvant && !validee ? "Validations à obtenir avant la saisie."
        : realisee && etape.DoubleValidation && !validee ? "Réalisée, en attente de validation."
        : null;

    /// <summary>Motif de refus d'accès à un chantier, ou null : un chef de chantier n'accède qu'aux projets où il est affecté.</summary>
    public static string? RefusAcces(bool responsable, bool affecte) =>
        responsable || affecte ? null : "Vous n'êtes pas affecté à ce chantier.";
}
