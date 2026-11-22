using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Negoce.Application;
using Progix.Modules.Negoce.Domain;
using Progix.Modules.Negoce.Infrastructure;
using Progix.SharedKernel;
using static Progix.Modules.Negoce.Application.Reponses;

namespace Progix.Modules.Negoce.Endpoints;

public record FournisseurInput(string? Nom, string? Contact, string? Telephone, string? Email, string? Adresse, string? Ville, string? Ice, string? Notes, bool? Actif);

public static class FournisseursEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/negoce/fournisseurs").WithTags("Négoce - fournisseurs").RequireAuthorization().AddEndpointFilter<ConcurrenceFilter>();

        g.MapGet("/", async (string? recherche, bool? actif, NegoceDbContext db, DroitsNegoce droits, ClaimsPrincipal user) =>
        {
            var q = db.Fournisseurs.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(recherche)) { var t = recherche.Trim().ToLower(); q = q.Where(f => f.Nom.ToLower().Contains(t) || (f.Contact ?? "").ToLower().Contains(t) || (f.Ville ?? "").ToLower().Contains(t)); }
            if (actif is { } a) q = q.Where(f => f.Actif == a);
            var fournisseurs = await q.OrderBy(f => f.Nom).ToListAsync();
            var commandes = await db.CommandesAchat.AsNoTracking().Include(c => c.Lignes).Include(c => c.Paiements).Where(c => !c.Annule).ToListAsync();
            // Le reste à payer n'est visible que de qui voit les paiements fournisseur.
            var voitReste = await droits.NiveauAsync(user, Habilitations.NegocePaiementsFournisseurs) >= Habilitations.Lecture && await droits.VoitPrixAchatAsync(user);
            return Results.Ok(fournisseurs.Select(f =>
            {
                var siennes = commandes.Where(c => c.FournisseurId == f.Id).ToList();
                return new
                {
                    id = f.Id, nom = f.Nom, contact = f.Contact, telephone = f.Telephone, email = f.Email, ville = f.Ville, actif = f.Actif,
                    commandes = siennes.Count, enCours = siennes.Count(c => c.Etat != EtatsAchat.Recue), reste = voitReste ? siennes.Sum(c => c.ResteAPayer) : (decimal?)null,
                };
            }));
        });

        g.MapGet("/{id:guid}", async (Guid id, NegoceDbContext db, DroitsNegoce droits, ClaimsPrincipal user) =>
        {
            var f = await db.Fournisseurs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (f is null) return Results.NotFound();
            var commandes = await db.CommandesAchat.AsNoTracking().Include(c => c.Lignes).Include(c => c.Paiements).Where(c => c.FournisseurId == id).OrderByDescending(c => c.DateCommande).ToListAsync();
            var voitMontants = await droits.NiveauAsync(user, Habilitations.NegocePaiementsFournisseurs) >= Habilitations.Lecture && await droits.VoitPrixAchatAsync(user);
            return Results.Ok(new
            {
                id = f.Id, nom = f.Nom, contact = f.Contact, telephone = f.Telephone, email = f.Email, adresse = f.Adresse, ville = f.Ville, ice = f.Ice, notes = f.Notes, actif = f.Actif,
                commandes = commandes.Select(c => new { id = c.Id, numero = c.Numero, date = c.DateCommande, etat = c.Etat, etatLabel = EtatsAchat.Label(c.Etat), totalTtc = voitMontants ? c.TotalTTC : (decimal?)null, reste = voitMontants ? (c.Annule ? 0 : c.ResteAPayer) : (decimal?)null }),
            });
        });

        g.MapPost("/", async (FournisseurInput input, NegoceDbContext db) =>
        {
            var f = new Fournisseur();
            if (await Appliquer(f, input, db) is { } erreur) return erreur;
            db.Fournisseurs.Add(f);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = f.Id });
        });

        g.MapPut("/{id:guid}", async (Guid id, FournisseurInput input, NegoceDbContext db) =>
        {
            var f = await db.Fournisseurs.FirstOrDefaultAsync(x => x.Id == id);
            if (f is null) return Results.NotFound();
            if (await Appliquer(f, input, db) is { } erreur) return erreur;
            f.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = f.Id });
        });
    }

    private static async Task<IResult?> Appliquer(Fournisseur f, FournisseurInput input, NegoceDbContext db)
    {
        var nom = (input.Nom ?? "").Trim();
        if (nom.Length == 0) return Invalide("nom", "Le nom du fournisseur est obligatoire.");
        if (await db.Fournisseurs.AnyAsync(x => x.Nom == nom && x.Id != f.Id)) return Conflit("Un fournisseur porte déjà ce nom.");
        static string? Net(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        f.Nom = nom; f.Contact = Net(input.Contact); f.Telephone = Net(input.Telephone); f.Email = Net(input.Email);
        f.Adresse = Net(input.Adresse); f.Ville = Net(input.Ville); f.Ice = Net(input.Ice); f.Notes = Net(input.Notes);
        if (input.Actif is { } a) f.Actif = a;
        return null;
    }
}
