using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Infrastructure;

/// <summary>Documents généraux du projet : dépôt ouvert à tous les rôles.</summary>
public class FichiersProjet(ProjetsDbContext db) : IContexteFichiers
{
    public string Contexte => "projet";
    public IReadOnlyList<string> Categories => [];
    public bool PeutDeposer(ClaimsPrincipal user) => true;

    public async Task<string?> RefusAsync(Guid cibleId, string? categorie, CancellationToken ct = default)
    {
        var p = await db.Projets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cibleId, ct);
        return p is null ? "Projet introuvable." : p.Supprime ? "Un projet supprimé est en lecture seule." : null;
    }
}

/// <summary>Plans d'exécution : réservés aux dessinateurs et à leur chef, sur la dernière version de la dalle.</summary>
public class FichiersPlans(ProjetsDbContext db) : IContexteFichiers
{
    public static readonly string[] Roles = [SharedKernel.Roles.Dessinateur, SharedKernel.Roles.DessinateurChef, SharedKernel.Roles.Directeur, SharedKernel.Roles.ChefProjet];

    public string Contexte => VersionPlan.Contexte;
    public IReadOnlyList<string> Categories => [VersionPlan.Plan, VersionPlan.Modele, VersionPlan.Autre];
    public bool PeutDeposer(ClaimsPrincipal user) => Roles.Any(user.IsInRole);

    public async Task<string?> RefusAsync(Guid cibleId, string? categorie, CancellationToken ct = default)
    {
        var v = await db.VersionsPlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cibleId, ct);
        if (v is null) return "Version de plans introuvable.";
        var dalle = await db.Dalles.AsNoTracking().FirstAsync(d => d.Id == v.DalleId, ct);
        var projet = await db.Projets.AsNoTracking().FirstAsync(p => p.Id == dalle.ProjetId, ct);
        if (projet.Supprime) return "Un projet supprimé est en lecture seule.";
        if (dalle.EtapesFaites >= Etapes.ValidationBct) return VersionPlan.Fige;
        if (await db.VersionsPlans.AnyAsync(x => x.DalleId == v.DalleId && x.Lettre > v.Lettre, ct))
            return "Seule la dernière version des plans est modifiable.";
        return null;
    }
}

/// <summary>Pièces d'une étape du suivi, rattachées à la dalle : déposées pendant que l'étape est en cours, figées ensuite.</summary>
public abstract class FichiersEtape(ProjetsDbContext db) : IContexteFichiers
{
    public abstract string Contexte { get; }
    public abstract IReadOnlyList<string> Categories { get; }
    public virtual bool ImagesSeulement => false;
    protected abstract int Etape(string? categorie);

    public bool PeutDeposer(ClaimsPrincipal user) =>
        user.IsInRole(SharedKernel.Roles.Directeur) || user.IsInRole(SharedKernel.Roles.ChefProjet)
        || EtapesConcernees.SelectMany(n => Etapes.Get(n).Roles).Any(user.IsInRole);

    protected abstract int[] EtapesConcernees { get; }

    public async Task<string?> RefusAsync(Guid cibleId, string? categorie, CancellationToken ct = default)
    {
        var dalle = await db.Dalles.AsNoTracking().FirstOrDefaultAsync(d => d.Id == cibleId, ct);
        if (dalle is null) return "Dalle introuvable.";
        var projet = await db.Projets.AsNoTracking().FirstAsync(p => p.Id == dalle.ProjetId, ct);
        if (projet.Supprime) return "Un projet supprimé est en lecture seule.";
        var n = Etape(categorie);
        if (dalle.EtapesFaites >= n) return "Cette étape est validée : ses pièces sont figées.";
        if (dalle.EtapesFaites < n - 1) return "Cette étape n'est pas encore en cours.";
        return null;
    }
}

/// <summary>Photos des étapes 3, 5, 6 et 7. La catégorie est le numéro de l'étape.</summary>
public class FichiersPhotos(ProjetsDbContext db) : FichiersEtape(db)
{
    public const string Nom = "photo_etape";
    public static readonly int[] EtapesAvecPhoto = [3, 5, 6, 7];

    public override string Contexte => Nom;
    public override IReadOnlyList<string> Categories => ["3", "5", "6", "7"];
    public override bool ImagesSeulement => true;
    protected override int[] EtapesConcernees => EtapesAvecPhoto;
    protected override int Etape(string? categorie) => int.Parse(categorie!);
}

/// <summary>Fichier d'un rapport d'écrasement (étape 8). La cible est le rapport ; seul le dernier rapport reçoit des fichiers, tant que l'étape est en cours.</summary>
public class FichiersRapport(ProjetsDbContext db) : IContexteFichiers
{
    public const string Nom = "rapport_ecrasement";
    public const int NumeroEtape = 8;

    public string Contexte => Nom;
    public IReadOnlyList<string> Categories => [];
    public bool PeutDeposer(ClaimsPrincipal user) =>
        user.IsInRole(SharedKernel.Roles.Directeur) || user.IsInRole(SharedKernel.Roles.ChefProjet) || Etapes.Get(NumeroEtape).Roles.Any(user.IsInRole);

    public async Task<string?> RefusAsync(Guid cibleId, string? categorie, CancellationToken ct = default)
    {
        var r = await db.RapportsEcrasement.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cibleId, ct);
        if (r is null) return "Saisissez d'abord la valeur du rapport d'écrasement.";
        var dalle = await db.Dalles.AsNoTracking().FirstAsync(d => d.Id == r.DalleId, ct);
        var projet = await db.Projets.AsNoTracking().FirstAsync(p => p.Id == dalle.ProjetId, ct);
        if (projet.Supprime) return "Un projet supprimé est en lecture seule.";
        if (dalle.EtapesFaites >= NumeroEtape) return "Cette étape est validée : ses pièces sont figées.";
        if (dalle.EtapesFaites < NumeroEtape - 1) return "Cette étape n'est pas encore en cours.";
        if (await db.RapportsEcrasement.AnyAsync(x => x.DalleId == r.DalleId && x.Numero > r.Numero, ct)) return "Ce rapport a été remplacé : il est gardé tel quel dans l'historique.";
        return null;
    }
}
