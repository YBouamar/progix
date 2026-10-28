using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Progix.Modules.Facturation.Domain;
using Progix.Modules.Facturation.Infrastructure;
using Progix.Modules.Facturation.Pdf;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Endpoints;

public record ContratInput(Guid? DevisId, DateOnly DateContrat, string? Lieu);

public static class ContratsEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api").WithTags("Contrats").RequireAuthorization(Policies.Commercial);

        g.MapGet("/projets/{id:guid}/contrats", async (Guid id, FacturationDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires, IDevisLookup devis, ClaimsPrincipal user) =>
        {
            var p = await projets.ProjetAsync(id);
            if (p is null) return Results.NotFound();
            var valides = await devis.ValidesAsync(id);
            var contrats = await db.Contrats.AsNoTracking().Where(c => c.ProjetId == id).OrderByDescending(c => c.CreatedAt).ToListAsync();
            return Results.Ok(new
            {
                refus = await Refus(p, valides, partenaires),
                peutAnnuler = EncaissementsEndpoints.RolesAnnulation.Any(user.IsInRole),
                devis = valides.Select(d => new { id = d.Id, numero = d.Numero, version = d.Version, netHT = d.NetHT }),
                contrats = contrats.Select(Dto),
            });
        });

        g.MapPost("/projets/{id:guid}/contrats", async (Guid id, ContratInput input, FacturationDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires, IDevisLookup devis, ClaimsPrincipal user) =>
        {
            var p = await projets.ProjetAsync(id);
            if (p is null) return Results.NotFound();
            var valides = await devis.ValidesAsync(id);
            if (await Refus(p, valides, partenaires) is { } refus) return Conflit(refus);
            var d = valides.FirstOrDefault(x => x.Id == input.DevisId) ?? (valides.Count == 1 ? valides[0] : null);
            if (d is null) return Conflit("Choisissez le devis validé sur lequel porte le contrat.");
            if (await db.Contrats.AnyAsync(c => c.DevisId == d.Id && !c.Annule)) return Conflit($"Un contrat existe déjà sur le devis {d.Numero}. Annulez-le avant d'en établir un autre.");
            var annee = input.DateContrat.Year;
            var c = new Contrat
            {
                Entite = p.Entite, ProjetId = p.Id, DevisId = d.Id, DevisNumero = $"{d.Numero}, version {d.Version}", DateContrat = input.DateContrat,
                Lieu = string.IsNullOrWhiteSpace(input.Lieu) ? "Casablanca" : input.Lieu.Trim(), EmisPar = user.UserName(),
                Numero = $"CTR-{await db.NumeroSuivantAsync($"contrat:{p.Entite}:{annee}"):00000}-{annee}",
            };
            db.Contrats.Add(c);
            await db.SaveChangesAsync();
            return Results.Ok(Dto(c));
        });

        g.MapPost("/contrats/{id:guid}/signer", async (Guid id, SignatureInput input, FacturationDbContext db, IUtilisateursLookup users, ClaimsPrincipal user) =>
        {
            var c = await db.Contrats.FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return Results.NotFound();
            if (c.Annule) return Conflit("Ce contrat est annulé.");
            if (c.Jeton is not null) return Conflit("Ce contrat est déjà signé.");
            if (string.IsNullOrEmpty(input.MotDePasse) || !await users.VerifierMotDePasseAsync(user.UserId(), input.MotDePasse))
                return Conflit("Mot de passe incorrect : il signe électroniquement le document.");
            c.SignePar = user.UserName(); c.SigneLe = DateTime.UtcNow; c.Jeton = PdfEndpoints.NouveauJeton();
            await db.SaveChangesAsync();
            return Results.Ok(Dto(c));
        });

        g.MapPost("/contrats/{id:guid}/annuler", async (Guid id, AnnulationInput input, FacturationDbContext db, ClaimsPrincipal user) =>
        {
            if (!EncaissementsEndpoints.RolesAnnulation.Any(user.IsInRole)) return Results.Forbid();
            var c = await db.Contrats.FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return Results.NotFound();
            if (c.Annule) return Conflit("Ce contrat est déjà annulé.");
            if (string.IsNullOrWhiteSpace(input.Motif)) return Conflit("Le motif de l'annulation est obligatoire.");
            c.Annule = true; c.MotifAnnulation = input.Motif.Trim(); c.AnnulePar = user.UserName(); c.AnnuleLe = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(Dto(c));
        });

        g.MapGet("/contrats/{id:guid}/pdf", async (Guid id, FacturationDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires, IDevisDocuments devis, IConfiguration config) =>
        {
            var c = await db.Contrats.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return Results.NotFound();
            if (c.Jeton is null) return Conflit("Ce document n'est pas encore signé : signez-le avant de l'imprimer ou de l'envoyer.");
            var e = await db.Entites.AsNoTracking().FirstOrDefaultAsync(x => x.Code == c.Entite);
            var p = await projets.ProjetAsync(c.ProjetId);
            var d = await devis.DocumentAsync(c.DevisId);
            if (e is null || p is null || d is null) return Conflit("Projet, devis ou entité introuvable pour ce contrat.");
            var intervenants = await projets.IntervenantsAsync(p.Id);
            var identites = await partenaires.IdentitesAsync(intervenants.Select(i => i.PartenaireId).Append(p.ClientId));
            if (!identites.TryGetValue(p.ClientId, out var client)) return Conflit("Client introuvable pour ce contrat.");
            var moe = intervenants.Where(i => identites.ContainsKey(i.PartenaireId)).Select(i => (i.Role, identites[i.PartenaireId])).ToList();
            return Results.File(ContratPdf.Generer(c, p, client, moe, d, e, PdfEndpoints.Lien(config, c.Jeton)), "application/pdf", $"{c.Numero}.pdf");
        });
    }

    /// <summary>Un contrat s'établit sur un devis validé ; pour une personne morale, ses mentions légales doivent être complètes.</summary>
    private static async Task<string?> Refus(ProjetResume p, IReadOnlyList<DevisValide> valides, IPartenairesLookup partenaires)
    {
        if (p.Supprime) return "Un projet supprimé est en lecture seule.";
        if (valides.Count == 0) return "Aucun devis validé pour ce projet : le contrat s'établit sur un devis validé.";
        var client = await partenaires.LegalAsync(p.ClientId);
        if (client is null) return "Client introuvable.";
        var manques = client.ManquesContrat ?? client.Manques;
        return manques.Count > 0 ? $"Fiche du client incomplète pour établir un contrat : il manque {string.Join(", ", manques)}." : null;
    }

    private static IResult Conflit(string message) => Results.Conflict(new { message });

    private static object Dto(Contrat c) => new
    {
        id = c.Id, numero = c.Numero, ancienNumero = c.AncienNumero, devisNumero = c.DevisNumero, dateContrat = c.DateContrat, lieu = c.Lieu, emisPar = c.EmisPar,
        signePar = c.SignePar, signeLe = c.SigneLe, annule = c.Annule, motifAnnulation = c.MotifAnnulation, annulePar = c.AnnulePar,
    };
}
