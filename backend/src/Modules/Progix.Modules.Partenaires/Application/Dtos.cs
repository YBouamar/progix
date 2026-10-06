namespace Progix.Modules.Partenaires.Application;

public record ContactDto(Guid? Id, string Nom, string? Fonction, string? Telephone, string? Email);

public record PartenaireListItem(Guid Id, string Type, string TypeLabel, string Nature, string Nom,
    string? Identifiant, string? Ville, string? Telephone, string? Email, bool ExonereTva, bool Archive, int NbContacts);

public record PartenaireDetail(Guid Id, string Type, string Nature, string Nom,
    string? RaisonSociale, string? Ice, string? Rc, string? IdentifiantFiscal, string? Patente,
    string? Civilite, string? NomFamille, string? Prenom, string? Cin, string? Nationalite,
    DateOnly? DateNaissance, string? LieuNaissance,
    string? Adresse, string? Ville, string? Telephone, string? Email,
    bool ExonereTva, bool Archive, DateTime CreatedAt, DateTime? UpdatedAt, List<ContactDto> Contacts,
    string? AttestationNumero = null, DateOnly? AttestationExpiration = null);

public record PartenaireInput(string Type, string Nature,
    string? RaisonSociale, string? Ice, string? Rc, string? IdentifiantFiscal, string? Patente,
    string? Civilite, string? NomFamille, string? Prenom, string? Cin, string? Nationalite,
    DateOnly? DateNaissance, string? LieuNaissance,
    string? Adresse, string? Ville, string? Telephone, string? Email,
    bool ExonereTva, List<ContactDto>? Contacts, string? AttestationNumero = null, DateOnly? AttestationExpiration = null);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
