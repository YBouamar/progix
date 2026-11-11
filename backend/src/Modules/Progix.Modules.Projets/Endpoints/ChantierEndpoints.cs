using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

/// <summary>
/// Menu Chantier : les dalles à suivre sur le terrain. Le responsable des chantiers, le chef de projet et le directeur voient tout
/// et affectent les chefs de chantier aux projets ; un chef de chantier ne voit que les dalles des projets où il est affecté.
/// Les étapes se réalisent par les points d'accès du suivi de chantier.
/// </summary>
public static class ChantierEndpoints
{
    public const int TaillePage = 20;

    public record AffecterInput(Guid UserId);

    private static bool Voit(ClaimsPrincipal u) => ChantierDalle.RolesVue.Any(u.IsInRole);
    private static bool Responsable(ClaimsPrincipal u) => ChantierDalle.RolesAffectation.Any(u.IsInRole);

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/chantier").WithTags("Chantier").RequireAuthorization();

        // Liste des dalles : toutes pour les responsables, celles de ses chantiers pour un chef de chantier.
        g.MapGet("/", async (string? etat, string? q, Guid? chef, int? page, ProjetsDbContext db, IUtilisateursLookup users, ClaimsPrincipal user) =>
        {
            if (!Voit(user)) return Results.Forbid();
            var responsable = Responsable(user);
            var moi = user.UserId();
            var chefs = db.Affectations.AsNoTracking().Where(a => a.Fonction == Fonctions.ChefChantier && a.Fin == null);
            var query = from d in db.Dalles.AsNoTracking()
                        join p in db.Projets.AsNoTracking() on d.ProjetId equals p.Id
                        where !p.Supprime
                        select new { d, p, Chefs = chefs.Count(a => a.ProjetId == p.Id) };
            if (!responsable) query = query.Where(x => chefs.Any(a => a.ProjetId == x.p.Id && a.UserId == moi));
            if (!string.IsNullOrWhiteSpace(q))
            {
                var like = $"%{q.Trim()}%";
                query = query.Where(x => EF.Functions.ILike(x.p.Code, like) || EF.Functions.ILike(x.p.Libelle, like) || EF.Functions.ILike(x.p.Ville ?? "", like));
            }
            if (chef is { } u) query = query.Where(x => chefs.Any(a => a.ProjetId == x.p.Id && a.UserId == u));

            // Mêmes règles que ChantierDalle.Etat, écrites pour la base.
            var parEtat = new Dictionary<string, IQueryable<Guid>>
            {
                [ChantierDalle.AAffecter] = query.Where(x => x.d.EtapesFaites < Etapes.Total && x.Chefs == 0).Select(x => x.d.Id),
                [ChantierDalle.Preparation] = query.Where(x => x.Chefs > 0 && x.d.EtapesFaites < Etapes.ValidationBct).Select(x => x.d.Id),
                [ChantierDalle.ARealiser] = query.Where(x => x.Chefs > 0 && x.d.EtapesFaites >= Etapes.ValidationBct && x.d.EtapesFaites < Etapes.Total && x.d.EtapesFaites + 1 != Etapes.CommandeUsine).Select(x => x.d.Id),
                [ChantierDalle.Commande] = query.Where(x => x.Chefs > 0 && x.d.EtapesFaites + 1 == Etapes.CommandeUsine).Select(x => x.d.Id),
                [ChantierDalle.Terminee] = query.Where(x => x.d.EtapesFaites >= Etapes.Total).Select(x => x.d.Id),
            };
            var compteurs = new List<object>();
            foreach (var e in ChantierDalle.Etats) compteurs.Add(new { etat = e, label = ChantierDalle.Labels[e], nb = await parEtat[e].CountAsync() });

            var filtre = etat is not null && parEtat.TryGetValue(etat, out var ids) ? query.Where(x => ids.Contains(x.d.Id)) : query;
            var n = Math.Max(1, page ?? 1);
            var total = await filtre.CountAsync();
            var rows = await filtre.OrderByDescending(x => x.p.DateCreation).ThenBy(x => x.p.Code).ThenBy(x => x.d.Numero)
                .Skip((n - 1) * TaillePage).Take(TaillePage).ToListAsync();
            var dalleIds = rows.Select(r => r.d.Id).ToList();
            var projetIds = rows.Select(r => r.p.Id).Distinct().ToList();
            var affectes = await chefs.Where(a => projetIds.Contains(a.ProjetId)).OrderBy(a => a.UserNom).ToListAsync();
            var etapes = await db.Etapes.AsNoTracking().Where(e => dalleIds.Contains(e.DalleId)).ToListAsync();

            return Results.Ok(new
            {
                items = rows.Select(r =>
                {
                    var siens = affectes.Where(a => a.ProjetId == r.p.Id).ToList();
                    var code = ChantierDalle.Etat(r.d.EtapesFaites, siens.Count);
                    var numero = Math.Min(r.d.EtapesFaites + 1, Etapes.Total);
                    var def = Etapes.Get(numero);
                    var e = etapes.FirstOrDefault(x => x.DalleId == r.d.Id && x.Numero == numero);
                    return new
                    {
                        dalleId = r.d.Id, projetId = r.p.Id, projetCode = r.p.Code, projetLibelle = r.p.Libelle, ville = r.p.Ville, bloque = r.p.Bloque,
                        reference = $"{r.p.Code}-{r.d.Suffixe}", niveauLabel = Dalle.NiveauLabel(r.d.Niveau), surface = r.d.Surface,
                        etat = code, etatLabel = ChantierDalle.Labels[code],
                        etapesFaites = r.d.EtapesFaites, etape = numero, etapeLabel = r.d.EtapesFaites >= Etapes.Total ? "Les 11 étapes sont faites" : def.Label,
                        attente = r.d.EtapesFaites >= Etapes.Total ? null
                            : ChantierDalle.Attente(r.p.Bloque, r.p.Statut != StatutsProjet.Cree, def, e?.FaitLe != null, e?.Validee == true),
                        chefs = siens.Select(a => new { userId = a.UserId, nom = a.UserNom }),
                    };
                }),
                total, page = n, pageSize = TaillePage, compteurs,
                chefs = responsable ? (await users.ParRolesAsync(ChantierDalle.RolesChef)).OrderBy(x => x.Nom).Select(x => new { id = x.Id, nom = x.Nom }) : null,
                peutAffecter = responsable,
            });
        });

