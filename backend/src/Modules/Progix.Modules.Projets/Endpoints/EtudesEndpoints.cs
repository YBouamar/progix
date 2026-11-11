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
/// Menu Étude : une étude par dalle de projet. L'ingénieur en chef, le chef de projet et le directeur y affectent les ingénieurs ;
/// un ingénieur affecté dépose les pièces et déclare l'étude terminée.
/// </summary>
public static class EtudesEndpoints
{
    public const int TaillePage = 20;

    public record AffecterInput(Guid UserId);

    private static bool Voit(ClaimsPrincipal u) => EtudeDalle.RolesVue.Any(u.IsInRole);
    private static bool Affecte(ClaimsPrincipal u) => EtudeDalle.RolesAffectation.Any(u.IsInRole);

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/etudes").WithTags("Étude").RequireAuthorization();

        // Liste des études : toutes les dalles des projets non supprimés, filtrées par état, texte ou ingénieur.
        g.MapGet("/", async (string? etat, string? q, Guid? ingenieur, int? page, ProjetsDbContext db, IUtilisateursLookup users, IFichiersLookup fichiers, ClaimsPrincipal user) =>
        {
            if (!Voit(user)) return Results.Forbid();
            var actives = db.AffectationsEtude.AsNoTracking().Where(a => a.Fin == null);
            var query = from d in db.Dalles.AsNoTracking()
                        join p in db.Projets.AsNoTracking() on d.ProjetId equals p.Id
                        where !p.Supprime
                        select new { d, p };
            if (!string.IsNullOrWhiteSpace(q))
            {
                var like = $"%{q.Trim()}%";
                query = query.Where(x => EF.Functions.ILike(x.p.Code, like) || EF.Functions.ILike(x.p.Libelle, like) || EF.Functions.ILike(x.p.Ville ?? "", like));
            }
            if (ingenieur is { } u) query = query.Where(x => actives.Any(a => a.DalleId == x.d.Id && a.UserId == u));

            var terminees = await query.CountAsync(x => x.d.EtudeTermineeLe != null);
            var enCours = await query.CountAsync(x => x.d.EtudeTermineeLe == null && actives.Any(a => a.DalleId == x.d.Id));
            var total = await query.CountAsync();

            var filtre = etat switch
            {
                EtudeDalle.Terminee => query.Where(x => x.d.EtudeTermineeLe != null),
                EtudeDalle.EnCours => query.Where(x => x.d.EtudeTermineeLe == null && actives.Any(a => a.DalleId == x.d.Id)),
                EtudeDalle.AAffecter => query.Where(x => x.d.EtudeTermineeLe == null && !actives.Any(a => a.DalleId == x.d.Id)),
                _ => query,
            };
            var n = Math.Max(1, page ?? 1);
            var nb = await filtre.CountAsync();
            var rows = await filtre.OrderByDescending(x => x.p.DateCreation).ThenBy(x => x.p.Code).ThenBy(x => x.d.Numero)
                .Skip((n - 1) * TaillePage).Take(TaillePage).ToListAsync();
            var ids = rows.Select(r => r.d.Id).ToList();
            var affectations = await actives.Where(a => ids.Contains(a.DalleId)).OrderBy(a => a.UserNom).ToListAsync();
            var moi = user.UserId();

            var items = new List<object>();
            foreach (var r in rows)
            {
                var pieces = await fichiers.CategoriesAsync(EtudeDalle.Contexte, r.d.Id);
                items.Add(Vue(r.d, r.p, affectations.Where(a => a.DalleId == r.d.Id).ToList(), pieces, moi, Affecte(user)));
            }

            return Results.Ok(new
            {
                items, total = nb, page = n, pageSize = TaillePage,
                compteurs = new[]
                {
                    new { etat = EtudeDalle.AAffecter, label = EtudeDalle.Labels[EtudeDalle.AAffecter], nb = total - terminees - enCours },
                    new { etat = EtudeDalle.EnCours, label = EtudeDalle.Labels[EtudeDalle.EnCours], nb = enCours },
                    new { etat = EtudeDalle.Terminee, label = EtudeDalle.Labels[EtudeDalle.Terminee], nb = terminees },
                },
                pieces = EtudeDalle.Pieces.Select(p => new { code = p.Code, label = p.Label }),
                ingenieurs = (await users.ParRolesAsync(EtudeDalle.RolesIngenieur)).OrderBy(x => x.Nom).Select(x => new { id = x.Id, nom = x.Nom }),
                peutAffecter = Affecte(user),
            });
        });

