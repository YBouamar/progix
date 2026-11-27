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

    /// <summary>Personnes ayant l'un des rôles demandés, comptes désactivés compris, avec leurs dates d'arrivée et de départ.</summary>
    Task<IReadOnlyList<Presence>> PresencesAsync(IEnumerable<string> roles, CancellationToken ct = default);

    /// <summary>Vérifie le mot de passe d'un utilisateur, pour les signatures.</summary>
    Task<bool> VerifierMotDePasseAsync(Guid userId, string motDePasse);
}

public record UtilisateurFiche(Guid Id, string Nom, string Role);

/// <summary>Présence d'une personne dans l'entreprise : sert au calcul des mois travaillés.</summary>
public record Presence(Guid Id, string Nom, string Role, bool Actif, DateOnly? Arrivee, DateOnly? Depart);

/// <summary>Résumé d'un partenaire : nom, type et contact principal.</summary>
public record PartenaireFiche(Guid Id, string Nom, string Type, string? Contact, bool ExonereTva);

/// <summary>
/// Identité légale d'un client. <c>Manques</c> liste les mentions obligatoires absentes pour facturer ou établir un contrat (ICE, registre de commerce) ;
/// l'identifiant fiscal et la patente (ITP) n'empêchent ni l'un ni l'autre.
/// </summary>
public record PartenaireLegal(Guid Id, string Nom, string Nature, string? Identifiants, string? Adresse, bool ExonereTva, IReadOnlyList<string> Manques);

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

/// <summary>
/// Usage d'une dalle dans un module (devis, facturation...) : sert à refuser la suppression d'une dalle déjà engagée.
/// Chaque module qui référence les dalles l'implémente ; la réponse est une phrase prête à afficher, ou null si la dalle n'y figure pas.
/// </summary>
public interface IUsagesDalle
{
    /// <summary>Phrase de refus, ou null : la dalle peut être supprimée (ce module peut devoir l'en retirer, voir <see cref="RetirerAsync"/>).</summary>
    Task<string?> UsageAsync(Guid dalleId, CancellationToken ct = default);

    /// <summary>Appelée juste avant la suppression de la dalle, une fois tous les refus écartés : le module retire la dalle de ses documents encore modifiables.</summary>
    Task RetirerAsync(Guid dalleId, string par, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>
/// Engagement d'un projet dans un module (devis envoyé, facture, paiement...) : sert à refuser qu'un ingénieur supprime un projet déjà avancé.
/// La réponse est une phrase prête à afficher, ou null si le module n'a rien d'engagé sur ce projet.
/// </summary>
public interface IUsagesProjet
{
    Task<string?> UsageAsync(Guid projetId, CancellationToken ct = default);
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
    /// <summary>Projets non supprimés absents de la liste donnée (ceux qui ont déjà un devis), les plus récents d'abord : les devis à créer.</summary>
    Task<IReadOnlyList<ProjetSansDevis>> SansDevisAsync(IReadOnlyCollection<Guid> avecDevis, CancellationToken ct = default);
    /// <summary>Vrai si la personne est aujourd'hui chef de chantier affecté au projet.</summary>
    Task<bool> EstChefChantierAsync(Guid projetId, Guid userId, CancellationToken ct = default);
    /// <summary>Maîtrise d'œuvre du projet : bureau d'études, bureau de contrôle, architecte, laboratoire.</summary>
    Task<IReadOnlyList<Intervenant>> IntervenantsAsync(Guid projetId, CancellationToken ct = default);
}

public record Intervenant(string Role, string RoleLabel, Guid PartenaireId);

/// <param name="Dalles">Nombre de dalles du projet : un devis ne se crée qu'avec au moins une dalle.</param>
public record ProjetSansDevis(Guid Id, string Code, string Libelle, string Entite, Guid ClientId, Guid? CommercialId, string? CommercialNom, int Dalles, decimal Surface, DateTime Cree);

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

    /// <summary>Même contrôle pour une personne donnée, quand le dépôt est réservé à certaines personnes de la cible. Par défaut : le contrôle général.</summary>
    Task<string?> RefusPourAsync(ClaimsPrincipal user, Guid cibleId, string? categorie, CancellationToken ct = default) => RefusAsync(cibleId, categorie, ct);
}

/// <summary>Accès en lecture aux fichiers déposés pour les autres modules.</summary>
public interface IFichiersLookup
{
    /// <summary>Catégories ayant au moins un fichier pour cette cible.</summary>
    Task<IReadOnlyList<string>> CategoriesAsync(string contexte, Guid cibleId, CancellationToken ct = default);
    Task<int> NombreAsync(string contexte, Guid cibleId, CancellationToken ct = default);
}

/// <summary>Contenu des fichiers pour les autres modules : lire une pièce déposée, déposer une pièce produite par l'application.</summary>
public interface IFichiersContenu
{
    /// <summary>Fichiers complets et non supprimés d'une cible, les plus récents d'abord.</summary>
    Task<IReadOnlyList<FichierResume>> ListerAsync(string contexte, Guid cibleId, string? categorie, CancellationToken ct = default);

    /// <summary>Contenu d'un fichier, copié en mémoire, ou null s'il n'existe pas dans ce contexte.</summary>
    Task<(FichierResume Fichier, MemoryStream Contenu)?> LireAsync(string contexte, Guid id, CancellationToken ct = default);

