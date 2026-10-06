using Progix.SharedKernel;

namespace Progix.Modules.Partenaires.Domain;

public static class TypesPartenaire
{
    public const string ClientFinal = "client_final";
    public const string Bet = "bet";
    public const string Bct = "bct";
    public const string Architecte = "architecte";
    public const string Laboratoire = "laboratoire";
    public const string EntrepriseConstruction = "entreprise_construction";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [ClientFinal] = "Client final",
        [Bet] = "BET",
        [Bct] = "BCT",
        [Architecte] = "Architecte",
        [Laboratoire] = "Laboratoire",
        [EntrepriseConstruction] = "Entreprise de construction",
    };
}

public static class Natures
{
    public const string Morale = "morale";
    public const string Physique = "physique";
    /// <summary>Catégorie libre : un nom suffit, aucun identifiant légal (ICE, RC, IF, patente, CIN).</summary>
    public const string Autre = "autre";
}

/// <summary>Client ou partenaire : personne morale ou personne physique.</summary>
public class Partenaire : Entity
{
    public string Type { get; set; } = TypesPartenaire.ClientFinal;
    public string Nature { get; set; } = Natures.Morale;

    // Personne morale
    public string? RaisonSociale { get; set; }
    public string? Ice { get; set; }
    public string? Rc { get; set; }
    public string? IdentifiantFiscal { get; set; }
    public string? Patente { get; set; }

    // Personne physique
    public string? Civilite { get; set; }
    public string? Nom { get; set; }
    public string? Prenom { get; set; }
    public string? Cin { get; set; }
    public string? Nationalite { get; set; }
    public DateOnly? DateNaissance { get; set; }
    public string? LieuNaissance { get; set; }

    // Coordonnées
    public string? Adresse { get; set; }
    public string? Ville { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }

    /// <summary>Client exonéré de TVA : TVA à 0 % sur ses documents, la ligne reste affichée.</summary>
    public bool ExonereTva { get; set; }
    /// <summary>Attestation d'exonération de la DGI : gardée sur la fiche, jamais imprimée sur les documents.</summary>
    public string? AttestationNumero { get; set; }
    public DateOnly? AttestationExpiration { get; set; }

    public bool Archive { get; set; }
    public List<Contact> Contacts { get; set; } = [];

    public string NomAffiche => Nature != Natures.Physique
        ? RaisonSociale ?? ""
        : string.Join(' ', new[] { Civilite, Prenom, Nom }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public class Contact : Entity
{
    public Guid PartenaireId { get; set; }
    public string Nom { get; set; } = "";
    public string? Fonction { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
}
