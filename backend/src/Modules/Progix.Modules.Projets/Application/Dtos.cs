namespace Progix.Modules.Projets.Application;

public record ProjetListItem(Guid Id, string Code, string Libelle, string Client, string? Ville, DateOnly DateCreation, string Entite,
    string TypeConstruction, string TypeLabel, string Statut, string StatutLabel, bool Bloque,
    bool Supprime, string? SuppressionInfo, int NbDalles, decimal Surface, string Ingenieurs, int DallesRealisees);

public record ProjetDetail(Guid Id, string Code, string Libelle, string Entite, string EntiteLabel,
    Guid ClientId, string Client, Guid? ProprietaireId, string? ProprietaireLibre, string Proprietaire,
    string? Adresse, string? Ville, DateOnly DateCreation,
    string TypeConstruction, string TypeLabel, Guid? CommercialId, string? Commercial, string? Description,
    string Statut, string StatutLabel,
    bool Bloque, string? BlocageMotif, string? BlocagePar, DateTime? BlocageLe,
    bool Supprime, string? SuppressionMotif, string? SuppressionPar, DateTime? SuppressionLe,
    string CreePar, DateTime CreatedAt);

public record ProjetInput(string Libelle, Guid ClientId, Guid? ProprietaireId, string? ProprietaireLibre,
    string Entite, string TypeConstruction, string? Description,
    string? Adresse, string? Ville, DateOnly? DateCreation, Guid? CommercialId = null);

public record MotifInput(string? Motif);

public record ReaffectationInput(string Entite);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