    /// <summary>Dépose un fichier produit par l'application, au nom de la personne qui l'a demandé. Les droits sont vérifiés par l'appelant.</summary>
    Task<FichierResume> DeposerAsync(string contexte, Guid cibleId, string? categorie, string nom, string contentType, MemoryStream contenu, ClaimsPrincipal par, CancellationToken ct = default);
}

public record FichierResume(Guid Id, Guid CibleId, string? Categorie, string Nom, long Taille, DateTime Date);

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
    Guid? CommercialId = null, string? CommercialNom = null, string? TypeLabel = null, decimal? ForfaitPropre = null, decimal? ForfaitType = null, string? Statut = null)
{
    /// <summary>Projet lancé : en réalisation ou terminé. Un devis peut encore s'y ajouter, en variante, et être validé par le client.</summary>
    public bool Lance => Statut is "realisation" or "termine";

    /// <summary>Forfait imposé au devis (DH/m²) : celui du projet, sinon celui de son type de construction ; null si la grille par surface s'applique.</summary>
    public decimal? ForfaitImpose => ForfaitPropre is > 0 ? ForfaitPropre : ForfaitType is > 0 ? ForfaitType : null;
}

/// <param name="Conforme">Avis de conformité d'une dalle réalisée : vrai, faux, ou null tant qu'il n'est pas donné.</param>
/// <param name="SurfaceCommande">Surface totale saisie sur la commande usine : celle du bon de livraison et de la facture.</param>
public record DalleResume(Guid Id, string Reference, string Emplacement, decimal Surface, int EtapesFaites = 0, bool? Conforme = null,
    AdaptResume? Adapt = null, decimal? SurfaceCommande = null);

/// <summary>Données ADAPT d'une dalle, telles qu'un devis les reprend.</summary>
/// <param name="AvecPt">PTTotal lu : ratio actif et câbles connus. Les deux exports peuvent arriver séparément.</param>
/// <param name="AvecRebar">RebarTotal lu : ratio passif connu.</param>
public record AdaptResume(decimal RatioActif, decimal RatioPassif, IReadOnlyList<SystemeAdaptResume> Systemes, bool AvecPt = true, bool AvecRebar = true);

/// <param name="Ancrages">Nombre de câbles du type d'ancrage.</param>
/// <param name="Gaine">Cumul de Tendon/Duct Length, en mètres.</param>
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
    /// <summary>Contexte de fichiers des documents joints à un devis (plans de principe).</summary>
    public const string ContexteDocuments = "plan_principe";

    /// <summary>Contexte de fichiers du devis signé et cacheté par le client, déposé après la validation.</summary>
    public const string ContexteSigne = "devis_signe";

    Task<DevisDocument?> DocumentAsync(Guid id, CancellationToken ct = default);
}

public record DevisDocument(Guid Id, string Numero, int Version, string Entite, Guid ProjetId, string Etat, string EtatLabel, DateOnly DateDevis, DateOnly Echeance,
    string? Commercial, IReadOnlyList<DevisDocumentLigne> Lignes, decimal SurfaceTotale, decimal TotalHT, decimal RemiseM2, decimal RemiseTotale, decimal NetHT,
    bool ExonereTva, decimal Tva, decimal TotalTTC, IReadOnlyList<KeyValuePair<string, decimal>> Echeances, string? Modalites, string? SignePar, DateTime? SigneLe, string? Jeton, decimal? RatioPassifSaisi = null)
{
    /// <summary>Ratio passif moyen du devis en kg/m², pondéré par la surface des dalles ; 0 sans surface.</summary>
    public decimal RatioPassifMoyen => Lignes.Sum(l => l.Surface) is > 0 and var s ? Lignes.Sum(l => l.RatioPassif * l.Surface) / s : 0;

    /// <summary>Ratio passif des conditions de l'offre : celui saisi s'il existe, sinon le ratio moyen calculé (0 : rien à afficher).</summary>
    public decimal RatioPassifAffiche => RatioPassifSaisi is > 0 ? RatioPassifSaisi.Value : RatioPassifMoyen;

    /// <summary>
    /// Ratios passifs moyens des conditions de l'offre. Chaque dalle a le sien (le ratio du devis par défaut, ou celui saisi pour elle).
    /// Tous égaux : un seul ratio (Commun), sans exception. Sinon : le ratio du devis, puis chaque dalle qui en diffère.
    /// </summary>
    public (decimal Commun, IReadOnlyList<(string Reference, decimal Ratio)> Exceptions) RatiosPassifsOffre
    {
        get
        {
            var commun = Math.Round(RatioPassifAffiche, 2);
            var parDalle = Lignes.Select(l => (l.Reference, Ratio: Math.Round(l.RatioPassifMoyen ?? commun, 2))).ToList();
            if (parDalle.Count > 0 && parDalle.All(x => x.Ratio == parDalle[0].Ratio)) return (parDalle[0].Ratio, []);
            return (commun, parDalle.Where(x => x.Ratio != commun).ToList());
        }
    }
}

/// <param name="RatioPassifMoyen">Ratio passif moyen saisi pour la dalle (conditions de l'offre) ; null : celui du devis.</param>
public record DevisDocumentLigne(string Reference, string Emplacement, decimal Surface, decimal RatioActif, decimal RatioPassif, int PrixM2, decimal MontantHT, decimal? RatioPassifMoyen = null);

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
