using Progix.SharedKernel;

namespace Progix.Modules.Negoce.Domain;

public static class TypesVente
{
    public const string Devis = "devis";
    public const string Commande = "commande";
    public const string Livraison = "bl";
    public const string Facture = "facture";
    public const string Avoir = "avoir";
}

public static class EtatsVente
{
    /// <summary>Devis ou commande en cours.</summary>
    public const string Ouvert = "ouvert";

    /// <summary>Devis transformé en commande.</summary>
    public const string Accepte = "accepte";
    public const string Refuse = "refuse";

    /// <summary>Bon de livraison, facture ou avoir émis : il ne se modifie plus, il s'annule avec un motif.</summary>
    public const string Emis = "emis";
    public const string Annule = "annule";
}

/// <summary>
/// Document de vente directe : devis, commande, bon de livraison, facture ou avoir. Chaque type est numéroté dans la série de son entité,
/// la même que celle de la post-tension (sauf la commande, série propre au négoce).
/// </summary>
public class DocumentVente : Entity
{
    public string Type { get; set; } = TypesVente.Devis;
    public string Numero { get; set; } = "";
    public string Entite { get; set; } = "";
    public Guid ClientId { get; set; }
    public string ClientNom { get; set; } = "";
    public Guid? ProjetId { get; set; }
    public string? ProjetCode { get; set; }
    public string? ProjetLibelle { get; set; }
    public Guid? GroupeId { get; set; }
    public string? GroupeNom { get; set; }

    /// <summary>Document dont celui-ci est issu : devis d'une commande, commande d'un bon, facture d'un avoir.</summary>
    public Guid? ParentId { get; set; }
    public string? ParentNumero { get; set; }
    public DateOnly DateDocument { get; set; }

    /// <summary>Validité d'un devis, échéance d'une facture.</summary>
    public DateOnly? Echeance { get; set; }
    public string Etat { get; set; } = EtatsVente.Ouvert;
    public decimal TauxTva { get; set; } = Tva.Taux;
    public string? Notes { get; set; }
    public string CreePar { get; set; } = "";
    public string? MotifAnnulation { get; set; }
    public string? AnnulePar { get; set; }
    public DateTime? AnnuleLe { get; set; }

    /// <summary>Signature électronique : le jeton aléatoire donne accès à la vérification publique (QR code du PDF). Un document signé ne se modifie plus.</summary>
    public string? Jeton { get; set; }
    public string? SignePar { get; set; }
    public DateTime? SigneLe { get; set; }
    public List<LigneVente> Lignes { get; set; } = [];
    public List<PaiementClient> Paiements { get; set; } = [];

    public bool Annule => Etat == EtatsVente.Annule;
    public decimal TotalHT => Lignes.Sum(l => Montants.LigneHT(l.Quantite, l.PrixUnitaire));
    public decimal TotalTVA => Montants.TvaDe(TotalHT, TauxTva);
    public decimal TotalTTC => TotalHT + TotalTVA;
    public decimal Encaisse => Paiements.Where(p => !p.Annule).Sum(p => p.Montant);
}

public class LigneVente : Entity
{
    public Guid DocumentId { get; set; }
    public Guid ArticleId { get; set; }
    public string Code { get; set; } = "";
    public string Designation { get; set; } = "";
    public string Unite { get; set; } = "";
    public decimal Quantite { get; set; }
    public decimal PrixUnitaire { get; set; }

    /// <summary>Ligne du document parent dont celle-ci reprend une quantité.</summary>
    public Guid? SourceLigneId { get; set; }

    /// <summary>Coût moyen au moment de la sortie de stock, pour un bon de livraison : il sert à la marge.</summary>
    public decimal CoutUnitaire { get; set; }
}

public class PaiementClient : Entity
{
    public Guid FactureId { get; set; }
    public Guid ClientId { get; set; }
    public string Entite { get; set; } = "";
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

public class CompteurNegoce
{
    public string Cle { get; set; } = "";
    public int Valeur { get; set; }
}

/// <summary>Quantités livrées et facturées d'une commande : contrôles purs, testables sans base.</summary>
public static class Avancement
{
    /// <summary>Quantité déjà reprise d'une ligne source par les documents de ce type qui ne sont pas annulés.</summary>
    public static decimal Reprise(IEnumerable<DocumentVente> documents, string type, Guid ligneSourceId) =>
        documents.Where(d => d.Type == type && !d.Annule).SelectMany(d => d.Lignes).Where(l => l.SourceLigneId == ligneSourceId).Sum(l => l.Quantite);

    /// <summary>Quantité encore livrable d'une ligne de commande.</summary>
    public static decimal ResteALivrer(LigneVente commande, IEnumerable<DocumentVente> enfants) =>
        commande.Quantite - Reprise(enfants, TypesVente.Livraison, commande.Id);

    /// <summary>Quantité encore facturable : on ne facture que ce qui est livré.</summary>
    public static decimal ResteAFacturer(LigneVente commande, IEnumerable<DocumentVente> enfants) =>
        Reprise(enfants, TypesVente.Livraison, commande.Id) - Reprise(enfants, TypesVente.Facture, commande.Id);

    /// <summary>Quantité encore créditable sur une ligne de facture : pas plus que ce qui a été facturé.</summary>
    public static decimal ResteACrediter(LigneVente facture, IEnumerable<DocumentVente> avoirs) =>
        facture.Quantite - Reprise(avoirs, TypesVente.Avoir, facture.Id);
}
