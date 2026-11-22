using Progix.SharedKernel;

namespace Progix.Modules.Negoce.Domain;

public static class EtatsAchat
{
    public const string EnAttente = "en_attente";
    public const string Partielle = "partielle";
    public const string Recue = "recue";
    public const string Annulee = "annulee";

    public static string Label(string etat) => etat switch
    {
        EnAttente => "En attente",
        Partielle => "Partiellement reçue",
        Recue => "Reçue",
        Annulee => "Annulée",
        _ => etat,
    };
}

public static class ModesPaiementNegoce
{
    public const string Virement = "virement";
    public const string Cheque = "cheque";
    public const string Effet = "effet";
    public const string Especes = "especes";

    public static readonly string[] Tous = [Virement, Cheque, Effet, Especes];

    /// <summary>Le chèque et l'effet s'accompagnent de leur image.</summary>
    public static bool AvecImage(string mode) => mode is Cheque or Effet;
}

public class CommandeAchat : Entity
{
    public string Numero { get; set; } = "";
    public string Entite { get; set; } = "";
    public Guid FournisseurId { get; set; }
    public string FournisseurNom { get; set; } = "";
    public DateOnly DateCommande { get; set; }
    public string? Notes { get; set; }
    public string CreePar { get; set; } = "";
    public bool Annule { get; set; }
    public string? MotifAnnulation { get; set; }
    public string? AnnulePar { get; set; }
    public DateTime? AnnuleLe { get; set; }
    public List<LigneAchat> Lignes { get; set; } = [];
    public List<ReceptionAchat> Receptions { get; set; } = [];
    public List<FactureFournisseur> Factures { get; set; } = [];
    public List<PaiementFournisseur> Paiements { get; set; } = [];

    /// <summary>En attente, partiellement reçue (au moins une réception, pas toutes les quantités), reçue.</summary>
    public string Etat => Annule ? EtatsAchat.Annulee
        : Lignes.Count > 0 && Lignes.All(l => l.QuantiteRecue >= l.Quantite) ? EtatsAchat.Recue
        : Lignes.Any(l => l.QuantiteRecue > 0) ? EtatsAchat.Partielle
        : EtatsAchat.EnAttente;

    public decimal TotalHT => Lignes.Sum(l => Montants.LigneHT(l.Quantite, l.PrixUnitaire));
    public decimal TotalTVA => Montants.TvaDe(TotalHT, Tva.Taux);
    public decimal TotalTTC => TotalHT + TotalTVA;
    public decimal Paye => Paiements.Where(p => !p.Annule).Sum(p => p.Montant);
    public decimal ResteAPayer => TotalTTC - Paye;
}

public class LigneAchat : Entity
{
    public Guid CommandeId { get; set; }
    public Guid ArticleId { get; set; }
    public string Code { get; set; } = "";
    public string Designation { get; set; } = "";
    public string Unite { get; set; } = "";
    public decimal Quantite { get; set; }

    /// <summary>Prix d'achat hors taxe, figé à la commande.</summary>
    public decimal PrixUnitaire { get; set; }
    public decimal QuantiteRecue { get; set; }

    public decimal Reste => Math.Max(0, Quantite - QuantiteRecue);
}

/// <summary>Une réception, même partielle : elle crée une entrée en stock par ligne.</summary>
public class ReceptionAchat : Entity
{
    public Guid CommandeId { get; set; }
    public DateOnly DateReception { get; set; }
    public string? Notes { get; set; }
    public string Par { get; set; } = "";
    public List<LigneReception> Lignes { get; set; } = [];
}

public class LigneReception : Entity
{
    public Guid ReceptionId { get; set; }
    public Guid LigneAchatId { get; set; }
    public Guid ArticleId { get; set; }
    public decimal Quantite { get; set; }
}

/// <summary>Facture reçue d'un fournisseur ; son scan se joint par le module des fichiers.</summary>
public class FactureFournisseur : Entity
{
    public Guid CommandeId { get; set; }
    public string NumeroFournisseur { get; set; } = "";
    public DateOnly DateFacture { get; set; }
    public decimal MontantTTC { get; set; }
    public string? Notes { get; set; }
    public string Par { get; set; } = "";
}

public class PaiementFournisseur : Entity
{
    public Guid CommandeId { get; set; }
    public DateOnly DatePaiement { get; set; }
    public decimal Montant { get; set; }
    public string Mode { get; set; } = ModesPaiementNegoce.Virement;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string Par { get; set; } = "";
    public bool Annule { get; set; }
    public string? MotifAnnulation { get; set; }
    public string? AnnulePar { get; set; }
    public DateTime? AnnuleLe { get; set; }
}

/// <summary>Quantités d'une réception : contrôles purs, testables sans base.</summary>
public static class Reception
{
    /// <summary>Motif de refus d'une ligne reçue, ou null. Une ligne ne dépasse jamais la quantité commandée.</summary>
    public static string? Refus(LigneAchat ligne, decimal quantite) =>
        quantite <= 0 ? $"La quantité reçue de « {ligne.Designation} » doit être supérieure à zéro."
        : quantite > ligne.Reste ? $"« {ligne.Designation} » : {quantite:0.####} reçus pour {ligne.Reste:0.####} restant à recevoir."
        : null;
}