        // Étude d'une dalle, avec l'historique de ses affectations.
        g.MapGet("/{dalleId:guid}", async (Guid dalleId, ProjetsDbContext db, IFichiersLookup fichiers, ClaimsPrincipal user) =>
            !Voit(user) ? Results.Forbid() : await DetailAsync(db, fichiers, dalleId, user));

        // Affecter un ingénieur à l'étude. Plusieurs ingénieurs peuvent porter la même étude.
        g.MapPost("/{dalleId:guid}/ingenieurs", async (Guid dalleId, AffecterInput input, ProjetsDbContext db, IUtilisateursLookup users, IFichiersLookup fichiers, ClaimsPrincipal user) =>
        {
            if (!Affecte(user)) return Results.Forbid();
            var (dalle, refus) = await OuvrirAsync(db, dalleId);
            if (dalle is null) return refus!;
            if (EtudeDalle.RefusAffecter(dalle.EtudeTermineeLe is not null) is { } motif) return ProjetsEndpoints.Conflit(motif);
            var personne = (await users.ParRolesAsync(EtudeDalle.RolesIngenieur)).FirstOrDefault(x => x.Id == input.UserId);
            if (personne is null) return ProjetsEndpoints.Conflit("Cette personne n'est pas un ingénieur actif.");
            if (!await db.AffectationsEtude.AnyAsync(a => a.DalleId == dalleId && a.UserId == input.UserId && a.Fin == null))
            {
                db.AffectationsEtude.Add(new AffectationEtude
                {
                    DalleId = dalleId, UserId = personne.Id, UserNom = personne.Nom, AffectePar = user.UserName(), Debut = DateOnly.FromDateTime(DateTime.UtcNow),
                });
                await db.SaveChangesAsync();
            }
            return await DetailAsync(db, fichiers, dalleId, user);
        });

        // Retirer un ingénieur de l'étude : l'affectation est close, elle reste dans l'historique.
        g.MapPost("/{dalleId:guid}/ingenieurs/{userId:guid}/retirer", async (Guid dalleId, Guid userId, ProjetsDbContext db, IFichiersLookup fichiers, ClaimsPrincipal user) =>
        {
            if (!Affecte(user)) return Results.Forbid();
            var (dalle, refus) = await OuvrirAsync(db, dalleId);
            if (dalle is null) return refus!;
            if (EtudeDalle.RefusAffecter(dalle.EtudeTermineeLe is not null) is { } motif) return ProjetsEndpoints.Conflit(motif);
            var a = await db.AffectationsEtude.FirstOrDefaultAsync(x => x.DalleId == dalleId && x.UserId == userId && x.Fin == null);
            if (a is not null)
            {
                a.Fin = DateOnly.FromDateTime(DateTime.UtcNow); a.RetirePar = user.UserName(); a.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            return await DetailAsync(db, fichiers, dalleId, user);
        });

        // Clôture de l'étude par un ingénieur qui y est affecté. Les pièces sont figées ensuite.
        g.MapPost("/{dalleId:guid}/terminer", async (Guid dalleId, ProjetsDbContext db, IFichiersLookup fichiers, ClaimsPrincipal user) =>
        {
            if (!Voit(user)) return Results.Forbid();
            var (dalle, refus) = await OuvrirAsync(db, dalleId);
            if (dalle is null) return refus!;
            var moi = user.UserId();
            var affecte = await db.AffectationsEtude.AnyAsync(a => a.DalleId == dalleId && a.UserId == moi && a.Fin == null);
            if (EtudeDalle.RefusTerminer(dalle.EtudeTermineeLe is not null, affecte) is { } motif) return ProjetsEndpoints.Conflit(motif);
            dalle.EtudeTermineeLe = DateTime.UtcNow; dalle.EtudeTermineePar = user.UserName(); dalle.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return await DetailAsync(db, fichiers, dalleId, user);
        });

        // Réouverture d'une étude terminée, par ceux qui affectent les ingénieurs.
        g.MapPost("/{dalleId:guid}/rouvrir", async (Guid dalleId, ProjetsDbContext db, IFichiersLookup fichiers, ClaimsPrincipal user) =>
        {
            if (!Affecte(user)) return Results.Forbid();
            var (dalle, refus) = await OuvrirAsync(db, dalleId);
            if (dalle is null) return refus!;
            if (dalle.EtudeTermineeLe is null) return ProjetsEndpoints.Conflit("Cette étude n'est pas terminée.");
            dalle.EtudeTermineeLe = null; dalle.EtudeTermineePar = null; dalle.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return await DetailAsync(db, fichiers, dalleId, user);
        });
    }

