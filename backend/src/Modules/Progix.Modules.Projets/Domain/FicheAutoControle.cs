using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

/// <summary>Fiche d'auto-contrôle du plancher, remplie à la réception de dalle (étape 6).</summary>
public class FicheAutoControle : Entity
{
    public const string Conforme = "c";
    public const string NonConforme = "nc";
    public const string NonApplicable = "na";

    public Guid DalleId { get; set; }
    public List<PointControle> Points { get; set; } = [];
    public string? SaisiPar { get; set; }

    public sealed record Definition(string Code, string Section, string Rubrique, string Libelle);

    /// <summary>Points critiques du modèle de fiche, dans l'ordre.</summary>
    public static readonly IReadOnlyList<Definition> Modele =
    [
        new("a1", "Armature", "A) Contrôle visuel", "Alignement du ferraillage"),
        new("b1", "Armature", "B) Contrôle des mesures", "Positionnement"),
        new("b2", "Armature", "B) Contrôle des mesures", "Recouvrement"),
        new("b3", "Armature", "B) Contrôle des mesures", "Mise en place des câbles"),
        new("c1", "Armature", "C) Cages assemblées aux plans", "Nombre"),
        new("c2", "Armature", "C) Cages assemblées aux plans", "Diamètre"),
        new("c3", "Armature", "C) Cages assemblées aux plans", "Position des cadres, épingles, etc."),
        new("p1", "Câbles précontraints", "", "Alignement des câbles"),
        new("p2", "Câbles précontraints", "", "Positionnement"),
        new("p3", "Câbles précontraints", "", "Mise en place des tréteaux"),
        new("p4", "Câbles précontraints", "", "Nombre de câbles installés"),
        new("p5", "Câbles précontraints", "", "Type d'ancrage"),
    ];
}

public class PointControle
{
    public string Code { get; set; } = "";
    /// <summary>« c » conforme, « nc » non conforme, « na » non applicable.</summary>
    public string Resultat { get; set; } = "";
    public string? Observation { get; set; }
}
