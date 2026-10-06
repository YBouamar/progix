using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Domain;

public static class TypesFacture
{
    public const string Livraison = "livraison";
    public const string Acompte = "acompte";
}

/// <summary>Facture. Une fois émise, elle ne se modifie pas et ne se supprime pas : une erreur se corrige par un avoir.</summary>
public class Facture : Entity
{
    /// <summary>Numéro FAC-XXXXX-AAAA, par entité et par an.</summary>
    public string Numero { get; set; } = "";
    public string Entite { get; set; } = "";
    public string? AncienNumero { get; set; }
    public string Type { get; set; } = TypesFacture.Livraison;

    public Guid ProjetId { get; set; }
    public string ProjetCode { get; set; } = "";
    public string ProjetLibelle { get; set; } = "";

    // Identité du client figée à l'émission.
    public Guid ClientId { get; set; }
    public string ClientNom { get; set; } = "";
    public string? ClientIdentifiants { get; set; }
    public string? ClientAdresse { get; set; }

    public DateOnly DateFacture { get; set; }
    public DateOnly Echeance { get; set; }
    /// <summary>Mode de règlement convenu, imprimé sur la facture.</summary>
    public string? ModeReglement { get; set; }

    /// <summary>Bons de livraison facturés, ou « Acompte ».</summary>
    public string Origine { get; set; } = "";

    // Facture d'acompte : pourcentage du devis validé.
    public Guid? DevisId { get; set; }
    public string? DevisNumero { get; set; }
    public decimal? AcomptePct { get; set; }

    public decimal TotalHT { get; set; }
    /// <summary>Acompte déjà facturé, déduit au prorata du montant de la facture.</summary>
    public decimal AcompteDeduit { get; set; }
    public decimal NetHT { get; set; }
    public decimal TauxTva { get; set; }
    public decimal Tva { get; set; }
    public decimal TotalTTC { get; set; }

    public string EmisPar { get; set; } = "";

    // Signature électronique par mot de passe : obligatoire avant d'imprimer ou d'envoyer le document.
    public string? SignePar { get; set; }
    public DateTime? SigneLe { get; set; }
    /// <summary>Jeton aléatoire porté par le QR code de vérification.</summary>
    public string? Jeton { get; set; }
    public List<LigneFacture> Lignes { get; set; } = [];
    public List<Encaissement> Encaissements { get; set; } = [];

    private static DateOnly Jour => DateOnly.FromDateTime(DateTime.UtcNow);
    /// <summary>Encaissé à ce jour : les effets non échus ne comptent pas, les remboursements se retranchent.</summary>
    public decimal Regle => Encaissements.Where(e => !e.Annule && !e.EnAttente(Jour)).Sum(e => e.Remboursement ? -e.Montant : e.Montant);
    /// <summary>Effets reçus dont l'échéance n'est pas encore arrivée.</summary>
    public decimal EffetsEnAttente => Encaissements.Where(e => e.EnAttente(Jour)).Sum(e => e.Montant);
    /// <summary>Ce qu'il reste à couvrir par un nouveau règlement, effets en attente déduits.</summary>
    public decimal ACouvrir => Reste - EffetsEnAttente;
    public List<Avoir> Avoirs { get; set; } = [];

    public decimal Avoirs_TTC => Avoirs.Where(a => !a.Annule).Sum(a => a.TotalTTC);
    /// <summary>Montant dû après avoirs.</summary>
    public decimal Du => TotalTTC - Avoirs_TTC;
    public bool Annulee => Avoirs.Any(a => a.Total && !a.Annule);
    /// <summary>Négatif quand le client a payé plus que le montant dû après avoir : trop-perçu à rembourser.</summary>
    public decimal Reste => Du - Regle;
}

public class LigneFacture : Entity
{
    public Guid FactureId { get; set; }
    public int Ordre { get; set; }
    public string Libelle { get; set; } = "";
    public Guid? DalleId { get; set; }
    public decimal? Surface { get; set; }
    public decimal? PrixM2 { get; set; }
    public decimal MontantHT { get; set; }
}
