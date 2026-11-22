using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Facturation.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Infrastructure;

/// <summary>Image du chèque ou de l'effet reçu, jointe au règlement. Photos seulement (JPEG, PNG, WebP, HEIC).</summary>
public class FichiersJustificatifPaiement(FacturationDbContext db) : IContexteFichiers
{
    public const string Nom = "justificatif_paiement";
    public string Contexte => Nom;
    public IReadOnlyList<string> Categories => [];
    public bool ImagesSeulement => true;
    public bool PeutDeposer(ClaimsPrincipal user) => Policies.RolesCommercial.Any(user.IsInRole);

    public async Task<string?> RefusAsync(Guid cibleId, string? categorie, CancellationToken ct = default)
    {
        var e = await db.Encaissements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cibleId, ct);
        if (e is null) return "Règlement introuvable.";
        if (e.Annule) return "Ce règlement est annulé.";
        return e.Mode is ModesPaiement.Cheque or ModesPaiement.Effet ? null : "L'image se joint à un règlement par chèque ou par effet.";
    }
}
