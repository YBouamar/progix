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

/// <summary>Devis de vente directe (duplicables) et commandes de vente qui en sont issues.</summary>
public static class DevisEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var d = app.MapGroup("/api/negoce/devis").WithTags("Négoce - devis").RequireAuthorization().AddEndpointFilter<ConcurrenceFilter>();

        d.MapGet("/", (string? etat, Guid? clientId, string? recherche, NegoceDbContext db) => Lister(db, TypesVente.Devis, etat, clientId, recherche));
        d.MapGet("/{id:guid}", async (Guid id, NegoceDbContext db) =>
        {
            var x = await Charger(db, id, TypesVente.Devis, suivi: false);
            if (x is null) return Results.NotFound();
            var commande = await db.Documents.AsNoTracking().Where(c => c.ParentId == id && c.Type == TypesVente.Commande && c.Etat != EtatsVente.Annule).Select(c => new { id = c.Id, numero = c.Numero }).FirstOrDefaultAsync();
            var detail = VentesSupport.Detail(x);
            detail["commande"] = commande;
            return Results.Ok(detail);
        });

        d.MapPost("/", async (DocumentVenteInput input, NegoceDbContext db, Numeros numeros, IPartenairesLookup partenaires, IProjetsLookup projets, ClaimsPrincipal user) =>
        {
            var x = new DocumentVente { Type = TypesVente.Devis, CreePar = user.UserName() };
            if (await Remplir(x, input, db, partenaires, projets) is { } erreur) return erreur;
            x.Numero = await numeros.DevisAsync(x.Entite, x.DateDocument.Year);
            db.Documents.Add(x);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = x.Id, numero = x.Numero });
        });

        d.MapPut("/{id:guid}", async (Guid id, DocumentVenteInput input, NegoceDbContext db, IPartenairesLookup partenaires, IProjetsLookup projets) =>
        {
            var x = await Charger(db, id, TypesVente.Devis, suivi: true);
            if (x is null) return Results.NotFound();
            if (x.Etat != EtatsVente.Ouvert) return Conflit("Seul un devis ouvert se modifie.");
            if (x.Jeton is not null) return Conflit("Ce devis est signé : il ne se modifie plus. Dupliquez-le pour en faire une nouvelle version.");
            if (!string.Equals(input.Entite?.Trim(), x.Entite, StringComparison.OrdinalIgnoreCase)) return Conflit("L'entité d'un devis numéroté ne change pas : dupliquez-le pour changer d'entité.");
            db.LignesVente.RemoveRange(x.Lignes);
            if (await Remplir(x, input, db, partenaires, projets) is { } erreur) return erreur;
            x.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = x.Id, numero = x.Numero });
        });

        // Duplication : le devis copié repart ouvert, sous un nouveau numéro. Le client, l'entité et le projet peuvent changer ;
        // les prix se recalculent d'après le groupe du client choisi si on le demande, sinon ils sont conservés.
        d.MapPost("/{id:guid}/dupliquer", async (Guid id, DupliquerInput input, NegoceDbContext db, Numeros numeros, IPartenairesLookup partenaires, IProjetsLookup projets, ClaimsPrincipal user) =>
        {
            var source = await Charger(db, id, TypesVente.Devis, suivi: false);
            if (source is null) return Results.NotFound();
            var (contexte, erreur) = await VentesSupport.ContexteAsync(input.Entite ?? source.Entite, input.ClientId ?? source.ClientId, input.ProjetId ?? (input.ClientId is null ? source.ProjetId : null), db, partenaires, projets);
            if (erreur is not null) return erreur;
            var entrees = source.Lignes.OrderBy(l => l.CreatedAt).Select(l => new LigneVenteInput(l.ArticleId, l.Quantite, l.PrixUnitaire));
            var (lignes, erreurLignes) = await VentesSupport.LignesAsync(entrees, contexte!.Groupe, db, forcerPrixGroupe: input.RecalculerPrix);
            if (erreurLignes is not null) return erreurLignes;
            var copie = new DocumentVente { Type = TypesVente.Devis, DateDocument = DateOnly.FromDateTime(DateTime.Today), Notes = source.Notes, CreePar = user.UserName(), Lignes = lignes! };
            VentesSupport.Appliquer(copie, contexte);
            copie.Numero = await numeros.DevisAsync(copie.Entite, copie.DateDocument.Year);
            db.Documents.Add(copie);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = copie.Id, numero = copie.Numero });
        });

        d.MapPost("/{id:guid}/refuser", async (Guid id, NegoceDbContext db) =>
        {
            var x = await Charger(db, id, TypesVente.Devis, suivi: true);
            if (x is null) return Results.NotFound();
            if (x.Etat != EtatsVente.Ouvert) return Conflit("Seul un devis ouvert peut être refusé.");
            x.Etat = EtatsVente.Refuse; x.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { id = x.Id, etat = x.Etat });
        });

        d.MapPost("/{id:guid}/annuler", async (Guid id, MotifInput input, NegoceDbContext db, ClaimsPrincipal user) =>
        {
            var x = await Charger(db, id, TypesVente.Devis, suivi: true);
            if (x is null) return Results.NotFound();
            if (x.Etat == EtatsVente.Annule) return Conflit("Ce devis est déjà annulé.");
            if (x.Etat == EtatsVente.Accepte) return Conflit("Ce devis est devenu une commande : annulez la commande.");
            return await Annuler(x, input, db, user);
        });

        // Le client accepte : le devis devient une commande qui reprend ses lignes et ses prix.
        d.MapPost("/{id:guid}/commande", async (Guid id, NegoceDbContext db, Numeros numeros, ClaimsPrincipal user) =>
        {
            var x = await Charger(db, id, TypesVente.Devis, suivi: true);
            if (x is null) return Results.NotFound();
            if (x.Etat != EtatsVente.Ouvert) return Conflit("Seul un devis ouvert peut devenir une commande.");
            var c = new DocumentVente
            {
                Type = TypesVente.Commande, Entite = x.Entite, ClientId = x.ClientId, ClientNom = x.ClientNom, ProjetId = x.ProjetId, ProjetCode = x.ProjetCode, ProjetLibelle = x.ProjetLibelle,
                GroupeId = x.GroupeId, GroupeNom = x.GroupeNom, ParentId = x.Id, ParentNumero = x.Numero, DateDocument = DateOnly.FromDateTime(DateTime.Today), TauxTva = x.TauxTva,
                Notes = x.Notes, CreePar = user.UserName(), Etat = EtatsVente.Ouvert,
                Lignes = x.Lignes.OrderBy(l => l.CreatedAt).Select(l => new LigneVente
                {
                    ArticleId = l.ArticleId, Code = l.Code, Designation = l.Designation, Unite = l.Unite, Quantite = l.Quantite, PrixUnitaire = l.PrixUnitaire, SourceLigneId = l.Id,
                }).ToList(),
            };
            c.Numero = await numeros.CommandeVenteAsync(c.Entite, c.DateDocument.Year);
            x.Etat = EtatsVente.Accepte; x.UpdatedAt = DateTime.UtcNow;
            db.Documents.Add(c);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = c.Id, numero = c.Numero });
        });

        var c = app.MapGroup("/api/negoce/commandes").WithTags("Négoce - commandes de vente").RequireAuthorization().AddEndpointFilter<ConcurrenceFilter>();

        c.MapGet("/", (string? etat, Guid? clientId, string? recherche, NegoceDbContext db) => Lister(db, TypesVente.Commande, etat, clientId, recherche));

        c.MapGet("/{id:guid}", async (Guid id, NegoceDbContext db) =>
        {
            var x = await Charger(db, id, TypesVente.Commande, suivi: false);
            if (x is null) return Results.NotFound();
            var enfants = await Enfants(db, id);
            var detail = VentesSupport.Detail(x);
            detail["lignes"] = x.Lignes.OrderBy(l => l.CreatedAt).Select(l => new
            {
                id = l.Id, articleId = l.ArticleId, code = l.Code, designation = l.Designation, unite = l.Unite, quantite = l.Quantite, prixUnitaire = l.PrixUnitaire,
                montantHt = Montants.LigneHT(l.Quantite, l.PrixUnitaire), livree = Avancement.Reprise(enfants, TypesVente.Livraison, l.Id), facturee = Avancement.Reprise(enfants, TypesVente.Facture, l.Id),
                resteALivrer = Avancement.ResteALivrer(l, enfants), resteAFacturer = Avancement.ResteAFacturer(l, enfants),
            }).ToList();
            detail["livraisons"] = enfants.Where(e => e.Type == TypesVente.Livraison).OrderBy(e => e.DateDocument).Select(VentesSupport.Resume).ToList();
            detail["factures"] = enfants.Where(e => e.Type == TypesVente.Facture).OrderBy(e => e.DateDocument).Select(VentesSupport.Resume).ToList();
            return Results.Ok(detail);
        });

        // Commande reçue directement, sans devis.
        c.MapPost("/", async (DocumentVenteInput input, NegoceDbContext db, Numeros numeros, IPartenairesLookup partenaires, IProjetsLookup projets, ClaimsPrincipal user) =>
        {
            var x = new DocumentVente { Type = TypesVente.Commande, CreePar = user.UserName() };
            if (await Remplir(x, input, db, partenaires, projets) is { } erreur) return erreur;
            x.Numero = await numeros.CommandeVenteAsync(x.Entite, x.DateDocument.Year);
            db.Documents.Add(x);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = x.Id, numero = x.Numero });
        });

        c.MapPost("/{id:guid}/annuler", async (Guid id, MotifInput input, NegoceDbContext db, ClaimsPrincipal user) =>
        {
            var x = await Charger(db, id, TypesVente.Commande, suivi: true);
            if (x is null) return Results.NotFound();
            if (x.Etat == EtatsVente.Annule) return Conflit("Cette commande est déjà annulée.");
            if ((await Enfants(db, id)).Any()) return Conflit("Cette commande a des bons de livraison ou des factures : annulez-les d'abord.");
            return await Annuler(x, input, db, user);
        });
    }

    /// <summary>Documents actifs (bons de livraison et factures) issus d'une commande, avec leurs lignes.</summary>
    internal static async Task<List<DocumentVente>> Enfants(NegoceDbContext db, Guid commandeId) =>
        await db.Documents.AsNoTracking().Include(x => x.Lignes).Where(x => x.ParentId == commandeId && x.Etat != EtatsVente.Annule && (x.Type == TypesVente.Livraison || x.Type == TypesVente.Facture)).ToListAsync();

    internal static Task<DocumentVente?> Charger(NegoceDbContext db, Guid id, string type, bool suivi)
    {
        var q = db.Documents.Include(x => x.Lignes).Include(x => x.Paiements).AsSplitQuery();
        return (suivi ? q : q.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id && x.Type == type);
    }

    internal static async Task<IResult> Annuler(DocumentVente x, MotifInput input, NegoceDbContext db, ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(input.Motif)) return Conflit("Le motif de l'annulation est obligatoire.");
        x.Etat = EtatsVente.Annule; x.MotifAnnulation = input.Motif.Trim(); x.AnnulePar = user.UserName(); x.AnnuleLe = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(new { id = x.Id, etat = x.Etat });
    }

    private static async Task<IResult> Lister(NegoceDbContext db, string type, string? etat, Guid? clientId, string? recherche)
    {
        var q = db.Documents.AsNoTracking().Include(x => x.Lignes).Where(x => x.Type == type);
        if (!string.IsNullOrWhiteSpace(etat)) q = q.Where(x => x.Etat == etat);
        if (clientId is { } c) q = q.Where(x => x.ClientId == c);
        if (!string.IsNullOrWhiteSpace(recherche)) { var t = recherche.Trim().ToLower(); q = q.Where(x => x.Numero.ToLower().Contains(t) || x.ClientNom.ToLower().Contains(t) || (x.ProjetCode ?? "").ToLower().Contains(t)); }
        var liste = await q.OrderByDescending(x => x.DateDocument).ThenByDescending(x => x.Numero).ToListAsync();
        if (type != TypesVente.Commande) return Results.Ok(liste.Select(VentesSupport.Resume));
        // Une commande montre son avancement : livré et facturé, en quantités.
        var ids = liste.Select(x => x.Id).ToList();
        var enfants = await db.Documents.AsNoTracking().Include(x => x.Lignes).Where(x => x.ParentId != null && ids.Contains(x.ParentId.Value) && x.Etat != EtatsVente.Annule && (x.Type == TypesVente.Livraison || x.Type == TypesVente.Facture)).ToListAsync();
        return Results.Ok(liste.Select(x =>
        {
            var siens = enfants.Where(e => e.ParentId == x.Id).ToList();
            var commandee = x.Lignes.Sum(l => l.Quantite);
            var livree = x.Lignes.Sum(l => Avancement.Reprise(siens, TypesVente.Livraison, l.Id));
            var facturee = x.Lignes.Sum(l => Avancement.Reprise(siens, TypesVente.Facture, l.Id));
            return new
            {
                id = x.Id, type = x.Type, numero = x.Numero, entite = x.Entite, clientId = x.ClientId, client = x.ClientNom, projetId = x.ProjetId, projetCode = x.ProjetCode,
                date = x.DateDocument, etat = x.Etat, etatLabel = VentesSupport.EtatLabel(x.Type, x.Etat), totalHt = x.TotalHT, totalTtc = x.TotalTTC, parentNumero = x.ParentNumero,
                livraison = livree <= 0 ? "a_livrer" : livree >= commandee ? "livree" : "partielle", facturation = facturee <= 0 ? "a_facturer" : facturee >= commandee ? "facturee" : "partielle",
            };
        }));
    }

    /// <summary>Reporte la saisie sur un devis ou une commande : contexte, lignes, dates.</summary>
    private static async Task<IResult?> Remplir(DocumentVente x, DocumentVenteInput input, NegoceDbContext db, IPartenairesLookup partenaires, IProjetsLookup projets)
    {
        var (contexte, erreur) = await VentesSupport.ContexteAsync(input.Entite, input.ClientId, input.ProjetId, db, partenaires, projets);
        if (erreur is not null) return erreur;
        var (lignes, erreurLignes) = await VentesSupport.LignesAsync(input.Lignes, contexte!.Groupe, db);
        if (erreurLignes is not null) return erreurLignes;
        VentesSupport.Appliquer(x, contexte);
        x.DateDocument = input.DateDocument ?? (x.DateDocument == default ? DateOnly.FromDateTime(DateTime.Today) : x.DateDocument);
        x.Echeance = input.Echeance;
        x.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        x.Lignes = lignes!;
        return null;
    }
}
