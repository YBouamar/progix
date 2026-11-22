using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

/// <summary>
/// Commande usine d'une dalle : la liste de ses tendons (numéro, système, orientation, longueur, ancrages) et ses accessoires.
/// Gaine = longueur du tendon ; torons = longueur × nombre de torons du système.
/// </summary>
public class CommandeUsine : Entity
{
    public Guid DalleId { get; set; }

    /// <summary>Rang de la commande sur la dalle : 1 pour la commande initiale, puis 2, 3... pour les compléments envoyés plus tard au chantier.</summary>
    public int Numero { get; set; } = 1;
    /// <summary>Une ligne par tendon. Les commandes d'avant ce modèle gardent leurs lignes groupées (préfixe et nombre).</summary>
    public List<LigneCommande> Lignes { get; set; } = [];
    public List<AccessoireCommande> Accessoires { get; set; } = [];

    /// <summary>Surface totale en m², saisie avec la commande : imprimée sur le bon de livraison et facturée.</summary>
    public decimal? Surface { get; set; }

    /// <summary>Nombre total de tendons, tenu à jour pour les contrôles.</summary>
    public int NbCables { get; set; }

    // Suivi logistique : à préparer (après les deux validations), préparée, envoyée au chantier, reçue au chantier, ou annulée.
    // Les colonnes gardent leurs anciens noms : FabricationLe = préparée, LivreeLe = envoyée au chantier.
    public DateTime? FabricationLe { get; set; }
    public DateTime? LivreeLe { get; set; }
    public DateTime? RecueLe { get; set; }
    public string? SuiviPar { get; set; }

    /// <summary>Annulation : à tout moment, avec un motif tracé. Une commande annulée ne consomme plus de stock et n'est plus comptée.</summary>
    public DateTime? AnnuleeLe { get; set; }
    public string? AnnuleePar { get; set; }
    public string? MotifAnnulation { get; set; }

    /// <summary>Modifications faites après la soumission, y compris après l'envoi : ce que l'usine doit voir.</summary>
    public List<ModificationCommande> Modifications { get; set; } = [];

    public bool Annulee => AnnuleeLe != null;

    /// <summary>Le stock est consommé dès que la commande est envoyée au chantier, sauf si elle est annulée.</summary>
    public bool Consommee => LivreeLe != null && AnnuleeLe == null;

    public const string APreparer = "a_preparer", Preparee = "preparee", Envoyee = "envoyee", Recue = "recue", Annulee_ = "annulee", Attente = "attente";

    /// <summary>État de suivi. Une commande initiale attend ses deux validations ; un complément est à préparer dès sa création.</summary>
    public string Etat(bool validee) =>
        AnnuleeLe != null ? Annulee_ : RecueLe != null ? Recue : LivreeLe != null ? Envoyee : FabricationLe != null ? Preparee
        : Numero > 1 || validee ? APreparer : Attente;

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

    /// <summary>Ligne changée après la soumission : date, et valeurs d'avant (« S2 · X · 7,00 m · 2 ancrages »). Une ligne ajoutée après est marquée telle quelle.</summary>
    public DateTime? ModifieLe { get; set; }
    public string? Avant { get; set; }
    public bool Ajoutee { get; set; }

    public decimal Gaine => Nombre * Longueur;
    public decimal MetresTorons => Gaine * Torons;
    public int Ancrages => AncragesSaisis ?? Nombre * 2;
}

/// <summary>Une modification d'une commande déjà soumise : qui, quand, dans quel état, et chaque changement en une ligne lisible.</summary>
public class ModificationCommande
{
    public DateTime Le { get; set; }
    public string Par { get; set; } = "";
    public string Etat { get; set; } = "";
    public List<string> Changements { get; set; } = [];
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
