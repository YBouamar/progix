using System.Security.Claims;

namespace Progix.SharedKernel;

/// <summary>Accès en lecture aux clients et partenaires pour les autres modules.</summary>
public interface IPartenairesLookup
{
    Task<IReadOnlyDictionary<Guid, string>> NomsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> RechercherAsync(string texte, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, PartenaireFiche>> FichesAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    /// <summary>Identité légale d'un client, pour les contrats et les factures.</summary>
    Task<PartenaireLegal?> LegalAsync(Guid id, CancellationToken ct = default);

    /// <summary>État civil ou identité de société, pour rédiger un contrat ou un courrier.</summary>
    Task<IReadOnlyDictionary<Guid, PartenaireIdentite>> IdentitesAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}

public record PartenaireIdentite(Guid Id, string Nom, string Nature, string? Adresse, string? Ville, string? Telephone, string? Rc, string? Ice,
    string? Civilite, string? Nationalite, DateOnly? DateNaissance, string? LieuNaissance, string? Cin);

/// <summary>Accès en lecture aux utilisateurs pour les autres modules.</summary>
public interface IUtilisateursLookup
{
    /// <summary>Utilisateurs actifs ayant l'un des rôles demandés, triés par nom.</summary>
    Task<IReadOnlyList<UtilisateurFiche>> ParRolesAsync(IEnumerable<string> roles, CancellationToken ct = default);

    /// <summary>Vérifie le mot de passe d'un utilisateur, pour les signatures.</summary>
    Task<bool> VerifierMotDePasseAsync(Guid userId, string motDePasse);
}

public record UtilisateurFiche(Guid Id, string Nom, string Role);

/// <summary>Résumé d'un partenaire : nom, type et contact principal.</summary>
public record PartenaireFiche(Guid Id, string Nom, string Type, string? Contact, bool ExonereTva);

/// <summary>
/// Identité légale d'un client. <c>Manques</c> liste les mentions obligatoires absentes pour facturer (ICE, registre de commerce) ;
/// <c>ManquesContrat</c> celles absentes pour établir un contrat (en plus : identifiant fiscal, patente).
/// </summary>
public record PartenaireLegal(Guid Id, string Nom, string Nature, string? Identifiants, string? Adresse, bool ExonereTva, IReadOnlyList<string> Manques,
    IReadOnlyList<string>? ManquesContrat = null);

/// <summary>Accès en lecture aux devis validés pour la facturation.</summary>
public interface IDevisLookup
{
    Task<IReadOnlyList<DevisValide>> ValidesAsync(Guid projetId, CancellationToken ct = default);
    /// <summary>Projets apportés par un agent commercial : ceux dont un devis non annulé le désigne.</summary>
    Task<IReadOnlyList<Guid>> ProjetsDuCommercialAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Devis envoyé, expiré ou validé (dernière version, non annulée) qui chiffre la dalle sur une autre surface : « DEV-00001-2026 v2 », ou null.
    /// Une modification de surface impose alors une nouvelle version du devis.
    /// </summary>
    Task<string?> DevisSurAutreSurfaceAsync(Guid dalleId, decimal surface, CancellationToken ct = default);
}

/// <param name="PrixNet">Prix net au m² par dalle : prix du devis moins la remise au m².</param>
/// <param name="TotalTTC">Montant TTC du devis : c'est le « commandé » du reste dû.</param>
public record DevisValide(Guid Id, string Numero, int Version, DateTime ValideLe, decimal NetHT, IReadOnlyDictionary<Guid, decimal> PrixNet, decimal TotalTTC = 0);

/// <summary>Accès en lecture aux projets et à leurs dalles pour les autres modules.</summary>
public interface IProjetsLookup
{
    Task<ProjetResume?> ProjetAsync(Guid id, bool avecDalles = false, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, ProjetResume>> ProjetsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> RechercherAsync(string texte, CancellationToken ct = default);
    /// <summary>Vrai si la personne est aujourd'hui chef de chantier affecté au projet.</summary>
    Task<bool> EstChefChantierAsync(Guid projetId, Guid userId, CancellationToken ct = default);
    /// <summary>Maîtrise d'œuvre du projet : bureau d'études, bureau de contrôle, architecte, laboratoire.</summary>
    Task<IReadOnlyList<Intervenant>> IntervenantsAsync(Guid projetId, CancellationToken ct = default);
}

public record Intervenant(string Role, string RoleLabel, Guid PartenaireId);

/// <summary>
/// Type de fichier géré par un module : qui peut déposer, sur quelle cible, dans quelles catégories.
/// Le module Fichiers interroge le contexte correspondant avant chaque dépôt.
/// </summary>
public interface IContexteFichiers
{
    string Contexte { get; }

