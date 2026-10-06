using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

/// <summary>Les 11 étapes du suivi de chantier d'une dalle, dans l'ordre strict.</summary>
public static class Etapes
{
    /// <param name="ValidationAvant">Vrai quand les deux validations précèdent la saisie (mise en tension, injection) ;
    /// faux quand elles suivent la réalisation (commande usine).</param>
    /// <param name="ValidationChantier">Vrai quand le responsable des chantiers valide aussi, en plus des validations technique et administrative (mise en tension).</param>
    public sealed record Definition(int Numero, string Label, bool DoubleValidation, string[] Roles, bool ValidationAvant = false, bool ValidationChantier = false);

    private static readonly string[] Dessin = [Roles.DessinateurChef, Roles.Dessinateur];
    private static readonly string[] Ingenierie = [Roles.IngenieurChef, Roles.Ingenieur];
    private static readonly string[] Administration = [Roles.ResponsableAdministratif, Roles.Administrateur];
    private static readonly string[] Chantier = [Roles.ResponsableChantier, Roles.ChefChantier];
    private static readonly string[] Logistique = [Roles.ResponsableLogistique];

    public const int Total = 11;
    public const int ValidationBct = 2;
    public const int ReceptionCoffrage = 3;
    public const int CommandeUsine = 4;
    public const int MiseEnTension = 9;
    public const int Injection = 10;

    // Le chef de projet a le contrôle de toutes les étapes, comme le directeur.
    public static readonly IReadOnlyList<Definition> All =
    [
        new(1, "Plans d'exécution", false, Dessin),
        new(2, "Validation BCT", false, [.. Ingenierie, .. Administration]),
        new(3, "Réception du coffrage", false, Chantier),
        new(4, "Commande usine", true, Logistique),
        new(5, "Pose de la précontrainte", false, Chantier),
        new(6, "Réception de dalle", false, Chantier),
        new(7, "Coulage du béton", false, Chantier),
        new(8, "Rapport d'écrasement", false, [.. Chantier, .. Ingenierie]),
        new(9, "Mise en tension", true, Chantier, ValidationAvant: true, ValidationChantier: true),
        new(10, "Injection", true, Chantier, ValidationAvant: true),
        new(11, "Réception finale", false, [.. Chantier, Roles.ResponsableAdministratif]),
    ];

    public static Definition Get(int numero) => All[numero - 1];
}

/// <summary>Réalisation d'une étape pour une dalle.</summary>
public class EtapeDalle : Entity
{
    public Guid DalleId { get; set; }
    public int Numero { get; set; }
    public string? FaitPar { get; set; }
    public DateTime? FaitLe { get; set; }
    public string? Remarque { get; set; }

    // Commande usine, mise en tension et injection : validation technique puis administrative.
    public string? ValidationTechniquePar { get; set; }
    public DateTime? ValidationTechniqueLe { get; set; }
    public string? ValidationAdministrativePar { get; set; }
    public DateTime? ValidationAdministrativeLe { get; set; }

    // Mise en tension : le responsable des chantiers valide aussi.
    public string? ValidationChantierPar { get; set; }
    public DateTime? ValidationChantierLe { get; set; }

    public bool Validee => ValidationTechniqueLe != null && ValidationAdministrativeLe != null
                           && (!Etapes.Get(Numero).ValidationChantier || ValidationChantierLe != null);
    public bool Terminee => FaitLe != null && (!Etapes.Get(Numero).DoubleValidation || Validee);
}

/// <summary>Trace des déverrouillages d'étape, réservés au directeur.</summary>
public class EtapeTrace : Entity
{
    public Guid DalleId { get; set; }
    public int Numero { get; set; }
    public string Motif { get; set; } = "";
    public string Par { get; set; } = "";
}
