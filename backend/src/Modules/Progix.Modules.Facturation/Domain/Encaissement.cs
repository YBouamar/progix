using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Domain;

public static class ModesPaiement
{
    public const string Virement = "virement";
    public const string Cheque = "cheque";
    public const string Especes = "especes";
    public const string Effet = "effet";

    public static readonly Dictionary<string, string> Libelles = new()
    {
        [Virement] = "Virement", [Cheque] = "Chèque", [Especes] = "Espèces", [Effet] = "Effet",
    };
}

/// <summary>
/// Règlement reçu sur un projet, ventilé entre ses dalles. Il ne se modifie pas : une erreur s'annule avec un motif, puis se ressaisit.
/// Les factures ne reçoivent pas de règlement : leur statut se déduit des paiements de leurs dalles.
/// </summary>
public class Encaissement : Entity
{
    /// <summary>Facture réglée, pour les règlements saisis avant le suivi par dalle seulement.</summary>
    public Guid? FactureId { get; set; }
    public Guid ProjetId { get; set; }
    public string Entite { get; set; } = "";

    // Projet et client figés à l'encaissement, repris sur le reçu.
    public string ProjetCode { get; set; } = "";
    public string ProjetLibelle { get; set; } = "";
    public string ClientNom { get; set; } = "";
    public string? ClientAdresse { get; set; }
    public string? ClientIdentifiants { get; set; }

    /// <summary>Ventilation du montant entre les dalles du projet ; la somme fait le montant.</summary>
    public List<PaiementDalle> Dalles { get; set; } = [];
    public Guid ClientId { get; set; }
    public DateOnly DatePaiement { get; set; }
    public string Mode { get; set; } = ModesPaiement.Virement;
    public decimal Montant { get; set; }
    /// <summary>Numéro de chèque, d'effet ou référence du virement.</summary>
    public string? Reference { get; set; }
    public string? Banque { get; set; }
    /// <summary>Date d'échéance, obligatoire pour un effet.</summary>
    public DateOnly? EcheanceEffet { get; set; }
    /// <summary>Remboursement au client d'un trop-perçu, après un avoir sur une facture déjà payée.</summary>
    public bool Remboursement { get; set; }
    // Reçu de paiement : numéro REC-XXXXX-AAAA par entité, signé par mot de passe à l'encaissement.
    public string? NumeroRecu { get; set; }
    public string? AncienNumeroRecu { get; set; }
    public DateTime? SigneLe { get; set; }
    /// <summary>Jeton aléatoire porté par le QR code, pour vérifier l'authenticité du reçu.</summary>
    public string? Jeton { get; set; }

    public string SaisiPar { get; set; } = "";

    /// <summary>Un effet n'est encaissé qu'à sa date d'échéance.</summary>
    public bool EnAttente(DateOnly jour) => !Annule && !Remboursement && Mode == ModesPaiement.Effet && EcheanceEffet > jour;

    public bool Annule { get; set; }
    public string? MotifAnnulation { get; set; }
    public string? AnnulePar { get; set; }
    public DateTime? AnnuleLe { get; set; }
}

/// <summary>Part d'un règlement affectée à une dalle.</summary>
public class PaiementDalle
{
    public Guid DalleId { get; set; }
    public string Reference { get; set; } = "";
    public decimal Montant { get; set; }
}
