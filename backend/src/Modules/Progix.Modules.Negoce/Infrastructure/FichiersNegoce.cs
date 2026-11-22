using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Negoce.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Negoce.Infrastructure;

/// <summary>Contextes de fichiers du négoce : scan de la facture d'un fournisseur, image du chèque ou de l'effet d'un paiement.</summary>
public static class FichiersNegoce
{
    public const string FactureFournisseur = "negoce_facture_fournisseur";
    public const string PaiementFournisseur = "negoce_paiement_fournisseur";
    public const string PaiementClient = "negoce_paiement_client";

    public static readonly string[] RolesAchats = [Roles.ResponsableAchats, Roles.ResponsableAdministratif, Roles.ChefProjet, Roles.Directeur];
    public static readonly string[] RolesVentes = [Roles.ResponsableVentesDirectes, Roles.ResponsableAdministratif, Roles.Administrateur, Roles.ChefProjet, Roles.Directeur];
}

/// <summary>Facture reçue d'un fournisseur : PDF ou image.</summary>
public class FichiersFactureFournisseur(NegoceDbContext db) : IContexteFichiers
{
    public string Contexte => FichiersNegoce.FactureFournisseur;
    public IReadOnlyList<string> Categories => [];
    public bool PeutDeposer(ClaimsPrincipal user) => FichiersNegoce.RolesAchats.Any(user.IsInRole);

    public async Task<string?> RefusAsync(Guid cibleId, string? categorie, CancellationToken ct = default) =>
        await db.FacturesFournisseur.AsNoTracking().AnyAsync(f => f.Id == cibleId, ct) ? null : "Facture du fournisseur introuvable.";
}

/// <summary>Image du chèque ou de l'effet remis à un fournisseur.</summary>
public class FichiersPaiementFournisseur(NegoceDbContext db) : IContexteFichiers
{
    public string Contexte => FichiersNegoce.PaiementFournisseur;
    public IReadOnlyList<string> Categories => [];
    public bool ImagesSeulement => true;
    public bool PeutDeposer(ClaimsPrincipal user) => FichiersNegoce.RolesAchats.Any(user.IsInRole);

    public async Task<string?> RefusAsync(Guid cibleId, string? categorie, CancellationToken ct = default)
    {
        var p = await db.PaiementsFournisseur.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cibleId, ct);
        if (p is null) return "Paiement introuvable.";
        if (p.Annule) return "Ce paiement est annulé.";
        return ModesPaiementNegoce.AvecImage(p.Mode) ? null : "L'image se joint à un paiement par chèque ou par effet.";
    }
}

/// <summary>Image du chèque ou de l'effet reçu d'un client de vente directe.</summary>
public class FichiersPaiementClient(NegoceDbContext db) : IContexteFichiers
{
    public string Contexte => FichiersNegoce.PaiementClient;
    public IReadOnlyList<string> Categories => [];
    public bool ImagesSeulement => true;
    public bool PeutDeposer(ClaimsPrincipal user) => FichiersNegoce.RolesVentes.Any(user.IsInRole);

    public async Task<string?> RefusAsync(Guid cibleId, string? categorie, CancellationToken ct = default)
    {
        var p = await db.PaiementsClient.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cibleId, ct);
        if (p is null) return "Paiement introuvable.";
        if (p.Annule) return "Ce paiement est annulé.";
        return ModesPaiementNegoce.AvecImage(p.Mode) ? null : "L'image se joint à un paiement par chèque ou par effet.";
    }
}