    /// <summary>Dalle à modifier, ou le refus à renvoyer : introuvable, ou projet supprimé.</summary>
    private static async Task<(Dalle? Dalle, IResult? Refus)> OuvrirAsync(ProjetsDbContext db, Guid dalleId)
    {
        var dalle = await db.Dalles.FirstOrDefaultAsync(d => d.Id == dalleId);
        if (dalle is null) return (null, Results.NotFound());
        var projet = await db.Projets.AsNoTracking().FirstAsync(p => p.Id == dalle.ProjetId);
        return projet.Supprime ? (null, ProjetsEndpoints.Conflit("Un projet supprimé est en lecture seule.")) : (dalle, null);
    }

    private static async Task<IResult> DetailAsync(ProjetsDbContext db, IFichiersLookup fichiers, Guid dalleId, ClaimsPrincipal user)
    {
        var d = await db.Dalles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dalleId);
        if (d is null) return Results.NotFound();
        var p = await db.Projets.AsNoTracking().FirstAsync(x => x.Id == d.ProjetId);
        var toutes = await db.AffectationsEtude.AsNoTracking().Where(a => a.DalleId == dalleId).OrderByDescending(a => a.CreatedAt).ToListAsync();
        var pieces = await fichiers.CategoriesAsync(EtudeDalle.Contexte, dalleId);
        return Results.Ok(new
        {
            etude = Vue(d, p, toutes.Where(a => a.Fin == null).OrderBy(a => a.UserNom).ToList(), pieces, user.UserId(), Affecte(user)),
            historique = toutes.Select(a => new { nom = a.UserNom, par = a.AffectePar, debut = a.Debut, fin = a.Fin, retirePar = a.RetirePar }),
        });
    }

    private static object Vue(Dalle d, Projet p, List<AffectationEtude> actives, IReadOnlyList<string> pieces, Guid moi, bool responsable)
    {
        var terminee = d.EtudeTermineeLe is not null;
        var affecte = actives.Any(a => a.UserId == moi);
        var etat = EtudeDalle.Etat(terminee, actives.Count);
        return new
        {
            dalleId = d.Id, projetId = p.Id, projetCode = p.Code, projetLibelle = p.Libelle, ville = p.Ville, supprime = p.Supprime,
            reference = $"{p.Code}-{d.Suffixe}", niveauLabel = Dalle.NiveauLabel(d.Niveau), surface = d.Surface,
            etat, etatLabel = EtudeDalle.Labels[etat],
            ingenieurs = actives.Select(a => new { userId = a.UserId, nom = a.UserNom, depuis = a.Debut }),
            pieces = pieces.Where(c => EtudeDalle.Pieces.Any(x => x.Code == c)),
            termineeLe = d.EtudeTermineeLe, termineePar = d.EtudeTermineePar,
            peutTerminer = !p.Supprime && EtudeDalle.RefusTerminer(terminee, affecte) is null,
            peutRouvrir = !p.Supprime && responsable && terminee,
            peutModifierIngenieurs = !p.Supprime && responsable && !terminee,
        };
    }
}
