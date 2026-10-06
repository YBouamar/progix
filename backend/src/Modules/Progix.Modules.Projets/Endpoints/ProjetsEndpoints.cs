using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Application;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

public static class ProjetsEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/projets").WithTags("Projets").RequireAuthorization();

        g.MapGet("/referentiel", () => Results.Ok(new
        {
            entites = Entites.Labels.Select(e => new { code = e.Key, label = e.Value }),
            types = TypesConstruction.All.Select(t => new { code = t.Code, label = t.Label, forfait = t.Forfait }),
            statuts = StatutsProjet.Labels.Select(s => new { code = s.Key, label = s.Value }),
        }));

        g.MapGet("/", async (ProjetsDbContext db, IPartenairesLookup partenaires, string? statut, string? entite,
            int? annee, string? q, bool? supprimes, int page = 1, int pageSize = 20) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var del = supprimes ?? false;
            var query = db.Projets.AsNoTracking().Where(p => p.Supprime == del);
            if (!string.IsNullOrWhiteSpace(entite)) query = query.Where(p => p.Entite == entite);
            if (annee is int a)
                query = query.Where(p => p.DateCreation >= new DateOnly(a, 1, 1) && p.DateCreation <= new DateOnly(a, 12, 31));
            if (!string.IsNullOrWhiteSpace(q))
            {
                var like = $"%{q.Trim()}%";
                var clients = await partenaires.RechercherAsync(q);
                query = query.Where(p => EF.Functions.ILike(p.Code, like) || EF.Functions.ILike(p.Libelle, like) || EF.Functions.ILike(p.Ville ?? "", like) || clients.Contains(p.ClientId));
            }

            // Compteurs par statut, calculés avant le filtre de statut pour alimenter les onglets.
            var parStatut = await query.GroupBy(p => p.Statut).Select(x => new { Statut = x.Key, Nb = x.Count() }).ToListAsync();
            if (!string.IsNullOrWhiteSpace(statut)) query = query.Where(p => p.Statut == statut);

            var total = await query.CountAsync();
            var rows = await query.OrderByDescending(p => p.DateCreation).ThenByDescending(p => p.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var noms = await partenaires.NomsAsync(rows.Select(r => r.ClientId));
            var idsPage = rows.Select(r => r.Id).ToList();
            var dalles = await db.Dalles.Where(d => idsPage.Contains(d.ProjetId)).GroupBy(d => d.ProjetId)
                .Select(x => new { x.Key, Nb = x.Count(), Surface = x.Sum(d => d.Surface), Faites = x.Count(d => d.EtapesFaites >= Etapes.Total) }).ToDictionaryAsync(x => x.Key);
            var ings = (await db.Affectations.AsNoTracking()
                    .Where(a => idsPage.Contains(a.ProjetId) && a.Fonction == Fonctions.Ingenieur && a.Fin == null).ToListAsync())
                .GroupBy(a => a.ProjetId).ToDictionary(x => x.Key, x => string.Join(", ", x.Select(a => a.UserNom)));
            var items = rows.Select(p => new ProjetListItem(p.Id, p.Code, p.Libelle, noms.GetValueOrDefault(p.ClientId, ""), p.Ville, p.DateCreation,
                p.Entite, p.TypeConstruction, TypesConstruction.Find(p.TypeConstruction)?.Label ?? p.TypeConstruction,
                p.Statut, StatutsProjet.Labels.GetValueOrDefault(p.Statut, p.Statut), p.Bloque, p.Supprime,
                p.Supprime ? $"Supprimé le {p.SuppressionLe:dd/MM/yyyy} par {p.SuppressionPar}. Motif : {p.SuppressionMotif}" : null,
                dalles.GetValueOrDefault(p.Id)?.Nb ?? 0, dalles.GetValueOrDefault(p.Id)?.Surface ?? 0,
                ings.GetValueOrDefault(p.Id, ""), dalles.GetValueOrDefault(p.Id)?.Faites ?? 0)).ToList();
            return Results.Ok(new
            {
                items, total, page, pageSize,
                compteurs = StatutsProjet.Labels.Select(s => new { statut = s.Key, label = s.Value, nb = parStatut.FirstOrDefault(x => x.Statut == s.Key)?.Nb ?? 0 }),
            });
        });

        // Avertissement de doublon sur le libellé : il n'empêche pas la création.
        g.MapGet("/doublon", async (ProjetsDbContext db, IPartenairesLookup partenaires, string libelle, Guid? sauf) =>
        {
            var l = libelle.Trim();
            var p = await db.Projets.AsNoTracking().Where(x => !x.Supprime && x.Id != sauf)
                .FirstOrDefaultAsync(x => EF.Functions.ILike(x.Libelle, l));
            if (p is null) return Results.Ok(new { doublon = false });
            var noms = await partenaires.NomsAsync([p.ClientId]);
            return Results.Ok(new
            {
                doublon = true, id = p.Id, code = p.Code, client = noms.GetValueOrDefault(p.ClientId, ""),
                statut = StatutsProjet.Labels.GetValueOrDefault(p.Statut, p.Statut),
            });
        });

        g.MapGet("/{id:guid}", async (Guid id, ProjetsDbContext db, IPartenairesLookup partenaires) =>
        {
            var p = await db.Projets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return p is null ? Results.NotFound() : Results.Ok(await ToDetail(p, partenaires));
        });

        g.MapPost("/", async (ProjetInput input, ProjetsDbContext db, IPartenairesLookup partenaires, ClaimsPrincipal user) =>
        {
            var erreurs = await Valider(input, partenaires);
            if (erreurs.Count > 0) return Results.ValidationProblem(erreurs);

            await using var tx = await db.Database.BeginTransactionAsync();
            var date = input.DateCreation ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var annee = date.Year;
            var numero = await db.NumeroSuivantAsync($"projet:{input.Entite}:{annee}");
            var p = new Projet { Code = $"{input.Entite}-{annee}-{numero:00000}", Entite = input.Entite, DateCreation = date, CreePar = user.UserName() };
            Appliquer(p, input);
            db.Projets.Add(p);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Created($"/api/projets/{p.Id}", await ToDetail(p, partenaires));
        }).RequireAuthorization(Policies.Commercial);

        g.MapPut("/{id:guid}", async (Guid id, ProjetInput input, ProjetsDbContext db, IPartenairesLookup partenaires) =>
        {
            var p = await db.Projets.FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            if (p.Supprime) return Conflit("Un projet supprimé est en lecture seule.");
            var erreurs = await Valider(input, partenaires);
            // Le code porte l'entité : elle ne change plus après la création.
            if (input.DateCreation is DateOnly d && d != p.DateCreation) erreurs["dateCreation"] = ["La date de création ne peut plus être modifiée."];
            if (input.Entite != p.Entite) erreurs["entite"] = ["L'entité émettrice ne peut plus être modifiée."];
            if (erreurs.Count > 0) return Results.ValidationProblem(erreurs);
            Appliquer(p, input);
            p.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(await ToDetail(p, partenaires));
        }).RequireAuthorization(Policies.Commercial);

        // Réaffectation à une autre entité : nouveau code de projet, et nouveaux numéros pour tous ses documents.
        g.MapPost("/{id:guid}/reaffecter", async (Guid id, ReaffectationInput input, ProjetsDbContext db, IPartenairesLookup partenaires,
            IProjetsLookup lookup, IEnumerable<IReaffectationEntite> modules, ClaimsPrincipal user) =>
        {
            var p = await db.Projets.FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            if (p.Supprime) return Conflit("Un projet supprimé est en lecture seule.");
            if (!Entites.Labels.ContainsKey(input.Entite)) return Conflit("Entité inconnue.");
            if (input.Entite == p.Entite) return Conflit("Le projet appartient déjà à cette entité.");
            var ancienne = p.Entite;
            var ancienCode = p.Code;
            var annee = p.DateCreation.Year;
            p.Entite = input.Entite;
            p.Code = $"{input.Entite}-{annee}-{await db.NumeroSuivantAsync($"projet:{input.Entite}:{annee}"):00000}";
            p.UpdatedAt = DateTime.UtcNow;
            db.Remarques.Add(new Remarque
            {
                ProjetId = p.Id, AuteurId = user.UserId(), AuteurNom = user.UserName(), AuteurRole = user.RoleLabel(),
                Texte = $"Projet réaffecté de {ancienne} à {input.Entite}. Ancien code : {ancienCode}. Les documents du projet ont reçu de nouveaux numéros.",
            });
            await db.SaveChangesAsync();
            var resume = (await lookup.ProjetAsync(p.Id, avecDalles: true))!;
            foreach (var m in modules) await m.ReaffecterAsync(resume, ancienne, user.UserName());
            return Results.Ok(await ToDetail(p, partenaires));
        }).RequireAuthorization(p => p.RequireRole(Roles.Directeur, Roles.ChefProjet, Roles.ResponsableAdministratif));

        g.MapPost("/{id:guid}/bloquer", async (Guid id, MotifInput input, ProjetsDbContext db, IPartenairesLookup partenaires, ClaimsPrincipal user) =>
        {
            var p = await db.Projets.FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            if (p.Supprime) return Conflit("Un projet supprimé est en lecture seule.");
            if (string.IsNullOrWhiteSpace(input.Motif)) return MotifRequis();
            p.Bloque = true;
            p.BlocageMotif = input.Motif.Trim();
            p.BlocagePar = user.UserName();
            p.BlocageLe = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(await ToDetail(p, partenaires));
        }).RequireAuthorization(p => p.RequireRole(Roles.Directeur)); // le blocage est propre au directeur

        g.MapPost("/{id:guid}/debloquer", async (Guid id, ProjetsDbContext db, IPartenairesLookup partenaires) =>
        {
            var p = await db.Projets.FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            p.Bloque = false;
            p.BlocageMotif = p.BlocagePar = null;
            p.BlocageLe = null;
            await db.SaveChangesAsync();
            return Results.Ok(await ToDetail(p, partenaires));
        }).RequireAuthorization(p => p.RequireRole(Roles.Directeur));

        g.MapPost("/{id:guid}/supprimer", async (Guid id, MotifInput input, ProjetsDbContext db, IPartenairesLookup partenaires, ClaimsPrincipal user) =>
        {
            var p = await db.Projets.FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(input.Motif)) return MotifRequis();
            p.Supprime = true;
            p.SuppressionMotif = input.Motif.Trim();
            p.SuppressionPar = user.UserName();
            p.SuppressionLe = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(await ToDetail(p, partenaires));
        }).RequireAuthorization(Policies.Direction);

        g.MapPost("/{id:guid}/restaurer", async (Guid id, ProjetsDbContext db, IPartenairesLookup partenaires) =>
        {
            var p = await db.Projets.FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            p.Supprime = false;
            p.SuppressionMotif = p.SuppressionPar = null;
            p.SuppressionLe = null;
            await db.SaveChangesAsync();
            return Results.Ok(await ToDetail(p, partenaires));
        }).RequireAuthorization(Policies.Direction);
    }

    internal static IResult Conflit(string message) => Results.Conflict(new { message });

    private static IResult MotifRequis() =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["motif"] = ["Le motif est obligatoire."] });

    private static async Task<Dictionary<string, string[]>> Valider(ProjetInput i, IPartenairesLookup partenaires)
    {
        var e = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(i.Libelle)) e["libelle"] = ["Le libellé est obligatoire."];
        if (!Entites.Labels.ContainsKey(i.Entite)) e["entite"] = ["Entité inconnue."];
        if (TypesConstruction.Find(i.TypeConstruction) is null) e["typeConstruction"] = ["Type de construction inconnu."];
        if (i.DateCreation is DateOnly dc && dc > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))) e["dateCreation"] = ["La date de création ne peut pas être dans le futur."];
        var ids = new List<Guid> { i.ClientId };
        if (i.ProprietaireId is Guid pid) ids.Add(pid);
        var noms = await partenaires.NomsAsync(ids);
        if (i.ClientId == Guid.Empty || !noms.ContainsKey(i.ClientId)) e["clientId"] = ["Le client est obligatoire."];
        if (i.ProprietaireId is Guid p2 && !noms.ContainsKey(p2)) e["proprietaireId"] = ["Propriétaire introuvable."];
        return e;
    }

    private static void Appliquer(Projet p, ProjetInput i)
    {
        p.Libelle = i.Libelle.Trim();
        p.ClientId = i.ClientId;
        p.ProprietaireId = i.ProprietaireId;
        p.ProprietaireLibre = i.ProprietaireId is null && !string.IsNullOrWhiteSpace(i.ProprietaireLibre) ? i.ProprietaireLibre.Trim() : null;
        p.TypeConstruction = i.TypeConstruction;
        p.Adresse = string.IsNullOrWhiteSpace(i.Adresse) ? null : i.Adresse.Trim();
        p.Ville = string.IsNullOrWhiteSpace(i.Ville) ? null : i.Ville.Trim();
        p.Description = string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim();
    }

    private static async Task<ProjetDetail> ToDetail(Projet p, IPartenairesLookup partenaires)
    {
        var ids = new List<Guid> { p.ClientId };
        if (p.ProprietaireId is Guid pid) ids.Add(pid);
        var noms = await partenaires.NomsAsync(ids);
        var type = TypesConstruction.Find(p.TypeConstruction);
        var proprietaire = p.ProprietaireId is Guid id2 ? noms.GetValueOrDefault(id2, "") : p.ProprietaireLibre ?? "";
        return new ProjetDetail(p.Id, p.Code, p.Libelle, p.Entite, Entites.Labels.GetValueOrDefault(p.Entite, p.Entite),
            p.ClientId, noms.GetValueOrDefault(p.ClientId, ""), p.ProprietaireId, p.ProprietaireLibre, proprietaire,
            p.Adresse, p.Ville, p.DateCreation,
            p.TypeConstruction, type?.Label ?? p.TypeConstruction, type?.Forfait ?? "", p.Description,
            p.Statut, StatutsProjet.Labels.GetValueOrDefault(p.Statut, p.Statut),
            p.Bloque, p.BlocageMotif, p.BlocagePar, p.BlocageLe,
            p.Supprime, p.SuppressionMotif, p.SuppressionPar, p.SuppressionLe, p.CreePar, p.CreatedAt);
    }
}