    /// <summary>Catégories acceptées. Vide : le contexte n'a pas de catégorie.</summary>
    IReadOnlyList<string> Categories { get; }

    bool PeutDeposer(ClaimsPrincipal user);

    /// <summary>Vrai si seules les images sont acceptées (photos de chantier).</summary>
    bool ImagesSeulement => false;

    /// <summary>Motif de refus pour cette cible et cette catégorie, ou null si le dépôt et la suppression sont possibles.</summary>
    Task<string?> RefusAsync(Guid cibleId, string? categorie, CancellationToken ct = default);
}

/// <summary>Accès en lecture aux fichiers déposés pour les autres modules.</summary>
public interface IFichiersLookup
{
    /// <summary>Catégories ayant au moins un fichier pour cette cible.</summary>
    Task<IReadOnlyList<string>> CategoriesAsync(string contexte, Guid cibleId, CancellationToken ct = default);
    Task<int> NombreAsync(string contexte, Guid cibleId, CancellationToken ct = default);
}

/// <summary>
/// Réaffectation d'un projet à une autre entité : chaque module renumérote ses documents dans la séquence de la nouvelle entité.
/// </summary>
public interface IReaffectationEntite
{
    /// <param name="projet">Le projet après réaffectation, avec son nouveau code et les nouvelles références de ses dalles.</param>
    Task ReaffecterAsync(ProjetResume projet, string ancienneEntite, string par, CancellationToken ct = default);
}

/// <summary>Changements de statut d'un projet déclenchés par les autres modules.</summary>
public interface IProjetsStatut
{
    /// <summary>Un projet « Créé » passe « En étude » à la validation d'un devis.</summary>
    Task PasserEnEtudeAsync(Guid projetId, CancellationToken ct = default);

    /// <summary>Un projet « En étude » revient à « Créé » quand il n'a plus aucun devis validé.</summary>
    Task RevenirCreeAsync(Guid projetId, CancellationToken ct = default);

    /// <summary>
    /// Surface d'une dalle modifiée depuis un devis en brouillon : la dalle reste la seule source de la surface.
    /// Renvoie le motif de refus (projet supprimé, surface invalide, dalle chiffrée dans un autre devis émis), ou null si la surface est enregistrée.
    /// </summary>
    Task<string?> ModifierSurfaceDalleAsync(Guid projetId, Guid dalleId, decimal surface, CancellationToken ct = default);

