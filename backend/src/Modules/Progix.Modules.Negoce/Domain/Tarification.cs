using Progix.SharedKernel;

namespace Progix.Modules.Negoce.Domain;

/// <summary>Un groupe de clients a des prix spéciaux. Un client appartient à un seul groupe.</summary>
public class GroupeClient : Entity
{
    public string Nom { get; set; } = "";
    public string? Description { get; set; }
    public bool Actif { get; set; } = true;
    public List<RegleGroupe> Regles { get; set; } = [];
    public List<MembreGroupe> Membres { get; set; } = [];
}

public static class TypesRegle
{
    /// <summary>Remise en pourcentage du prix de vente.</summary>
    public const string Pourcent = "pourcent";

    /// <summary>Remise en dirhams par unité.</summary>
    public const string Montant = "montant";

    /// <summary>Prix fixé pour l'article : réservé aux règles d'article.</summary>
    public const string Prix = "prix";
}

/// <summary>
/// Règle de prix d'un groupe : pour un article, pour une catégorie, ou (ni l'un ni l'autre) pour tout le catalogue.
/// La plus précise l'emporte : article, puis catégorie, puis catalogue.
/// </summary>
public class RegleGroupe : Entity
{
    public Guid GroupeId { get; set; }
    public Guid? ArticleId { get; set; }
    public Guid? CategorieId { get; set; }
    public string Type { get; set; } = TypesRegle.Pourcent;
    public decimal Valeur { get; set; }

    /// <summary>Motif de refus si la règle est incohérente, sinon null.</summary>
    public string? Refus() =>
        ArticleId is not null && CategorieId is not null ? "Une règle porte sur un article ou sur une catégorie, pas sur les deux."
        : Type is not (TypesRegle.Pourcent or TypesRegle.Montant or TypesRegle.Prix) ? "Type de règle inconnu."
        : Type == TypesRegle.Prix && ArticleId is null ? "Un prix fixé se définit article par article."
        : Valeur < 0 ? "La valeur d'une règle ne peut pas être négative."
        : Type == TypesRegle.Pourcent && Valeur > 100 ? "Une remise en pourcentage ne dépasse pas 100."
        : null;
}

public class MembreGroupe
{
    /// <summary>Le client est la clé : un client ne peut appartenir qu'à un groupe.</summary>
    public Guid ClientId { get; set; }
    public Guid GroupeId { get; set; }
}

public static class Tarification
{
    /// <summary>Règle qui s'applique à l'article : celle de l'article, sinon de sa catégorie, sinon du catalogue.</summary>
    public static RegleGroupe? RegleApplicable(IEnumerable<RegleGroupe> regles, Guid articleId, Guid categorieId)
    {
        var liste = regles as IReadOnlyCollection<RegleGroupe> ?? regles.ToList();
        return liste.FirstOrDefault(r => r.ArticleId == articleId)
            ?? liste.FirstOrDefault(r => r is { ArticleId: null } && r.CategorieId == categorieId)
            ?? liste.FirstOrDefault(r => r is { ArticleId: null, CategorieId: null });
    }

    /// <summary>Prix de vente unitaire hors taxe pour un client du groupe (null : prix public). Jamais négatif.</summary>
    public static decimal PrixPour(decimal prixPublic, RegleGroupe? regle) => regle is null ? prixPublic : Math.Max(0m, Montants.Arrondir(regle.Type switch
    {
        TypesRegle.Prix => regle.Valeur,
        TypesRegle.Pourcent => prixPublic * (1 - regle.Valeur / 100m),
        TypesRegle.Montant => prixPublic - regle.Valeur,
        _ => prixPublic,
    }));
}
