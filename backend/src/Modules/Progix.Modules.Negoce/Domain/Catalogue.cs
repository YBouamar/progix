using Progix.SharedKernel;

namespace Progix.Modules.Negoce.Domain;

/// <summary>Unité de vente ou de stock d'un article : pièce, mètre, mètre carré, tonne...</summary>
public class Unite : Entity
{
    public string Libelle { get; set; } = "";
    public string Symbole { get; set; } = "";
    public bool Actif { get; set; } = true;
}

public class CategorieArticle : Entity
{
    public string Libelle { get; set; } = "";
    public int Ordre { get; set; }
    public bool Actif { get; set; } = true;
}

/// <summary>Un article est acheté à des fournisseurs, ou produit à l'usine (pas de prix d'achat : un coût moyen saisi).</summary>
public static class OriginesArticle
{
    public const string Achete = "achete";
    public const string Produit = "produit";

    public static bool Valide(string? o) => o is Achete or Produit;
}

public class Article : Entity
{
    public string Code { get; set; } = "";
    public string Designation { get; set; } = "";
    public Guid CategorieId { get; set; }
    public CategorieArticle? Categorie { get; set; }
    public Guid UniteId { get; set; }
    public Unite? Unite { get; set; }

    /// <summary>Prix de vente public, hors taxe.</summary>
    public decimal PrixVente { get; set; }
    public string Origine { get; set; } = OriginesArticle.Achete;

    /// <summary>Coût moyen pondéré : recalculé à chaque entrée en stock ; pour un article produit, il se saisit sur la fiche.</summary>
    public decimal CoutMoyen { get; set; }

    /// <summary>Stock en dessous duquel l'article est signalé.</summary>
    public decimal SeuilAlerte { get; set; }

    /// <summary>Poids d'un mètre, en kilogrammes, pour un article stocké en tonnes et mesuré en mètres (toron T15,7 : 1,18).</summary>
    public decimal? KgParMetre { get; set; }
    public bool Actif { get; set; } = true;

    /// <summary>
    /// Verrou optimiste du stock : chaque mouvement l'incrémente, et l'enregistrement échoue si une autre saisie a modifié l'article entre-temps.
    /// Deux sorties simultanées ne peuvent donc pas dépasser le stock.
    /// </summary>
    public int VersionStock { get; set; }
}

/// <summary>Prix d'achat hors taxe d'un article chez un fournisseur. Un seul prix par couple article et fournisseur.</summary>
public class PrixFournisseur : Entity
{
    public Guid ArticleId { get; set; }
    public Guid FournisseurId { get; set; }
    public decimal PrixAchat { get; set; }
}

public class Fournisseur : Entity
{
    public string Nom { get; set; } = "";
    public string? Contact { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public string? Adresse { get; set; }
    public string? Ville { get; set; }
    public string? Ice { get; set; }
    public string? Notes { get; set; }
    public bool Actif { get; set; } = true;
}

/// <summary>TVA des ventes directes et des achats : 20 % partout, sauf client exonéré.</summary>
public static class Tva
{
    public const decimal Taux = 20m;
}

public static class Montants
{
    public static decimal Arrondir(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public static decimal LigneHT(decimal quantite, decimal prix) => Arrondir(quantite * prix);

    public static decimal TvaDe(decimal ht, decimal taux) => Arrondir(ht * taux / 100m);
}
