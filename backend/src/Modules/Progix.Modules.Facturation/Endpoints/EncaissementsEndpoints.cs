using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Facturation.Domain;
using Progix.Modules.Facturation.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Endpoints;

public record AnnulationInput(string? Motif);

/// <summary>Règles communes aux encaissements : plafond des espèces, annulation.</summary>
public static class EncaissementsEndpoints
{
    /// <summary>Plafond légal des règlements en espèces : par client et par mois civil.</summary>
    public const decimal PlafondEspeces = 5000m;

    /// <summary>Annulation d'un règlement : responsable administratif, chef de projet, directeur.</summary>
    public static readonly string[] RolesAnnulation = [Roles.ResponsableAdministratif, Roles.ChefProjet, Roles.Directeur];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api").WithTags("Encaissements").RequireAuthorization(Policies.Commercial);

        g.MapPost("/encaissements/{id:guid}/annuler", async (Guid id, AnnulationInput input, FacturationDbContext db, ClaimsPrincipal user) =>
        {
            var e = await db.Encaissements.FirstOrDefaultAsync(x => x.Id == id);
            if (e is null) return Results.NotFound();
            if (!RolesAnnulation.Any(user.IsInRole)) return Results.Forbid();
            if (e.Annule) return Conflit("Cet encaissement est déjà annulé.");
            if (string.IsNullOrWhiteSpace(input.Motif)) return Conflit("Le motif de l'annulation est obligatoire.");
            e.Annule = true; e.MotifAnnulation = input.Motif.Trim(); e.AnnulePar = user.UserName(); e.AnnuleLe = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = e.Id, projetId = e.ProjetId, annule = true });
        });
    }

    private static IResult Conflit(string message) => Results.Conflict(new { message });

    /// <summary>Espèces déjà encaissées pour un client sur le mois civil de la date, toutes entités confondues.</summary>
    internal static async Task<decimal> EspecesDuMois(FacturationDbContext db, Guid clientId, DateOnly date)
    {
        var debut = new DateOnly(date.Year, date.Month, 1);
        var fin = debut.AddMonths(1);
        return await db.Encaissements.Where(e => e.ClientId == clientId && !e.Annule && !e.Remboursement && e.Mode == ModesPaiement.Especes && e.DatePaiement >= debut && e.DatePaiement < fin)
            .SumAsync(e => (decimal?)e.Montant) ?? 0;
    }
}
