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

/// <summary>Fiches de chantier en PDF : auto-contrôle, mise en tension, rapport d'injection. Une fiche se signe une fois son étape validée.</summary>
public static class FichesEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/dalles/{did:guid}/fiches/{type}").WithTags("Fiches de chantier").RequireAuthorization();

        g.MapGet("/", async (Guid did, string type, FacturationDbContext db, IFichesChantier fiches, ClaimsPrincipal user) =>
        {
            var f = await fiches.FicheAsync(did, type);
            if (f is null) return Results.NotFound();
            var s = await db.SignaturesFiches.AsNoTracking().FirstOrDefaultAsync(x => x.DalleId == did && x.Type == type);
            return Results.Ok(new { figee = f.Figee, signePar = s?.SignePar, signeLe = s?.SigneLe, peutSigner = f.Figee && s is null && f.RolesSignature.Any(user.IsInRole) });
        });

        g.MapPost("/signer", async (Guid did, string type, SignatureInput input, FacturationDbContext db, IFichesChantier fiches, IUtilisateursLookup users, ClaimsPrincipal user) =>
        {
            var f = await fiches.FicheAsync(did, type);
            if (f is null) return Results.NotFound();
            if (!f.RolesSignature.Any(user.IsInRole)) return Results.Forbid();
            if (!f.Figee) return Conflit("La fiche se signe une fois l'étape validée : elle peut encore changer.");
            if (await db.SignaturesFiches.AnyAsync(x => x.DalleId == did && x.Type == type)) return Conflit("Cette fiche est déjà signée.");
            if (string.IsNullOrEmpty(input.MotDePasse) || !await users.VerifierMotDePasseAsync(user.UserId(), input.MotDePasse))
                return Conflit("Mot de passe incorrect : il signe électroniquement le document.");
            var s = new SignatureFiche { DalleId = did, Type = type, ProjetId = f.ProjetId, Reference = f.Reference, Titre = f.Titre, SignePar = user.UserName(), SigneLe = DateTime.UtcNow, Jeton = PdfEndpoints.NouveauJeton() };
            db.SignaturesFiches.Add(s);
            await db.SaveChangesAsync();
            return Results.Ok(new { figee = true, signePar = s.SignePar, signeLe = s.SigneLe, peutSigner = false });
        });

        g.MapGet("/pdf", async (Guid did, string type, FacturationDbContext db, IFichesChantier fiches, IProjetsLookup projets, IPartenairesLookup partenaires, IConfiguration config) =>
        {
            var f = await fiches.FicheAsync(did, type);
            if (f is null) return Results.NotFound();
            var s = await db.SignaturesFiches.AsNoTracking().FirstOrDefaultAsync(x => x.DalleId == did && x.Type == type);
            if (s is null) return Conflit("Ce document n'est pas encore signé : signez-le avant de l'imprimer ou de l'envoyer.");
            var p = await projets.ProjetAsync(f.ProjetId);
            var e = p is null ? null : await db.Entites.AsNoTracking().FirstOrDefaultAsync(x => x.Code == p.Entite);
            if (p is null || e is null) return Conflit("Projet ou entité introuvable pour cette fiche.");
            var client = (await partenaires.NomsAsync([p.ClientId])).GetValueOrDefault(p.ClientId, "");
            return Results.File(FichePdf.Generer(f, p, client, e, s, PdfEndpoints.Lien(config, s.Jeton)), "application/pdf", $"{type}-{f.Reference}.pdf");
        });
    }

    private static IResult Conflit(string message) => Results.Conflict(new { message });
}
