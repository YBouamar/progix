using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

public static class StatutsProjet
{
    public const string Cree = "cree";
    public const string EnEtude = "etude";
    public const string EnRealisation = "realisation";
    public const string Termine = "termine";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Cree] = "Créé",
        [EnEtude] = "En étude",
        [EnRealisation] = "En réalisation",
        [Termine] = "Terminé",
    };
}

public static class TypesConstruction
{
    public sealed record Definition(string Code, string Label, string Forfait);

    public static readonly IReadOnlyList<Definition> All =
    [
        new("immeuble", "Immeuble", "100 DH/m², puis 85 DH/m² à partir de 10 000 m²"),
        new("villa", "Villa", "120 DH/m²"),
        new("radier", "Radier", "120 DH/m²"),
        new("dallage", "Dallage", "50 DH/m²"),
    ];

    public static Definition? Find(string code) => All.FirstOrDefault(t => t.Code == code);
}

public class Projet : Entity
{
    /// <summary>Code attribué à la création, par entité et par an : APT-2026-00018.</summary>
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    public string Entite { get; set; } = Entites.Apt;
    public Guid ClientId { get; set; }

    /// <summary>Propriétaire : un client de la liste, ou un nom saisi librement.</summary>
    public Guid? ProprietaireId { get; set; }
    public string? ProprietaireLibre { get; set; }

    public string? Adresse { get; set; }
    public string? Ville { get; set; }

    /// <summary>Date de création du projet, saisie à la création : son année figure dans le code.</summary>
    public DateOnly DateCreation { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public string TypeConstruction { get; set; } = "immeuble";
    public string? Description { get; set; }
    public string Statut { get; set; } = StatutsProjet.Cree;

    // « Bloqué » est un marqueur en plus du statut.
    public bool Bloque { get; set; }
    public string? BlocageMotif { get; set; }
    public string? BlocagePar { get; set; }
    public DateTime? BlocageLe { get; set; }

    // Suppression logique, restaurable.
    public bool Supprime { get; set; }
    public string? SuppressionMotif { get; set; }
    public string? SuppressionPar { get; set; }
    public DateTime? SuppressionLe { get; set; }

    public string CreePar { get; set; } = "";
}

/// <summary>Dernier numéro attribué par entité et par an.</summary>
public class Compteur
{
    public string Cle { get; set; } = "";
    public int Valeur { get; set; }
}
