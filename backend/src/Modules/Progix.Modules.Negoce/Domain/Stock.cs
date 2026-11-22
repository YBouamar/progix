using Progix.SharedKernel;

namespace Progix.Modules.Negoce.Domain;

public static class TypesMouvement
{
    public const string Entree = "entree";

    /// <summary>Entrée directe d'un article fabriqué à l'usine. La fabrication elle-même n'est pas gérée.</summary>
    public const string Production = "production";
    public const string Sortie = "sortie";

    /// <summary>Stock compté : le mouvement ajuste l'écart avec le stock enregistré.</summary>
    public const string Inventaire = "inventaire";
    public const string Correction = "correction";

    public static readonly string[] Tous = [Entree, Production, Sortie, Inventaire, Correction];
}

/// <summary>Mouvement de stock : on ne le modifie pas, une erreur se corrige par un mouvement de sens contraire.</summary>
public class MouvementStock : Entity
{
    public Guid ArticleId { get; set; }
    public string Type { get; set; } = TypesMouvement.Entree;

    /// <summary>Quantité signée : positive pour une entrée, négative pour une sortie.</summary>
    public decimal Quantite { get; set; }

    /// <summary>Coût unitaire retenu : celui de l'entrée, ou le coût moyen au moment d'une sortie.</summary>
    public decimal CoutUnitaire { get; set; }
    public DateOnly Date { get; set; }
    public string? Motif { get; set; }

    /// <summary>Pièce d'origine : réception d'achat, bon de livraison...</summary>
    public string? PieceType { get; set; }
    public Guid? PieceId { get; set; }
    public string? PieceNumero { get; set; }
    public string Par { get; set; } = "";
}

public static class Valorisation
{
    /// <summary>
    /// Coût moyen pondéré après une entrée. Sans stock positif, le coût de l'entrée devient le coût moyen.
    /// </summary>
    public static decimal NouveauCoutMoyen(decimal stock, decimal coutMoyen, decimal quantiteEntree, decimal coutEntree) =>
        stock <= 0 || stock + quantiteEntree <= 0 ? coutEntree
        : Math.Round((stock * coutMoyen + quantiteEntree * coutEntree) / (stock + quantiteEntree), 4, MidpointRounding.AwayFromZero);

    /// <summary>Tonnes d'un métrage de toron : 1 m de toron T15,7 pèse 1,18 kg.</summary>
    public static decimal TonnesDepuisMetres(decimal metres, decimal kgParMetre) => Math.Round(metres * kgParMetre / 1000m, 4, MidpointRounding.AwayFromZero);

    /// <summary>Mouvement signé d'une saisie, ou le motif de refus. <paramref name="saisie"/> est la quantité saisie (compté, pour un inventaire).</summary>
    public static (decimal? Quantite, string? Refus) Signer(string type, decimal saisie, decimal stock, string? motif)
    {
        switch (type)
        {
            case TypesMouvement.Entree or TypesMouvement.Production:
                return saisie > 0 ? (saisie, null) : (null, "La quantité entrée doit être supérieure à zéro.");
            case TypesMouvement.Sortie:
                if (saisie <= 0) return (null, "La quantité sortie doit être supérieure à zéro.");
                if (string.IsNullOrWhiteSpace(motif)) return (null, "Le motif de la sortie est obligatoire.");
                return saisie > stock ? (null, $"Stock insuffisant : {stock:0.####} en stock.") : (-saisie, null);
            case TypesMouvement.Inventaire:
                if (saisie < 0) return (null, "Le stock compté ne peut pas être négatif.");
                return saisie == stock ? (null, "Le stock compté est identique au stock enregistré.") : (saisie - stock, null);
            case TypesMouvement.Correction:
                if (saisie == 0) return (null, "La quantité de la correction ne peut pas être nulle.");
                if (string.IsNullOrWhiteSpace(motif)) return (null, "Le motif de la correction est obligatoire.");
                return stock + saisie < 0 ? (null, $"La correction rendrait le stock négatif ({stock:0.####} en stock).") : (saisie, null);
            default:
                return (null, "Type de mouvement inconnu.");
        }
    }
}
