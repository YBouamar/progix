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

/// <summary>
/// Type de construction proposé à la création d'un projet, paramétrable. Le projet garde le code, qui ne change jamais ;
/// un type porté par un projet ne se supprime pas, il se désactive. Le forfait ne dépend plus du type (grille unique du devis).
/// </summary>
public class TypeConstructionParam : Entity
{
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    public int Ordre { get; set; }
    public bool Actif { get; set; } = true;

    /// <summary>Types créés au premier démarrage : ceux proposés jusqu'ici.</summary>
    public static readonly (string Code, string Libelle)[] ParDefaut = [("immeuble", "Immeuble"), ("villa", "Villa"), ("radier", "Radier"), ("dallage", "Dallage")];
}

/// <summary>Libellés des types de construction en mémoire, chargés au démarrage du module et rechargés à chaque enregistrement du paramétrage.</summary>
public static class TypesConstruction
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> Libelles = new();

    public static string Libelle(string code) =>
        Libelles.TryGetValue(code, out var l) ? l : TypeConstructionParam.ParDefaut.FirstOrDefault(t => t.Code == code).Libelle ?? code;

    public static void Charger(IEnumerable<TypeConstructionParam> types)
    {
        var liste = types.ToList();
        foreach (var t in liste) Libelles[t.Code] = t.Libelle;
        foreach (var c in Libelles.Keys.Except(liste.Select(t => t.Code))) Libelles.TryRemove(c, out _);
    }
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

    /// <summary>Agent commercial du projet, facultatif, choisi à la création. Les devis du projet le reprennent.</summary>
    public Guid? CommercialId { get; set; }
    public string? CommercialNom { get; set; }
    /// <summary>Vrai une fois le commercial choisi ou repris des anciens devis : la reprise ne repasse pas sur un choix fait depuis.</summary>
    public bool CommercialRepris { get; set; }
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
