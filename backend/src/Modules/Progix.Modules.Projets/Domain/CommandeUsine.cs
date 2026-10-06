using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

/// <summary>
/// Commande usine d'une dalle : la liste de ses tendons (numéro, système, orientation, longueur, ancrages) et ses accessoires.
/// Gaine = longueur du tendon ; torons = longueur × nombre de torons du système.
/// </summary>
public class CommandeUsine : Entity
{
    public Guid DalleId { get; set; }
    /// <summary>Une ligne par tendon. Les commandes d'avant ce modèle gardent leurs lignes groupées (préfixe et nombre).</summary>
    public List<LigneCommande> Lignes { get; set; } = [];
    public List<AccessoireCommande> Accessoires { get; set; } = [];

    /// <summary>Nombre total de tendons, tenu à jour pour les contrôles.</summary>
    public int NbCables { get; set; }

    // Suivi logistique après les deux validations : en fabrication, livrée, reçue.
    public DateTime? FabricationLe { get; set; }
    public DateTime? LivreeLe { get; set; }
    public DateTime? RecueLe { get; set; }
    public string? SuiviPar { get; set; }

    /// <summary>Le stock est consommé dès que la commande est livrée.</summary>
    public bool Consommee => LivreeLe != null;

    /// <summary>Liste des tendons, un par un. Une ancienne ligne groupée est dépliée en X01, X02...</summary>
    public IEnumerable<Tendon> Tendons()
    {
        var compteurs = new Dictionary<string, int>();
        foreach (var l in Lignes)
        {
            if (l.Numero is not null) { yield return new Tendon(l.Numero, l.Torons, l.Sens, l.Longueur, l.Ancrages); continue; }
            for (var i = 0; i < l.Nombre; i++)
            {
                var n = compteurs[l.Prefixe] = compteurs.GetValueOrDefault(l.Prefixe) + 1;
                yield return new Tendon($"{l.Prefixe}{n:00}", l.Torons, l.Sens, l.Longueur, 2);
            }
        }
    }

    public IEnumerable<(string Nom, decimal Longueur)> Cables() => Tendons().Select(t => (t.Numero, t.Longueur));
}

/// <param name="Torons">Le système est désigné par son nombre de torons : S4 = 4.</param>
public record Tendon(string Numero, int Torons, string Sens, decimal Longueur, int Ancrages)
{
    public string Systeme => $"S{Torons}";
}

public class LigneCommande
{
    /// <summary>Numéro du tendon. Absent sur une ancienne ligne groupée.</summary>
    public string? Numero { get; set; }
    /// <summary>Le système est désigné par son nombre de torons.</summary>
    public int Torons { get; set; }
    public string Sens { get; set; } = "X";
    public string Prefixe { get; set; } = "X";
    public int Nombre { get; set; } = 1;
    public decimal Longueur { get; set; }
    /// <summary>Ancrages du tendon, tels que donnés à l'import ou à la saisie. Absent sur une ancienne ligne : 2 par câble.</summary>
    public int? AncragesSaisis { get; set; }

    public decimal Gaine => Nombre * Longueur;
    public decimal MetresTorons => Gaine * Torons;
    public int Ancrages => AncragesSaisis ?? Nombre * 2;
}

public class AccessoireCommande
{
    /// <summary>Accessoire paramétré d'origine, pour le suivi du stock.</summary>
    public Guid? AccessoireId { get; set; }
    public string Libelle { get; set; } = "";
    public string? Unite { get; set; }
    public int Quantite { get; set; }
}

/// <summary>Accessoire paramétré, proposé d'office dans chaque commande usine.</summary>
public class Accessoire : Entity
{
    public string Libelle { get; set; } = "";

    /// <summary>Unité de commande : pièce, rouleau, litre, kg...</summary>
    public string? Unite { get; set; }
    public int Ordre { get; set; }
    public bool Actif { get; set; } = true;

    public static readonly (string Libelle, string Unite)[] ParDefaut =
    [
        ("SCOTCHE", "Unité"), ("PEINTURE GRIS+CATALISEUR", "Kg"), ("PEINTURE BLEU", "Kg"),
        ("DULIANT", "L"), ("LA GRAISSE", "Kg"), ("DISQUE A MEULE", "Unité"),
    ];
}

/// <summary>Article de stock d'une entité : ancrage ou gaine d'un système, ou toron. Code : « ancrage:4 », « gaine:4 », « toron », ou « accessoire:identifiant ».</summary>
public class ArticleStock : Entity
{
    public string Entite { get; set; } = "";
    public string Code { get; set; } = "";
    public decimal StockInitial { get; set; }
    public decimal Seuil { get; set; }

    public static string Libelle(string code)
    {
        var p = code.Split(':');
        return p[0] switch { "ancrage" => $"Ancrage S{p[1]}, unité", "gaine" => $"Gaine S{p[1]}, m", _ => "Toron T15, m" };
    }
}

/// <summary>Mouvement manuel de stock : entrée, sortie ou correction d'inventaire, toujours motivé.</summary>
public class MouvementStock : Entity
{
    public string Entite { get; set; } = "";
    public string Code { get; set; } = "";
    public string Sens { get; set; } = "";
    /// <summary>Quantité signée : positive pour une entrée, négative pour une sortie.</summary>
    public decimal Quantite { get; set; }
    public string Motif { get; set; } = "";
    public string Par { get; set; } = "";
}
