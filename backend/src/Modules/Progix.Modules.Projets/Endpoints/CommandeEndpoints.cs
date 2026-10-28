using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

public record TendonInput(string? Numero, int Torons, string? Sens, decimal Longueur, int Ancrages);
public record AccessoireInput(string? Libelle, int Quantite, string? Unite);
public record AccessoireParamInput(Guid? Id, string? Libelle, bool Actif, string? Unite);
public record AccessoiresParamInput(List<AccessoireParamInput>? Accessoires);
public record CommandeInput(List<TendonInput>? Tendons, List<AccessoireInput>? Accessoires, decimal? Surface = null);

/// <summary>Contenu de la commande usine d'une dalle (étape 4).</summary>
public static class CommandeEndpoints
{
    private static bool PeutParametrer(ClaimsPrincipal u) =>
        u.IsInRole(Roles.Directeur) || u.IsInRole(Roles.ChefProjet) || u.IsInRole(Roles.ResponsableLogistique);

    private static async Task<object> Parametres(ProjetsDbContext db, ClaimsPrincipal user) => new
    {
        modifiable = PeutParametrer(user),
        accessoires = (await db.Accessoires.AsNoTracking().OrderBy(a => a.Ordre).ToListAsync()).Select(a => new { id = a.Id, libelle = a.Libelle, unite = a.Unite, actif = a.Actif }),
    };

    public static void Map(IEndpointRouteBuilder app)
    {
        var param = app.MapGroup("/api/parametres/accessoires").WithTags("Paramétrage").RequireAuthorization();
        param.MapGet("/", async (ProjetsDbContext db, ClaimsPrincipal user) => Results.Ok(await Parametres(db, user)));
        param.MapPut("/", async (AccessoiresParamInput input, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            if (!PeutParametrer(user)) return Results.Forbid();
            var liste = (input.Accessoires ?? []).Where(a => !string.IsNullOrWhiteSpace(a.Libelle)).ToList();
            if (liste.GroupBy(a => a.Libelle!.Trim().ToUpperInvariant()).Any(x => x.Count() > 1)) return ProjetsEndpoints.Conflit("Un accessoire figure deux fois dans la liste.");
            var existants = await db.Accessoires.ToListAsync();
            // Un accessoire retiré de la liste est supprimé ; les commandes déjà saisies gardent son libellé.
            db.Accessoires.RemoveRange(existants.Where(e => liste.All(a => a.Id != e.Id)));
            for (var i = 0; i < liste.Count; i++)
            {
                var a = existants.FirstOrDefault(e => e.Id == liste[i].Id);
                if (a is null) db.Accessoires.Add(a = new Accessoire());
                a.Libelle = liste[i].Libelle!.Trim();
                a.Unite = string.IsNullOrWhiteSpace(liste[i].Unite) ? null : liste[i].Unite!.Trim();
                a.Actif = liste[i].Actif;
                a.Ordre = i;
            }
            await db.SaveChangesAsync();
            return Results.Ok(await Parametres(db, user));
        });

        var g = app.MapGroup("/api/projets/{id:guid}/dalles/{did:guid}/commande").WithTags("Suivi de chantier").RequireAuthorization();

        g.MapGet("/", async (Guid id, Guid did, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var (projet, dalle) = await Charger(db, id, did);
            return projet is null || dalle is null ? Results.NotFound() : Results.Ok(await Vue(db, projet, dalle, user));
        });

        g.MapPut("/", async (Guid id, Guid did, CommandeInput input, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var (projet, dalle) = await Charger(db, id, did);
            if (projet is null || dalle is null) return Results.NotFound();
            if (!PeutSaisir(user)) return Results.Forbid();
            if (await Verrou(db, projet, dalle) is { } verrou) return ProjetsEndpoints.Conflit(verrou);

            var tendons = input.Tendons ?? [];
            // Un accessoire laissé à 0 n'entre pas dans la commande.
            var accessoires = (input.Accessoires ?? []).Where(a => !string.IsNullOrWhiteSpace(a.Libelle) && a.Quantite != 0).ToList();
            var c = await db.CommandesUsine.FirstOrDefaultAsync(x => x.DalleId == did);
            if (ImportTendons.Refus(tendons, c?.Lignes.Select(l => l.Torons).ToHashSet()) is { } refus) return ProjetsEndpoints.Conflit(refus);
            if (accessoires.Any(a => a.Quantite < 0)) return ProjetsEndpoints.Conflit("La quantité d'un accessoire ne peut pas être négative.");
            if (input.Surface is not > 0) return ProjetsEndpoints.Conflit("Saisissez la surface totale de la commande (m²) : elle est reprise sur le bon de livraison et la facture.");

            if (c is null) db.CommandesUsine.Add(c = new CommandeUsine { DalleId = did });
            c.Surface = Math.Round(input.Surface.Value, 2);
            c.Lignes = tendons.Select(t => new LigneCommande
            {
                Numero = ImportTendons.Numero(t.Numero)!, Torons = t.Torons, Sens = ImportTendons.Sens(t.Sens)!, Prefixe = "", Nombre = 1,
                Longueur = Math.Round(t.Longueur, 2), AncragesSaisis = t.Ancrages,
            }).ToList();
            var parametres = await db.Accessoires.AsNoTracking().ToListAsync();
            c.Accessoires = accessoires.Select(a => new AccessoireCommande { AccessoireId = parametres.FirstOrDefault(x => x.Libelle == a.Libelle!.Trim())?.Id, Libelle = a.Libelle!.Trim(), Quantite = a.Quantite, Unite = string.IsNullOrWhiteSpace(a.Unite) ? null : a.Unite.Trim() }).ToList();
            c.NbCables = c.Lignes.Count;
            c.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(await Vue(db, projet, dalle, user));
        });

        // Trame vide à remplir : Excel, CSV ou JSON, avec deux tendons d'exemple.
        g.MapGet("/trame.{format}", (string format) => ImportTendons.Trame(format) is { } t ? Results.File(t.Contenu, t.Type, t.Nom) : Results.NotFound());

        // Lecture d'un fichier de tendons. Rien n'est enregistré : l'écran affiche le résultat, qu'il reste à enregistrer.
        g.MapPost("/import", async (Guid id, Guid did, IFormFile fichier, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var (projet, dalle) = await Charger(db, id, did);
            if (projet is null || dalle is null) return Results.NotFound();
            if (!PeutSaisir(user)) return Results.Forbid();
            if (await Verrou(db, projet, dalle) is { } verrou) return ProjetsEndpoints.Conflit(verrou);
            if (fichier.Length == 0) return ProjetsEndpoints.Conflit("Le fichier est vide.");
            if (fichier.Length > 5 * 1024 * 1024) return ProjetsEndpoints.Conflit("Le fichier dépasse 5 Mo.");
            using var flux = new MemoryStream();
            await fichier.CopyToAsync(flux);
            var (tendons, erreurs, refus) = ImportTendons.Lire(fichier.FileName, flux.ToArray());
            if (refus is not null) return ProjetsEndpoints.Conflit(refus);
            return Results.Ok(new
            {
                tendons = tendons.Select(t => new { numero = t.Numero, torons = t.Torons, sens = t.Sens, longueur = t.Longueur, ancrages = t.Ancrages }),
                erreurs,
            });
        }).DisableAntiforgery();
    }