    /// <summary>Reprise : le commercial choisi jusqu'ici sur un devis devient celui du projet, si le projet n'en a pas encore.</summary>
    Task DefinirCommercialSiAbsentAsync(Guid projetId, Guid commercialId, string commercialNom, CancellationToken ct = default);
}

/// <param name="CommercialId">Agent commercial du projet, choisi sur le projet ; les devis le reprennent.</param>
public record ProjetResume(Guid Id, string Code, string Libelle, string Entite, Guid ClientId, string TypeConstruction,
    bool Supprime, IReadOnlyList<DalleResume> Dalles, bool Bloque = false, string? Ville = null,
    Guid? CommercialId = null, string? CommercialNom = null, string? TypeLabel = null);

/// <param name="Conforme">Avis de conformité d'une dalle réalisée : vrai, faux, ou null tant qu'il n'est pas donné.</param>
/// <param name="SurfaceCommande">Surface totale saisie sur la commande usine : celle du bon de livraison et de la facture.</param>
public record DalleResume(Guid Id, string Reference, string Emplacement, decimal Surface, int EtapesFaites = 0, bool? Conforme = null,
    AdaptResume? Adapt = null, decimal? SurfaceCommande = null);

/// <summary>Données ADAPT d'une dalle, telles qu'un devis les reprend.</summary>
public record AdaptResume(decimal RatioActif, decimal RatioPassif, IReadOnlyList<SystemeAdaptResume> Systemes);

/// <param name="Ancrages">Nombre de câbles du type d'ancrage.</param>
/// <param name="Gaine">Cumul de Strand Length, en mètres.</param>
public record SystemeAdaptResume(int Torons, int Ancrages, decimal Gaine);

/// <summary>
/// Vérification publique d'un document signé, atteinte par son QR code. Chaque module qui signe des documents
/// en fournit une implémentation ; le jeton est aléatoire et propre au document.
/// </summary>
public interface IVerificationDocuments
{
    Task<DocumentVerifie?> TrouverAsync(string jeton, CancellationToken ct = default);
}

/// <param name="Annule">Mention à afficher si le document n'a plus de valeur, sinon null.</param>
public record DocumentVerifie(string Type, string Numero, string Entite, IReadOnlyList<KeyValuePair<string, string>> Lignes, string SignePar, DateTime? SigneLe, string? Annule);

/// <summary>Contenu d'un devis pour son impression : le module Devis le fournit, le PDF est mis en page ailleurs.</summary>
public interface IDevisDocuments
{
    Task<DevisDocument?> DocumentAsync(Guid id, CancellationToken ct = default);
}

public record DevisDocument(Guid Id, string Numero, int Version, string Entite, Guid ProjetId, string Etat, string EtatLabel, DateOnly DateDevis, DateOnly Echeance,
    string? Commercial, IReadOnlyList<DevisDocumentLigne> Lignes, decimal SurfaceTotale, decimal TotalHT, decimal RemiseM2, decimal RemiseTotale, decimal NetHT,
    bool ExonereTva, decimal Tva, decimal TotalTTC, IReadOnlyList<KeyValuePair<string, decimal>> Echeances, string? Modalites, string? SignePar, DateTime? SigneLe, string? Jeton);

public record DevisDocumentLigne(string Reference, string Emplacement, decimal Surface, decimal RatioActif, decimal RatioPassif, int PrixM2, decimal MontantHT);

/// <summary>Fiches de chantier d'une dalle (auto-contrôle, mise en tension, injection), prêtes à imprimer.</summary>
public interface IFichesChantier
{
    Task<FicheChantier?> FicheAsync(Guid dalleId, string type, CancellationToken ct = default);
}

/// <param name="Figee">Vrai une fois l'étape validée : la fiche ne change plus et peut être signée.</param>
/// <param name="Lignes">Lignes du tableau ; une ligne d'une seule cellule est un titre de section.</param>
public record FicheChantier(string Type, string Titre, Guid DalleId, Guid ProjetId, string Reference, string Emplacement, int Etape, bool Figee, string? FaitPar, DateTime? FaitLe,
    string? Remarque, IReadOnlyList<KeyValuePair<string, string>> Entete, IReadOnlyList<string> Colonnes, IReadOnlyList<IReadOnlyList<string>> Lignes,
    IReadOnlyList<KeyValuePair<string, string>> Synthese, string? ClientNom, string? ClientSignature, IReadOnlyList<string> RolesSignature);

/// <summary>Factures non soldées d'un projet, pour le rappel de paiement avant l'injection.</summary>
public interface IEncoursProjet
{
    Task<EncoursProjet> EncoursAsync(Guid projetId, CancellationToken ct = default);
}

/// <param name="Factures">Nombre de factures non soldées.</param>
/// <param name="Echues">Parmi elles, celles dont l'échéance est passée.</param>
public record EncoursProjet(int Factures, int Echues, decimal Reste, decimal ResteEchu);

/// <summary>
/// Courriels d'information envoyés aux directeurs (étapes clés du chantier, nouveaux paiements).
/// L'envoi se fait en arrière-plan : une panne de messagerie ne bloque jamais l'action qui l'a déclenché.
/// </summary>
public interface INotificationsDirection
{
    /// <param name="lien">Chemin dans PROGIX à ouvrir depuis le courriel, par exemple /projets/{id}.</param>
    Task SignalerAsync(string sujet, string titre, IReadOnlyList<KeyValuePair<string, string>> lignes, string? lien, CancellationToken ct = default);
}

/// <summary>
/// Types d'ancrage (systèmes) existants : S1, S2, S3, S4, S7, S9, S12, S15. Le nombre est celui des torons du système.
/// Seuls ces types se saisissent sur un devis ou une commande usine ; un type déjà enregistré sur un document reste accepté.
/// </summary>
public static class SystemesAncrage
{
    public static readonly IReadOnlyList<int> Torons = [1, 2, 3, 4, 7, 9, 12, 15];

    public static bool Existe(int torons) => Torons.Contains(torons);

    /// <summary>Liste lisible pour les messages : « S1, S2, S3, S4, S7, S9, S12, S15 ».</summary>
    public static string Liste => string.Join(", ", Torons.Select(t => $"S{t}"));
}

/// <summary>Entités émettrices des documents.</summary>
public static class Entites
{
    public const string Apt = "APT";
    public const string Bbr = "BBR";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Apt] = "Africa PT / MK4",
        [Bbr] = "BECOMAR",
    };
}

public static class ClaimsExtensions
{
    public static Guid UserId(this ClaimsPrincipal u) => Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static string UserName(this ClaimsPrincipal u) => u.FindFirstValue(ClaimTypes.Name) ?? "";
    public static string RoleLabel(this ClaimsPrincipal u) => Roles.Find(u.FindFirstValue(ClaimTypes.Role) ?? "")?.Label ?? "";
}
