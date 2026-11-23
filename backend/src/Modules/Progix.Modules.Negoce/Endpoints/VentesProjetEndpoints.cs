using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Negoce.Application;
using Progix.Modules.Negoce.Domain;
using Progix.Modules.Negoce.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Negoce.Endpoints;

/// <summary>Les ventes directes rattachées à un projet, pour l'onglet de sa fiche : chaque bloc n'est rendu qu'avec le droit de lecture du module concerné.</summary>
public static class VentesProjetEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/projets/{projetId:guid}/ventes-directes", async (Guid projetId, NegoceDbContext db, DroitsNegoce droits, ClaimsPrincipal user) =>
        {
            async Task<bool> Peut(string module) => await droits.NiveauAsync(user, module) >= Habilitations.Lecture;
            var voitDevis = await Peut(Habilitations.NegoceDevis);
            var voitLivraisons = await Peut(Habilitations.NegoceLivraisons);
            var voitFactures = await Peut(Habilitations.NegoceFactures);
            if (!voitDevis && !voitLivraisons && !voitFactures) return Results.Forbid();

            var documents = await db.Documents.AsNoTracking().Include(d => d.Lignes).Include(d => d.Paiements).AsSplitQuery()
                .Where(d => d.ProjetId == projetId).OrderByDescending(d => d.DateDocument).ThenByDescending(d => d.Numero).ToListAsync();
            var avoirs = documents.Where(d => d.Type == TypesVente.Avoir).ToList();

            var factures = documents.Where(d => d.Type == TypesVente.Facture).ToList();
            var paiements = factures.SelectMany(f => f.Paiements.Select(p => new
            {
                id = p.Id, factureId = f.Id, facture = f.Numero, date = p.DatePaiement, montant = p.Montant, mode = p.Mode, reference = p.Reference, annule = p.Annule,
            })).OrderByDescending(p => p.date).ToList();

            return Results.Ok(new
            {
                devis = voitDevis ? documents.Where(d => d.Type == TypesVente.Devis).Select(VentesSupport.Resume) : null,
                commandes = voitDevis ? documents.Where(d => d.Type == TypesVente.Commande).Select(VentesSupport.Resume) : null,
                livraisons = voitLivraisons ? documents.Where(d => d.Type == TypesVente.Livraison).Select(VentesSupport.Resume) : null,
                factures = voitFactures ? factures.Select(f =>
                {
                    var reste = VentesSupport.ResteAPayer(f, avoirs.Where(a => a.ParentId == f.Id));
                    return new { resume = VentesSupport.Resume(f), encaisse = f.Encaisse, reste = f.Annule ? 0 : reste, statut = VentesSupport.StatutPaiement(f, reste) };
                }) : null,
                avoirs = voitFactures ? avoirs.Select(VentesSupport.Resume) : null,
                paiements = voitFactures ? paiements : null,
            });
        }).RequireAuthorization().WithTags("Négoce - projet");
    }
}
