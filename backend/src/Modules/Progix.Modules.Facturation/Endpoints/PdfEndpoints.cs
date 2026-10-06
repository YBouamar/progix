using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Progix.Modules.Facturation.Infrastructure;
using Progix.Modules.Facturation.Pdf;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Endpoints;

public record SignatureInput(string? MotDePasse);

public static class PdfEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api").WithTags("Documents PDF").RequireAuthorization(Policies.Commercial);

        g.MapGet("/factures/{id:guid}/pdf", async (Guid id, FacturationDbContext db, IConfiguration config) =>
        {
            var f = await db.Factures.AsNoTracking().Include(x => x.Lignes).FirstOrDefaultAsync(x => x.Id == id);
            if (f is null) return Results.NotFound();
            var e = await db.Entites.AsNoTracking().FirstOrDefaultAsync(x => x.Code == f.Entite);
            if (e is null) return Results.Conflict(new { message = "L'entité émettrice n'est pas paramétrée." });
            // Dates de livraison : celles des bons rattachés aux dalles facturées.
            var dalles = f.Lignes.Where(l => l.DalleId != null).Select(l => l.DalleId!.Value).ToList();
            var livraisons = dalles.Count == 0 ? [] : await db.BonsLivraison.AsNoTracking().Where(b => b.Lignes.Any(l => dalles.Contains(l.DalleId))).Select(b => b.DateLivraison).ToListAsync();
            if (f.Jeton is null) return NonSigne();
            return Results.File(DocumentsPdf.Facture(f, e, livraisons, Lien(config, f.Jeton)), "application/pdf", $"{f.Numero}.pdf");
        });

        g.MapGet("/encaissements/{id:guid}/recu", async (Guid id, FacturationDbContext db, IConfiguration config) =>
        {
            var x = await db.Encaissements.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id);
            if (x is null) return Results.NotFound();
            if (x.NumeroRecu is null || x.Jeton is null) return Results.Conflict(new { message = "Ce règlement n'a pas de reçu signé." });
            var f = await db.Factures.AsNoTracking().Include(v => v.Encaissements).Include(v => v.Avoirs).FirstAsync(v => v.Id == x.FactureId);
            var e = await db.Entites.AsNoTracking().FirstOrDefaultAsync(v => v.Code == f.Entite);
            if (e is null) return Results.Conflict(new { message = "L'entité émettrice n'est pas paramétrée." });
            return Results.File(DocumentsPdf.Recu(x, f, e, Lien(config, x.Jeton)), "application/pdf", $"{x.NumeroRecu}.pdf");
        });

        // Signature électronique : le mot de passe de la personne connectée, exigé avant toute impression ou tout envoi.
        g.MapPost("/factures/{id:guid}/signer", async (Guid id, SignatureInput input, FacturationDbContext db, IUtilisateursLookup users, ClaimsPrincipal user) =>
        {
            var f = await db.Factures.Include(x => x.Lignes).Include(x => x.Encaissements).Include(x => x.Avoirs).FirstOrDefaultAsync(x => x.Id == id);
            if (f is null) return Results.NotFound();
            if (f.Jeton is not null) return Results.Conflict(new { message = "Cette facture est déjà signée." });
            if (!await MotDePasseValide(users, user, input)) return MauvaisMotDePasse();
            f.SignePar = user.UserName(); f.SigneLe = DateTime.UtcNow; f.Jeton = NouveauJeton();
            await db.SaveChangesAsync();
            return Results.Ok(FacturesEndpoints.Dto(f, true));
        });

        g.MapPost("/avoirs/{id:guid}/signer", async (Guid id, SignatureInput input, FacturationDbContext db, IUtilisateursLookup users, ClaimsPrincipal user) =>
        {
            var a = await db.Avoirs.FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            if (a.Jeton is not null) return Results.Conflict(new { message = "Cet avoir est déjà signé." });
            if (!await MotDePasseValide(users, user, input)) return MauvaisMotDePasse();
            a.SignePar = user.UserName(); a.SigneLe = DateTime.UtcNow; a.Jeton = NouveauJeton();
            await db.SaveChangesAsync();
            var f = await db.Factures.AsNoTracking().Include(x => x.Lignes).Include(x => x.Encaissements).Include(x => x.Avoirs).FirstAsync(x => x.Id == a.FactureId);
            return Results.Ok(FacturesEndpoints.Dto(f, true));
        });

        g.MapPost("/livraisons/{id:guid}/signer", async (Guid id, SignatureInput input, FacturationDbContext db, IUtilisateursLookup users, ClaimsPrincipal user) =>
        {
            var b = await db.BonsLivraison.FirstOrDefaultAsync(x => x.Id == id);
            if (b is null) return Results.NotFound();
            if (b.Jeton is not null) return Results.Conflict(new { message = "Ce bon de livraison est déjà signé." });
            if (!await MotDePasseValide(users, user, input)) return MauvaisMotDePasse();
            b.SignePar = user.UserName(); b.SigneLe = DateTime.UtcNow; b.Jeton = NouveauJeton();
            await db.SaveChangesAsync();
            return Results.Ok(new { id = b.Id, signePar = b.SignePar, signeLe = b.SigneLe });
        });

        g.MapGet("/livraisons/{id:guid}/pdf", async (Guid id, FacturationDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires, IConfiguration config) =>
        {
            var b = await db.BonsLivraison.AsNoTracking().Include(x => x.Lignes).FirstOrDefaultAsync(x => x.Id == id);
            if (b is null) return Results.NotFound();
            if (b.Jeton is null) return NonSigne();
            var e = await db.Entites.AsNoTracking().FirstOrDefaultAsync(x => x.Code == b.Entite);
            var p = await projets.ProjetAsync(b.ProjetId);
            var client = p is null ? null : await partenaires.LegalAsync(p.ClientId);
            if (e is null || p is null || client is null) return Results.Conflict(new { message = "Projet, client ou entité introuvable pour ce bon." });
            return Results.File(DocumentsPdf.Livraison(b, p, client, e, Lien(config, b.Jeton)), "application/pdf", $"{b.Numero}.pdf");
        });

        // Le PDF d'un devis est ouvert aux rôles qui voient les devis.
        app.MapGet("/api/devis/{id:guid}/pdf", async (Guid id, FacturationDbContext db, IDevisDocuments devis, IProjetsLookup projets, IPartenairesLookup partenaires, IConfiguration config) =>
        {
            var d = await devis.DocumentAsync(id);
            if (d is null) return Results.NotFound();
            if (d.Jeton is null) return NonSigne();
            var e = await db.Entites.AsNoTracking().FirstOrDefaultAsync(x => x.Code == d.Entite);
            var p = await projets.ProjetAsync(d.ProjetId);
            var client = p is null ? null : await partenaires.LegalAsync(p.ClientId);
            if (e is null || p is null || client is null) return Results.Conflict(new { message = "Projet, client ou entité introuvable pour ce devis." });
            return Results.File(DocumentsPdf.Devis(d, p, client, e, Lien(config, d.Jeton)), "application/pdf", $"{d.Numero}-v{d.Version}.pdf");
        }).WithTags("Documents PDF").RequireAuthorization(Policies.Devis);

        // Vérification publique d'un document signé, atteinte par son QR code : aucun compte n'est nécessaire, seul le jeton du document donne accès.
        app.MapGet("/api/verification/{jeton}", async (string jeton, IEnumerable<IVerificationDocuments> sources) =>
        {
            foreach (var source in sources)
                if (await source.TrouverAsync(jeton) is { } d)
                    return Results.Ok(new { type = d.Type, numero = d.Numero, entite = d.Entite, lignes = d.Lignes.Select(l => new { label = l.Key, valeur = l.Value }), signePar = d.SignePar, signeLe = d.SigneLe, annule = d.Annule });
            return Results.NotFound(new { message = "Aucun document ne correspond à ce code." });
        }).WithTags("Documents PDF").AllowAnonymous();

        g.MapGet("/avoirs/{id:guid}/pdf", async (Guid id, FacturationDbContext db, IConfiguration config) =>
        {
            var a = await db.Avoirs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            var f = await db.Factures.AsNoTracking().FirstAsync(x => x.Id == a.FactureId);
            var e = await db.Entites.AsNoTracking().FirstOrDefaultAsync(x => x.Code == a.Entite);
            if (e is null) return Results.Conflict(new { message = "L'entité émettrice n'est pas paramétrée." });
            if (a.Jeton is null) return NonSigne();
            return Results.File(DocumentsPdf.Avoir(a, f, e, Lien(config, a.Jeton)), "application/pdf", $"{a.Numero}.pdf");
        });
    }

    public static string NouveauJeton() => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    public static string Lien(IConfiguration config, string jeton) => $"{(config["App:PublicUrl"] ?? "http://localhost:3000").TrimEnd('/')}/verification/{jeton}";
    private static async Task<bool> MotDePasseValide(IUtilisateursLookup users, ClaimsPrincipal user, SignatureInput input) =>
        !string.IsNullOrEmpty(input.MotDePasse) && await users.VerifierMotDePasseAsync(user.UserId(), input.MotDePasse);
    private static IResult MauvaisMotDePasse() => Results.Conflict(new { message = "Mot de passe incorrect : il signe électroniquement le document." });
    private static IResult NonSigne() => Results.Conflict(new { message = "Ce document n'est pas encore signé : signez-le avant de l'imprimer ou de l'envoyer." });
}
