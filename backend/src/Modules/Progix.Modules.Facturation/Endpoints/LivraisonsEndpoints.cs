using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Facturation.Domain;
using Progix.Modules.Facturation.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Endpoints;

public record LivraisonInput(Guid ProjetId, DateOnly DateLivraison, List<Guid>? DalleIds);

public static class LivraisonsEndpoints
{
    /// <summary>Une dalle devient livrable dès que sa commande usine est préparée : étape 4 terminée.</summary>
    public const int EtapeCommandeUsine = 4;

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/livraisons").WithTags("Livraisons").RequireAuthorization(Policies.Commercial);

        g.MapGet("/", async (FacturationDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires,
            Guid? projetId, int? annee, string? q, int page = 1, int pageSize = 20) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var query = db.BonsLivraison.AsNoTracking();
            if (projetId is Guid pid) query = query.Where(b => b.ProjetId == pid);
            if (annee is int a) query = query.Where(b => b.DateLivraison >= new DateOnly(a, 1, 1) && b.DateLivraison <= new DateOnly(a, 12, 31));
            if (!string.IsNullOrWhiteSpace(q))
            {
                var like = $"%{q.Trim()}%";
                var ids = await projets.RechercherAsync(q);
                query = query.Where(b => EF.Functions.ILike(b.Numero, like) || ids.Contains(b.ProjetId));
            }
            var total = await query.CountAsync();
            var rows = await query.Include(b => b.Lignes).OrderByDescending(b => b.DateLivraison).ThenByDescending(b => b.Numero)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var infos = await projets.ProjetsAsync(rows.Select(r => r.ProjetId));
            var noms = await partenaires.NomsAsync(infos.Values.Select(p => p.ClientId));
            var factureIds = rows.Where(r => r.FactureId != null).Select(r => r.FactureId!.Value).ToList();
            var factures = await db.Factures.AsNoTracking().Where(f => factureIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Numero);
            return Results.Ok(new { items = rows.Select(b => Dto(b, infos.GetValueOrDefault(b.ProjetId), noms, b.FactureId is Guid fid ? factures.GetValueOrDefault(fid) : null)), total, page, pageSize });
        });

        // Dalles d'un projet avec leur situation de livraison, pour préparer un bon.
        g.MapGet("/dalles", async (Guid projetId, FacturationDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires) =>
        {
            var projet = await projets.ProjetAsync(projetId, avecDalles: true);
            if (projet is null) return Results.NotFound();
            var livrees = await (from l in db.LignesLivraison.AsNoTracking()
                                 join b in db.BonsLivraison.AsNoTracking() on l.BonLivraisonId equals b.Id
                                 where b.ProjetId == projetId
                                 select new { l.DalleId, b.Numero }).ToDictionaryAsync(x => x.DalleId, x => x.Numero);
            var client = (await partenaires.NomsAsync([projet.ClientId])).GetValueOrDefault(projet.ClientId, "");
            return Results.Ok(new
            {
                projetId = projet.Id, code = projet.Code, libelle = projet.Libelle, entite = projet.Entite, client,
                refus = Refus(projet),
                dalles = projet.Dalles.Select(d =>
                {
                    var bl = livrees.GetValueOrDefault(d.Id);
                    var prete = d.EtapesFaites >= EtapeCommandeUsine;
                    return new
                    {
                        id = d.Id, reference = d.Reference, emplacement = d.Emplacement, surface = d.SurfaceCommande ?? d.Surface, surfaceCommande = d.SurfaceCommande,
                        livrable = bl is null && prete,
                        etat = bl is not null ? $"Déjà livrée · {bl}" : prete ? "Commande préparée" : "Commande non préparée",
                    };
                }),
            });
        });

        g.MapGet("/{id:guid}", async (Guid id, FacturationDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires) =>
        {
            var b = await db.BonsLivraison.AsNoTracking().Include(x => x.Lignes).FirstOrDefaultAsync(x => x.Id == id);
            if (b is null) return Results.NotFound();
            var infos = await projets.ProjetsAsync([b.ProjetId]);
            var noms = await partenaires.NomsAsync(infos.Values.Select(p => p.ClientId));
            return Results.Ok(Dto(b, infos.GetValueOrDefault(b.ProjetId), noms));
        });

        g.MapPost("/", async (LivraisonInput input, FacturationDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires, ClaimsPrincipal user) =>
        {
            var projet = await projets.ProjetAsync(input.ProjetId, avecDalles: true);
            if (projet is null) return Results.NotFound();
            if (Refus(projet) is { } refus) return Conflit(refus);
            var ids = (input.DalleIds ?? []).Distinct().ToList();
            if (ids.Count == 0) return Conflit("Sélectionnez au moins une dalle.");
            var dalles = projet.Dalles.Where(d => ids.Contains(d.Id)).ToList();
            if (dalles.Count != ids.Count) return Conflit("Une dalle sélectionnée n'appartient pas à ce projet.");
            if (dalles.FirstOrDefault(d => d.EtapesFaites < EtapeCommandeUsine) is { } pasPrete)
                return Conflit($"La commande usine de la dalle {pasPrete.Reference} n'est pas encore préparée.");
            if (await db.LignesLivraison.AnyAsync(l => ids.Contains(l.DalleId))) return Conflit("Une dalle sélectionnée figure déjà sur un bon de livraison.");
            // La surface livrée est celle saisie sur la commande usine ; une commande d'avant cette règle garde la surface de la dalle.
            decimal Livree(DalleResume d) => d.SurfaceCommande ?? d.Surface;

            await using var tx = await db.Database.BeginTransactionAsync();
            var annee = input.DateLivraison.Year;
            var b = new BonLivraison
            {
                Entite = projet.Entite, Numero = $"BL-{await db.NumeroSuivantAsync($"bl:{projet.Entite}:{annee}"):00000}-{annee}", ProjetId = projet.Id, DateLivraison = input.DateLivraison,
                EmisPar = user.UserName(), Surface = dalles.Sum(Livree),
                Lignes = dalles.Select((d, i) => new LigneLivraison { DalleId = d.Id, Ordre = i, Reference = d.Reference, Emplacement = d.Emplacement, Surface = Livree(d) }).ToList(),
            };
            db.BonsLivraison.Add(b);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            var noms = await partenaires.NomsAsync([projet.ClientId]);
            return Results.Ok(Dto(b, projet, noms));
        });
    }

    private static IResult Conflit(string message) => Results.Conflict(new { message });

    private static string? Refus(ProjetResume p) =>
        p.Supprime ? "Un projet supprimé est en lecture seule." : p.Bloque ? "Projet bloqué : les livraisons sont suspendues." : null;

    private static object Dto(BonLivraison b, ProjetResume? p, IReadOnlyDictionary<Guid, string> noms, string? facture = null) => new
    {
        id = b.Id, numero = b.Numero, ancienNumero = b.AncienNumero, dateLivraison = b.DateLivraison, projetId = b.ProjetId, projetCode = p?.Code, projet = p?.Libelle,
        entite = b.Entite, client = p is null ? "" : noms.GetValueOrDefault(p.ClientId, ""), surface = b.Surface, emisPar = b.EmisPar, emisLe = b.CreatedAt, signePar = b.SignePar, signeLe = b.SigneLe,
        facture,
        dalles = b.Lignes.OrderBy(l => l.Ordre).Select(l => new { reference = l.Reference, emplacement = l.Emplacement, surface = l.Surface }),
    };
}