    /// <summary>Système et orientation de chaque tendon de la dalle, par numéro : repris sur les fiches de tension et d'injection.</summary>
    public static async Task<Dictionary<string, Tendon>> ReperesAsync(ProjetsDbContext db, Guid dalleId)
    {
        var c = await db.CommandesUsine.AsNoTracking().FirstOrDefaultAsync(x => x.DalleId == dalleId);
        var d = new Dictionary<string, Tendon>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in c?.Tendons() ?? []) d.TryAdd(t.Numero, t);
        return d;
    }

    private static bool PeutSaisir(ClaimsPrincipal u) =>
        u.IsInRole(Roles.Directeur) || u.IsInRole(Roles.ChefProjet) || Etapes.Get(Etapes.CommandeUsine).Roles.Any(u.IsInRole);

    private static async Task<(Projet?, Dalle?)> Charger(ProjetsDbContext db, Guid id, Guid did) =>
        (await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id),
         await db.Dalles.AsNoTracking().FirstOrDefaultAsync(d => d.Id == did && d.ProjetId == id));

    /// <summary>La commande se saisit pendant l'étape 4, avant d'être soumise aux deux validations.</summary>
    private static async Task<string?> Verrou(ProjetsDbContext db, Projet projet, Dalle dalle)
    {
        if (projet.Supprime) return "Un projet supprimé est en lecture seule.";
        if (projet.Bloque) return "Projet bloqué : les étapes de chantier sont suspendues.";
        if (dalle.EtapesFaites >= Etapes.CommandeUsine) return "La commande usine est validée : elle est figée.";
        if (dalle.EtapesFaites < Etapes.CommandeUsine - 1) return "La commande usine n'est pas encore en cours.";
        return await db.Etapes.AnyAsync(e => e.DalleId == dalle.Id && e.Numero == Etapes.CommandeUsine && e.FaitLe != null)
            ? "La commande est soumise à validation : elle n'est plus modifiable." : null;
    }

    private static async Task<object> Vue(ProjetsDbContext db, Projet projet, Dalle dalle, ClaimsPrincipal user)
    {
        var c = await db.CommandesUsine.AsNoTracking().FirstOrDefaultAsync(x => x.DalleId == dalle.Id) ?? new CommandeUsine();
        var verrou = await Verrou(db, projet, dalle);
        return new
        {
            modifiable = verrou is null && PeutSaisir(user), verrou, typesAncrage = SystemesAncrage.Torons,
            surface = c.Surface, surfaceDalle = dalle.Surface,
            tendons = c.Tendons().Select(t => new { numero = t.Numero, torons = t.Torons, sens = t.Sens, longueur = t.Longueur, ancrages = t.Ancrages }),
            accessoires = c.Accessoires.Select(a => new { libelle = a.Libelle, unite = a.Unite, quantite = a.Quantite }),
            // Accessoires paramétrés, proposés d'office à la saisie.
            proposes = verrou is null
                ? (await db.Accessoires.AsNoTracking().Where(a => a.Actif).OrderBy(a => a.Ordre).ToListAsync()).Select(a => (object)new { libelle = a.Libelle, unite = a.Unite })
                : [],
        };
    }
}