        // Chefs de chantier d'un projet, avec l'historique des affectations.
        g.MapGet("/projets/{projetId:guid}/chefs", async (Guid projetId, ProjetsDbContext db, ClaimsPrincipal user) =>
            !Voit(user) ? Results.Forbid() : await ChefsAsync(db, projetId, user));

        // Affecter un chef de chantier au projet : il suit toutes ses dalles.
        g.MapPost("/projets/{projetId:guid}/chefs", async (Guid projetId, AffecterInput input, ProjetsDbContext db, IUtilisateursLookup users, ClaimsPrincipal user) =>
        {
            if (!Responsable(user)) return Results.Forbid();
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projetId);
            if (projet is null) return Results.NotFound();
            if (projet.Supprime) return ProjetsEndpoints.Conflit("Un projet supprimé est en lecture seule.");
            var personne = (await users.ParRolesAsync(ChantierDalle.RolesChef)).FirstOrDefault(x => x.Id == input.UserId);
            if (personne is null) return ProjetsEndpoints.Conflit("Cette personne n'est pas un chef de chantier actif.");
            if (!await db.Affectations.AnyAsync(a => a.ProjetId == projetId && a.Fonction == Fonctions.ChefChantier && a.UserId == input.UserId && a.Fin == null))
            {
                db.Affectations.Add(new Affectation
                {
                    ProjetId = projetId, Fonction = Fonctions.ChefChantier, UserId = personne.Id, UserNom = personne.Nom, Debut = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(1)),
                });
                await db.SaveChangesAsync();
            }
            return await ChefsAsync(db, projetId, user);
        });

        // Retirer un chef de chantier : l'affectation est close, elle reste dans l'historique.
        g.MapPost("/projets/{projetId:guid}/chefs/{userId:guid}/retirer", async (Guid projetId, Guid userId, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            if (!Responsable(user)) return Results.Forbid();
            var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projetId);
            if (projet is null) return Results.NotFound();
            if (projet.Supprime) return ProjetsEndpoints.Conflit("Un projet supprimé est en lecture seule.");
            var a = await db.Affectations.FirstOrDefaultAsync(x => x.ProjetId == projetId && x.Fonction == Fonctions.ChefChantier && x.UserId == userId && x.Fin == null);
            if (a is not null)
            {
                a.Fin = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(1)); a.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            return await ChefsAsync(db, projetId, user);
        });
    }

    private static async Task<IResult> ChefsAsync(ProjetsDbContext db, Guid projetId, ClaimsPrincipal user)
    {
        var projet = await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projetId);
        if (projet is null) return Results.NotFound();
        var toutes = await db.Affectations.AsNoTracking().Where(a => a.ProjetId == projetId && a.Fonction == Fonctions.ChefChantier).OrderByDescending(a => a.CreatedAt).ToListAsync();
        var moi = user.UserId();
        if (ChantierDalle.RefusAcces(Responsable(user), toutes.Any(a => a.UserId == moi && a.Fin == null)) is { } refus)
            return Results.Json(new { message = refus }, statusCode: StatusCodes.Status403Forbidden);
        return Results.Ok(new
        {
            projetId, projetCode = projet.Code, projetLibelle = projet.Libelle,
            chefs = toutes.Where(a => a.Fin == null).OrderBy(a => a.UserNom).Select(a => new { userId = a.UserId, nom = a.UserNom, depuis = a.Debut }),
            historique = toutes.Select(a => new { nom = a.UserNom, debut = a.Debut, fin = a.Fin }),
            peutAffecter = !projet.Supprime && Responsable(user),
        });
    }
}
