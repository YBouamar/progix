using Microsoft.EntityFrameworkCore;
using Progix.Modules.Facturation.Pdf;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Infrastructure;

/// <summary>PDF des documents de vente directe : l'entité émettrice, le logo et le style sont ceux de la post-tension.</summary>
public class RenduPdfVenteFacturation(FacturationDbContext db) : IRenduPdfVente
{
    public async Task<byte[]?> RendreAsync(DocumentVentePdf document, string lienVerification, CancellationToken ct = default)
    {
        var e = await db.Entites.AsNoTracking().FirstOrDefaultAsync(x => x.Code == document.Entite, ct);
        return e is null ? null : DocumentsPdf.VenteDirecte(document, e, lienVerification);
    }
}

/// <summary>Espèces encaissées en post-tension, pour le plafond mensuel par client.</summary>
public class EspecesFacturation(FacturationDbContext db) : IEspecesClient
{
    public const string Nom = "facturation";
    public string Source => Nom;
    public Task<decimal> DuMoisAsync(Guid clientId, DateOnly date, CancellationToken ct = default) => Endpoints.EncaissementsEndpoints.EspecesDuMois(db, clientId, date);
}
