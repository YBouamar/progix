using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Progix.Modules.Negoce.Application;
using Progix.Modules.Negoce.Domain;
using Progix.Modules.Negoce.Infrastructure;
using Progix.SharedKernel;
using static Progix.Modules.Negoce.Application.Reponses;

namespace Progix.Modules.Negoce.Endpoints;

/// <summary>
/// Signature électronique et PDF des documents de vente directe : devis, bons de livraison, factures et avoirs.
/// Comme en post-tension, un document se signe avant d'être imprimé ou envoyé ; le QR code mène à la vérification publique.
/// Les commandes de vente, document interne, ne se signent pas.
/// </summary>
public static class SignaturesEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        Groupe(app, "devis", TypesVente.Devis, "Ce devis");
        Groupe(app, "livraisons", TypesVente.Livraison, "Ce bon de livraison");
        Groupe(app, "factures", TypesVente.Facture, "Cette facture");
        Groupe(app, "avoirs", TypesVente.Avoir, "Cet avoir");
    }

    private static void Groupe(IEndpointRouteBuilder app, string segment, string type, string sujet)
    {
        var g = app.MapGroup($"/api/negoce/{segment}").WithTags("Négoce - signature et PDF").RequireAuthorization();

        g.MapPost("/{id:guid}/signer", async (Guid id, NegoceDbContext db, ClaimsPrincipal user) =>
        {
            var d = await DevisEndpoints.Charger(db, id, type, suivi: true);
            if (d is null) return Results.NotFound();
            if (d.Annule) return Conflit($"{sujet} est annulé : on ne le signe plus.");
            if (d.Jeton is not null) return Conflit($"{sujet} est déjà signé.");
            if (d.Lignes.Count == 0) return Conflit($"{sujet} n'a aucune ligne.");
            d.SignePar = user.UserName(); d.SigneLe = DateTime.UtcNow; d.Jeton = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            await db.SaveChangesAsync();
            return Results.Ok(new { id = d.Id, signePar = d.SignePar, signeLe = d.SigneLe });
        });

        g.MapGet("/{id:guid}/pdf", async (Guid id, NegoceDbContext db, IPartenairesLookup partenaires, IRenduPdfVente rendu, IConfiguration config) =>
        {
            var d = await DevisEndpoints.Charger(db, id, type, suivi: false);
            if (d is null) return Results.NotFound();
            if (d.Jeton is null) return Conflit("Ce document n'est pas encore signé : signez-le avant de l'imprimer ou de l'envoyer.");
            var client = await partenaires.LegalAsync(d.ClientId);
            var lien = $"{(config["App:PublicUrl"] ?? "http://localhost:3000").TrimEnd('/')}/verification/{d.Jeton}";
            var donnees = new DocumentVentePdf(d.Type, d.Numero, d.Entite, d.DateDocument, d.Echeance, d.ClientNom, client?.Adresse, client?.Identifiants, d.ProjetCode, d.ParentNumero, d.TauxTva,
                d.Lignes.OrderBy(l => l.CreatedAt).Select(l => new LigneVentePdf(l.Code, l.Designation, l.Unite, l.Quantite, l.PrixUnitaire, Montants.LigneHT(l.Quantite, l.PrixUnitaire))).ToList(),
                d.TotalHT, d.TotalTVA, d.TotalTTC, d.Notes, d.Annule, d.MotifAnnulation, d.SignePar ?? "", d.SigneLe ?? d.CreatedAt);
            var pdf = await rendu.RendreAsync(donnees, lien);
            return pdf is null ? Conflit("L'entité émettrice n'est pas paramétrée.") : Results.File(pdf, "application/pdf", $"{d.Numero}.pdf");
        });
    }
}
