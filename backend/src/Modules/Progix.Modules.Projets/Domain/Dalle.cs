using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

/// <summary>
/// Dalle d'un projet. Référence : code du projet, numéro d'ordre, lettre de version (APT-2026-00018-01A).
/// La lettre vaut A à la création et s'incrémente à chaque nouvelle version des plans d'exécution.
/// </summary>
public class Dalle : Entity
{
    public const string Produit = "Dalle en Post-Tension";

    public Guid ProjetId { get; set; }
    public int Numero { get; set; }
    public char Version { get; set; } = 'A';
    public string? Bloc { get; set; }
    public string? Immeuble { get; set; }

    /// <summary>Niveau : négatif pour les sous-sols, 0 pour le rez-de-chaussée, positif pour les étages.</summary>
    public int Niveau { get; set; }
    public string? Detail { get; set; }
    /// <summary>Surface en m² ; 0 tant qu'elle reste à renseigner (dalle créée par niveaux, en attente des fichiers ADAPT).</summary>
    public decimal Surface { get; set; }

    /// <summary>Dernier import des exports ADAPT : ratios et câbles repris par les devis.</summary>
    public AdaptDalle? Adapt { get; set; }

    /// <summary>Un seul dessinateur par dalle.</summary>
    public Guid? DessinateurId { get; set; }
    public string? DessinateurNom { get; set; }

    /// <summary>Nombre d'étapes terminées, de 0 à 11. La dalle est réalisée à 11.</summary>
    public int EtapesFaites { get; set; }

    /// <summary>Date à laquelle l'étude (plans d'exécution) est planifiée.</summary>
    public DateOnly? EtudePlanifiee { get; set; }

    // Avis de conformité, donné une fois les 11 étapes faites. Seule une dalle conforme peut figurer sur une attestation.
    public bool? Conforme { get; set; }
    public string? ConformiteRemarque { get; set; }
    public string? ConformitePar { get; set; }
    public DateTime? ConformiteLe { get; set; }

    public string Suffixe => $"{Numero:00}{Version}";

    /// <summary>Libellé du niveau, tel que paramétré ; à défaut, libellé calculé.</summary>
    public static string NiveauLabel(int n) => NiveauxDalle.Libelle(n);

    /// <summary>Libellé calculé : sert à créer la liste des niveaux au premier démarrage.</summary>
    public static string NiveauParDefaut(int n) => n switch
    {
        0 => "Rez-de-chaussée",
        -1 => "1er sous-sol",
        < -1 => $"{-n}ème sous-sol",
        1 => "1er étage",
        _ => $"{n}ème étage",
    };
}
