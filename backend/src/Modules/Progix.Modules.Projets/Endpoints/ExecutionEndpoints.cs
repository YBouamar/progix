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
/// Menu Exécution : les dalles à traiter par les dessinateurs. Le dessinateur en chef, le chef de projet et le directeur affectent
/// les dessinateurs et planifient ; un dessinateur affecté ouvre les dossiers (une version des plans chacun), les termine et note leur envoi.
/// </summary>
public static class ExecutionEndpoints
{
    public const int TaillePage = 20;

    public record AffecterInput(Guid UserId);
    public record PrevueInput(DateOnly? Date);
    public record DossierInput(string? Commentaire);
    public record ArretInput(string? Motif);
    public record EnvoiInput(DateOnly? Date);

    private static bool Voit(ClaimsPrincipal u) => ExecutionDalle.RolesVue.Any(u.IsInRole);
    private static bool Responsable(ClaimsPrincipal u) => ExecutionDalle.RolesAffectation.Any(u.IsInRole);

    /// <summary>Jour au Maroc (UTC+1 toute l'année).</summary>
    private static DateOnly Jour(DateTime utc) => DateOnly.FromDateTime(utc.AddHours(1));

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/execution").WithTags("Exécution").RequireAuthorization();

        // Liste : toutes les dalles des projets non supprimés, filtrées par état, texte ou dessinateur.
        g.MapGet("/", async (string? etat, string? q, Guid? dessinateur, int? page, ProjetsDbContext db, IUtilisateursLookup users, ClaimsPrincipal user) =>
        {
            if (!Voit(user)) return Results.Forbid();
            var actives = db.AffectationsExecution.AsNoTracking().Where(a => a.Fin == null);
            var derniers = db.VersionsPlans.AsNoTracking().Where(v => !db.VersionsPlans.Any(w => w.DalleId == v.DalleId && w.Lettre > v.Lettre));
            var query = from d in db.Dalles.AsNoTracking()
                        join p in db.Projets.AsNoTracking() on d.ProjetId equals p.Id
                        where !p.Supprime
                        select new
                        {
                            d, p,
                            Dossier = derniers.Where(v => v.DalleId == d.Id).Select(v => v.Etat).FirstOrDefault(),
                            Dessinateurs = actives.Count(a => a.DalleId == d.Id),
                        };
            if (!string.IsNullOrWhiteSpace(q))
            {
                var like = $"%{q.Trim()}%";
                query = query.Where(x => EF.Functions.ILike(x.p.Code, like) || EF.Functions.ILike(x.p.Libelle, like) || EF.Functions.ILike(x.p.Ville ?? "", like));
            }
            if (dessinateur is { } u) query = query.Where(x => actives.Any(a => a.DalleId == x.d.Id && a.UserId == u));

            var compteurs = new List<object>();
            foreach (var e in ExecutionDalle.Etats)
            {
                var nb = e switch
                {
                    ExecutionDalle.AAffecter => await query.CountAsync(x => x.Dossier == null && x.Dessinateurs == 0),
                    ExecutionDalle.ALancer => await query.CountAsync(x => x.Dossier == null && x.Dessinateurs > 0),
                    _ => await query.CountAsync(x => x.Dossier == e),
                };
                compteurs.Add(new { etat = e, label = ExecutionDalle.Labels[e], nb });
            }

            var filtre = etat switch
            {
                ExecutionDalle.AAffecter => query.Where(x => x.Dossier == null && x.Dessinateurs == 0),
                ExecutionDalle.ALancer => query.Where(x => x.Dossier == null && x.Dessinateurs > 0),
                ExecutionDalle.EnCours or ExecutionDalle.Termine or ExecutionDalle.Arrete => query.Where(x => x.Dossier == etat),
                _ => query,
            };
            var n = Math.Max(1, page ?? 1);
            var total = await filtre.CountAsync();
            // Les dalles planifiées d'abord, par date ; les autres ensuite, des projets les plus récents aux plus anciens.
            var rows = await filtre.OrderBy(x => x.d.EtudePlanifiee == null).ThenBy(x => x.d.EtudePlanifiee)
                .ThenByDescending(x => x.p.DateCreation).ThenBy(x => x.p.Code).ThenBy(x => x.d.Numero)
                .Skip((n - 1) * TaillePage).Take(TaillePage).ToListAsync();
            var ids = rows.Select(r => r.d.Id).ToList();
            var affectations = await actives.Where(a => ids.Contains(a.DalleId)).OrderBy(a => a.CreatedAt).ToListAsync();
            var versions = await db.VersionsPlans.AsNoTracking().Where(v => ids.Contains(v.DalleId)).ToListAsync();
            var moi = user.UserId();

            return Results.Ok(new
            {
                items = rows.Select(r => Vue(r.d, r.p, affectations.Where(a => a.DalleId == r.d.Id).ToList(), versions.Where(v => v.DalleId == r.d.Id).ToList(), moi, Responsable(user))),
                total, page = n, pageSize = TaillePage, compteurs,
                dessinateurs = (await users.ParRolesAsync(ExecutionDalle.RolesDessinateur)).OrderBy(x => x.Nom).Select(x => new { id = x.Id, nom = x.Nom }),
                peutAffecter = Responsable(user),
            });
        });

        // Suivi par jour : les dossiers ouverts chaque jour et les dalles planifiées ce jour-là qui n'ont pas encore de dossier.
        g.MapGet("/jours", async (DateOnly? du, DateOnly? au, ProjetsDbContext db, IPartenairesLookup partenaires, ClaimsPrincipal user) =>
        {
            if (!Voit(user)) return Results.Forbid();
            var aujourdhui = Jour(DateTime.UtcNow);
            var fin = au ?? aujourdhui;
            var debut = du ?? fin.AddDays(-6);
            if (debut > fin) (debut, fin) = (fin, debut);
            if (fin.DayNumber - debut.DayNumber > 92) debut = fin.AddDays(-92);
            // Bornes en temps universel du premier et du dernier jour demandés.
            var (de, a) = (debut.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-1), fin.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-1));

            var dossiers = await (from v in db.VersionsPlans.AsNoTracking()
                                  join d in db.Dalles.AsNoTracking() on v.DalleId equals d.Id
                                  join p in db.Projets.AsNoTracking() on d.ProjetId equals p.Id
                                  where !p.Supprime && v.CreatedAt >= de && v.CreatedAt < a
                                  select new { v, d, p, Premier = !db.VersionsPlans.Any(w => w.DalleId == v.DalleId && w.Lettre < v.Lettre) }).ToListAsync();
            var prevues = await (from d in db.Dalles.AsNoTracking()
                                 join p in db.Projets.AsNoTracking() on d.ProjetId equals p.Id
                                 where !p.Supprime && d.EtudePlanifiee >= debut && d.EtudePlanifiee <= fin && !db.VersionsPlans.Any(v => v.DalleId == d.Id)
                                 select new { d, p }).ToListAsync();

            var dalleIds = dossiers.Select(x => x.d.Id).Concat(prevues.Select(x => x.d.Id)).Distinct().ToList();
            var projetIds = dossiers.Select(x => x.p.Id).Concat(prevues.Select(x => x.p.Id)).Distinct().ToList();
            var dessinateurs = await db.AffectationsExecution.AsNoTracking().Where(x => dalleIds.Contains(x.DalleId) && x.Fin == null).OrderBy(x => x.CreatedAt).ToListAsync();
            var ingenieurs = await db.AffectationsEtude.AsNoTracking().Where(x => dalleIds.Contains(x.DalleId) && x.Fin == null).OrderBy(x => x.CreatedAt).ToListAsync();
            var liens = await db.ProjetPartenaires.AsNoTracking().Where(x => projetIds.Contains(x.ProjetId) && (x.Role == "bet" || x.Role == "bct")).ToListAsync();
            var noms = await partenaires.NomsAsync(liens.Select(l => l.PartenaireId).Concat(dossiers.Select(x => x.p.ClientId)).Concat(prevues.Select(x => x.p.ClientId)).Distinct());
            string? Partenaire(Guid projetId, string role) =>
                string.Join(", ", liens.Where(l => l.ProjetId == projetId && l.Role == role).Select(l => noms.GetValueOrDefault(l.PartenaireId)).Where(x => x is not null)) is { Length: > 0 } s ? s : null;

            object Ligne(Dalle d, Projet p, VersionPlan? v, bool premier, DateOnly jour)
            {
                var etat = v?.Etat ?? ExecutionDalle.Etat(null, dessinateurs.Count(x => x.DalleId == d.Id));
                var type = v is null ? ExecutionDalle.Exe : ExecutionDalle.Type(premier);
                return new
                {
                    jour, dalleId = d.Id, projetId = p.Id, dossierId = v?.Id, lettre = v?.Lettre.ToString(),
                    reference = v is null ? $"{p.Code}-{d.Suffixe}" : $"{p.Code}-{d.Numero:00}{v.Lettre}", projet = p.Libelle, ville = p.Ville,
                    client = noms.GetValueOrDefault(p.ClientId), bct = Partenaire(p.Id, "bct"), bet = Partenaire(p.Id, "bet"),
                    niveauLabel = Dalle.NiveauLabel(d.Niveau), surface = v?.Surface ?? d.Surface,
                    type, typeLabel = ExecutionDalle.Types[type], etat, etatLabel = ExecutionDalle.Labels[etat],
                    dessinateurs = v?.Etat == ExecutionDalle.Termine && v.DessinateursNoms is not null ? v.DessinateursNoms
                        : string.Join(" + ", dessinateurs.Where(x => x.DalleId == d.Id).Select(x => x.UserNom)),
                    ingenieurs = string.Join(" + ", ingenieurs.Where(x => x.DalleId == d.Id).Select(x => x.UserNom)),
                    termineLe = v?.TermineLe, envoyeLe = v?.EnvoyeLe, arretMotif = v?.ArretMotif,
                    enRetard = v is null && jour < aujourdhui,
                };
            }

            var lignes = dossiers.Select(x => (Jour: Jour(x.v.CreatedAt), Ordre: x.v.CreatedAt, L: Ligne(x.d, x.p, x.v, x.Premier, Jour(x.v.CreatedAt))))
                .Concat(prevues.Select(x => (Jour: x.d.EtudePlanifiee!.Value, Ordre: DateTime.MaxValue, L: Ligne(x.d, x.p, null, true, x.d.EtudePlanifiee!.Value)))).ToList();
            return Results.Ok(new
            {
                du = debut, au = fin, aujourdhui,
                jours = lignes.GroupBy(x => x.Jour).OrderByDescending(j => j.Key).Select(j => new
                {
                    jour = j.Key,
                    lignes = j.OrderBy(x => x.Ordre).Select(x => x.L),
                    surfaceExe = dossiers.Where(x => Jour(x.v.CreatedAt) == j.Key && x.Premier).Sum(x => x.v.Surface ?? x.d.Surface),
                    surfaceModif = dossiers.Where(x => Jour(x.v.CreatedAt) == j.Key && !x.Premier).Sum(x => x.v.Surface ?? x.d.Surface),
                }),
            });
        });

        // Exécution d'une dalle : ses dessinateurs, ses dossiers et l'historique des affectations.
        g.MapGet("/{dalleId:guid}", async (Guid dalleId, ProjetsDbContext db, IFichiersLookup fichiers, ClaimsPrincipal user) =>
            !Voit(user) ? Results.Forbid() : await DetailAsync(db, fichiers, dalleId, user));

        // Affecter un dessinateur à la dalle. Plusieurs dessinateurs peuvent partager la même dalle.
        g.MapPost("/{dalleId:guid}/dessinateurs", async (Guid dalleId, AffecterInput input, ProjetsDbContext db, IUtilisateursLookup users, IFichiersLookup fichiers, ClaimsPrincipal user) =>
        {
            if (!Responsable(user)) return Results.Forbid();
            var (dalle, refus) = await OuvrirAsync(db, dalleId);
            if (dalle is null) return refus!;
            var personne = (await users.ParRolesAsync(ExecutionDalle.RolesDessinateur)).FirstOrDefault(x => x.Id == input.UserId);
            if (personne is null) return ProjetsEndpoints.Conflit("Cette personne n'est pas un dessinateur actif.");
            if (!await db.AffectationsExecution.AnyAsync(a => a.DalleId == dalleId && a.UserId == input.UserId && a.Fin == null))
            {
                db.AffectationsExecution.Add(Nouvelle(dalleId, personne, user));
                await db.SaveChangesAsync();
                await SynchroniserAsync(db, dalle);
            }
            return await DetailAsync(db, fichiers, dalleId, user);
        });

        // Retirer un dessinateur : l'affectation est close, elle reste dans l'historique.
        g.MapPost("/{dalleId:guid}/dessinateurs/{userId:guid}/retirer", async (Guid dalleId, Guid userId, ProjetsDbContext db, IFichiersLookup fichiers, ClaimsPrincipal user) =>
        {
            if (!Responsable(user)) return Results.Forbid();
            var (dalle, refus) = await OuvrirAsync(db, dalleId);
            if (dalle is null) return refus!;
            var a = await db.AffectationsExecution.FirstOrDefaultAsync(x => x.DalleId == dalleId && x.UserId == userId && x.Fin == null);
            if (a is not null)
            {
                Clore(a, user);
                await db.SaveChangesAsync();
                await SynchroniserAsync(db, dalle);
            }
            return await DetailAsync(db, fichiers, dalleId, user);
        });

        // Jour prévu pour traiter la dalle : il la place dans le suivi par jour avant l'ouverture de son premier dossier.
        g.MapPut("/{dalleId:guid}/prevue", async (Guid dalleId, PrevueInput input, ProjetsDbContext db, IFichiersLookup fichiers, ClaimsPrincipal user) =>
        {
            if (!Responsable(user)) return Results.Forbid();
            var (dalle, refus) = await OuvrirAsync(db, dalleId);
            if (dalle is null) return refus!;
            dalle.EtudePlanifiee = input.Date; dalle.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return await DetailAsync(db, fichiers, dalleId, user);
        });

        // Ouvrir un dossier : une nouvelle version des plans. Le premier est un EXE, les suivants des MODIF.
        g.MapPost("/{dalleId:guid}/dossiers", async (Guid dalleId, DossierInput input, ProjetsDbContext db, IFichiersLookup fichiers, ClaimsPrincipal user) =>
        {
            if (!Voit(user)) return Results.Forbid();
            var (dalle, refus) = await OuvrirAsync(db, dalleId);
            if (dalle is null) return refus!;
            if (!await AgitAsync(db, dalleId, user)) return ProjetsEndpoints.Conflit(ExecutionDalle.RefusOuvrir(null, null, false)!);
            if (await VersionsEndpoints.CreerAsync(db, fichiers, dalle, input.Commentaire, user) is { } motif) return ProjetsEndpoints.Conflit(motif);
            return await DetailAsync(db, fichiers, dalleId, user);
        });

        // Clôture d'un dossier : la surface de la dalle et ses dessinateurs sont relevés, les fichiers figés.
        g.MapPost("/dossiers/{id:guid}/terminer", async (Guid id, ProjetsDbContext db, IFichiersLookup fichiers, ClaimsPrincipal user) =>
        {
            if (!Voit(user)) return Results.Forbid();
            var (v, dalle, refus) = await DossierAsync(db, id);
            if (v is null) return refus!;
            var agit = await AgitAsync(db, dalle!.Id, user);
            var actives = await db.AffectationsExecution.Where(a => a.DalleId == dalle.Id && a.Fin == null).OrderBy(a => a.CreatedAt).ToListAsync();
            var manque = v.Etat == ExecutionDalle.EnCours && agit ? await ManqueAsync(fichiers, v) : null;
            if (ExecutionDalle.RefusTerminer(v.Etat, agit, actives.Count, dalle.Surface, manque) is { } motif) return ProjetsEndpoints.Conflit(motif);
            Terminer(v, dalle, actives, user.UserName());
            await db.SaveChangesAsync();
            return await DetailAsync(db, fichiers, dalle.Id, user);
        });

        // Réouverture du dernier dossier terminé, tant que les plans ne sont pas figés par le BCT.
        g.MapPost("/dossiers/{id:guid}/rouvrir", async (Guid id, ProjetsDbContext db, IFichiersLookup fichiers, ClaimsPrincipal user) =>
        {
            if (!Responsable(user)) return Results.Forbid();
            var (v, dalle, refus) = await DossierAsync(db, id);
            if (v is null) return refus!;
            if (v.Etat != ExecutionDalle.Termine) return ProjetsEndpoints.Conflit("Ce dossier n'est pas terminé.");
            if (dalle!.EtapesFaites >= Etapes.ValidationBct) return ProjetsEndpoints.Conflit(VersionPlan.Fige);
            if (await db.VersionsPlans.AnyAsync(x => x.DalleId == v.DalleId && x.Lettre > v.Lettre)) return ProjetsEndpoints.Conflit("Seul le dernier dossier de la dalle se rouvre.");
            v.Etat = ExecutionDalle.EnCours; v.TermineLe = null; v.TerminePar = null; v.Surface = null; v.DessinateursIds = null; v.DessinateursNoms = null; v.EnvoyeLe = null;
            v.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return await DetailAsync(db, fichiers, dalle.Id, user);
        });

        // Arrêt d'un dossier en cours, avec son motif.
        g.MapPost("/dossiers/{id:guid}/arreter", async (Guid id, ArretInput input, ProjetsDbContext db, IFichiersLookup fichiers, ClaimsPrincipal user) =>
        {
            if (!Responsable(user)) return Results.Forbid();
            var (v, dalle, refus) = await DossierAsync(db, id);
            if (v is null) return refus!;
            if (v.Etat != ExecutionDalle.EnCours) return ProjetsEndpoints.Conflit("Seul un dossier en cours s'arrête.");
            if (string.IsNullOrWhiteSpace(input.Motif)) return ProjetsEndpoints.Conflit("Indiquez pourquoi le dossier est arrêté.");
            v.Etat = ExecutionDalle.Arrete; v.ArretMotif = input.Motif.Trim(); v.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return await DetailAsync(db, fichiers, dalle!.Id, user);
        });

        // Reprise d'un dossier arrêté.
        g.MapPost("/dossiers/{id:guid}/reprendre", async (Guid id, ProjetsDbContext db, IFichiersLookup fichiers, ClaimsPrincipal user) =>
        {
            if (!Responsable(user)) return Results.Forbid();
            var (v, dalle, refus) = await DossierAsync(db, id);
            if (v is null) return refus!;
            if (v.Etat != ExecutionDalle.Arrete) return ProjetsEndpoints.Conflit("Ce dossier n'est pas arrêté.");
            if (await db.VersionsPlans.AnyAsync(x => x.DalleId == v.DalleId && x.Lettre > v.Lettre)) return ProjetsEndpoints.Conflit("Un dossier plus récent a été ouvert depuis.");
            v.Etat = ExecutionDalle.EnCours; v.ArretMotif = null; v.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return await DetailAsync(db, fichiers, dalle!.Id, user);
        });

        // Date d'envoi des plans d'un dossier terminé.
        g.MapPut("/dossiers/{id:guid}/envoi", async (Guid id, EnvoiInput input, ProjetsDbContext db, IFichiersLookup fichiers, ClaimsPrincipal user) =>
        {
            if (!Voit(user)) return Results.Forbid();
            var (v, dalle, refus) = await DossierAsync(db, id);
            if (v is null) return refus!;
            if (!await AgitAsync(db, dalle!.Id, user)) return ProjetsEndpoints.Conflit("Seuls les dessinateurs affectés à cette dalle notent l'envoi.");
            if (v.Etat != ExecutionDalle.Termine) return ProjetsEndpoints.Conflit("L'envoi se note sur un dossier terminé.");
            if (input.Date is { } le && le < Jour(v.TermineLe!.Value)) return ProjetsEndpoints.Conflit("La date d'envoi ne peut pas précéder la clôture du dossier.");
            v.EnvoyeLe = input.Date; v.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return await DetailAsync(db, fichiers, dalle.Id, user);
        });
    }

    private static AffectationExecution Nouvelle(Guid dalleId, UtilisateurFiche personne, ClaimsPrincipal user) => new()
    {
        DalleId = dalleId, UserId = personne.Id, UserNom = personne.Nom, AffectePar = user.UserName(), Debut = Jour(DateTime.UtcNow),
    };

    private static void Clore(AffectationExecution a, ClaimsPrincipal user)
    {
        a.Fin = Jour(DateTime.UtcNow); a.RetirePar = user.UserName(); a.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Relève sur le dossier la surface de la dalle et ses dessinateurs, et le clôt.</summary>
    internal static void Terminer(VersionPlan v, Dalle dalle, IReadOnlyList<AffectationExecution> actives, string par)
    {
        v.Etat = ExecutionDalle.Termine; v.TermineLe = DateTime.UtcNow; v.TerminePar = par; v.Surface = dalle.Surface;
        v.DessinateursIds = string.Join(",", actives.Select(a => a.UserId));
        v.DessinateursNoms = string.Join(" + ", actives.Select(a => a.UserNom));
        v.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Valider l'étape « Plans d'exécution » clôt le dossier en cours de la dalle, s'il peut l'être : même geste pour le dessinateur.
    /// </summary>
    public static async Task TerminerAvecEtapeAsync(ProjetsDbContext db, Dalle dalle, string par)
    {
        var v = await db.VersionsPlans.Where(x => x.DalleId == dalle.Id).OrderByDescending(x => x.Lettre).FirstOrDefaultAsync();
        if (v is null || v.Etat != ExecutionDalle.EnCours) return;
        var actives = await db.AffectationsExecution.Where(a => a.DalleId == dalle.Id && a.Fin == null).OrderBy(a => a.CreatedAt).ToListAsync();
        if (actives.Count == 0 || dalle.Surface <= 0) return;
        Terminer(v, dalle, actives, par);
    }

    /// <summary>
    /// Ancien réglage « un dessinateur par dalle » (équipe du projet, tableau de répartition) : la personne choisie devient
    /// le seul dessinateur de la dalle, ou la dalle n'en a plus.
    /// </summary>
    public static async Task DefinirSeulAsync(ProjetsDbContext db, Dalle dalle, UtilisateurFiche? personne, ClaimsPrincipal user)
    {
        var actives = await db.AffectationsExecution.Where(a => a.DalleId == dalle.Id && a.Fin == null).ToListAsync();
        foreach (var a in actives.Where(a => a.UserId != personne?.Id)) Clore(a, user);
        if (personne is not null && actives.All(a => a.UserId != personne.Id)) db.AffectationsExecution.Add(Nouvelle(dalle.Id, personne, user));
        await db.SaveChangesAsync();
        await SynchroniserAsync(db, dalle);
    }

    /// <summary>Tient à jour le dessinateur porté par la dalle : le premier affecté, et les noms de tous.</summary>
    private static async Task SynchroniserAsync(ProjetsDbContext db, Dalle dalle)
    {
        var actives = await db.AffectationsExecution.AsNoTracking().Where(a => a.DalleId == dalle.Id && a.Fin == null).OrderBy(a => a.CreatedAt).ToListAsync();
        dalle.DessinateurId = actives.FirstOrDefault()?.UserId;
        dalle.DessinateurNom = actives.Count == 0 ? null : string.Join(" + ", actives.Select(a => a.UserNom));
        dalle.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>Agit sur les dossiers d'une dalle : un responsable, ou un dessinateur affecté à la dalle.</summary>
    private static async Task<bool> AgitAsync(ProjetsDbContext db, Guid dalleId, ClaimsPrincipal user)
    {
        if (Responsable(user)) return true;
        var moi = user.UserId();
        return await db.AffectationsExecution.AnyAsync(a => a.DalleId == dalleId && a.UserId == moi && a.Fin == null);
    }

    /// <summary>Ce qui manque dans les fichiers d'un dossier pour le terminer, ou null.</summary>
    private static async Task<string?> ManqueAsync(IFichiersLookup fichiers, VersionPlan v)
    {
        var categories = await fichiers.CategoriesAsync(VersionPlan.Contexte, v.Id);
        var manque = new List<string>();
        if (!categories.Contains(VersionPlan.Plan)) manque.Add("le plan d'exécution");
        if (!categories.Contains(VersionPlan.Modele)) manque.Add("le modèle de calcul");
        return manque.Count == 0 ? null : $"Il manque {string.Join(" et ", manque)} dans le dossier {v.Lettre}.";
    }

    /// <summary>Dalle à modifier, ou le refus à renvoyer : introuvable, ou projet supprimé.</summary>
    private static async Task<(Dalle? Dalle, IResult? Refus)> OuvrirAsync(ProjetsDbContext db, Guid dalleId)
    {
        var dalle = await db.Dalles.FirstOrDefaultAsync(d => d.Id == dalleId);
        if (dalle is null) return (null, Results.NotFound());
        var projet = await db.Projets.AsNoTracking().FirstAsync(p => p.Id == dalle.ProjetId);
        return projet.Supprime ? (null, ProjetsEndpoints.Conflit("Un projet supprimé est en lecture seule.")) : (dalle, null);
    }

    private static async Task<(VersionPlan? Dossier, Dalle? Dalle, IResult? Refus)> DossierAsync(ProjetsDbContext db, Guid id)
    {
        var v = await db.VersionsPlans.FirstOrDefaultAsync(x => x.Id == id);
        if (v is null) return (null, null, Results.NotFound());
        var (dalle, refus) = await OuvrirAsync(db, v.DalleId);
        return dalle is null ? (null, null, refus) : (v, dalle, null);
    }

    private static async Task<IResult> DetailAsync(ProjetsDbContext db, IFichiersLookup fichiers, Guid dalleId, ClaimsPrincipal user)
    {
        var d = await db.Dalles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dalleId);
        if (d is null) return Results.NotFound();
        var p = await db.Projets.AsNoTracking().FirstAsync(x => x.Id == d.ProjetId);
        var toutes = await db.AffectationsExecution.AsNoTracking().Where(a => a.DalleId == dalleId).OrderByDescending(a => a.CreatedAt).ToListAsync();
        var actives = toutes.Where(a => a.Fin == null).OrderBy(a => a.CreatedAt).ToList();
        var versions = await db.VersionsPlans.AsNoTracking().Where(v => v.DalleId == dalleId).OrderByDescending(v => v.Lettre).ToListAsync();
        var responsable = Responsable(user);
        var agit = responsable || actives.Any(a => a.UserId == user.UserId());
        var fige = d.EtapesFaites >= Etapes.ValidationBct;
        var dossiers = new List<object>();
        foreach (var (v, i) in versions.Select((v, i) => (v, i)))
        {
            var type = ExecutionDalle.Type(i == versions.Count - 1);
            var dernier = i == 0;
            dossiers.Add(new
            {
                id = v.Id, lettre = v.Lettre.ToString(), reference = $"{p.Code}-{d.Numero:00}{v.Lettre}", type, typeLabel = ExecutionDalle.Types[type],
                etat = v.Etat, etatLabel = ExecutionDalle.Labels[v.Etat], commentaire = v.Commentaire,
                ouvertLe = v.CreatedAt, ouvertPar = v.CreePar, termineLe = v.TermineLe, terminePar = v.TerminePar,
                surface = v.Surface, dessinateurs = v.DessinateursNoms, arretMotif = v.ArretMotif, envoyeLe = v.EnvoyeLe,
                pieces = await fichiers.CategoriesAsync(VersionPlan.Contexte, v.Id),
                dernier,
                peutDeposer = !p.Supprime && dernier && !fige && v.Etat == ExecutionDalle.EnCours && FichiersPlans.Roles.Any(user.IsInRole),
                peutTerminer = !p.Supprime && v.Etat == ExecutionDalle.EnCours && agit,
                peutRouvrir = !p.Supprime && responsable && dernier && !fige && v.Etat == ExecutionDalle.Termine,
                peutArreter = !p.Supprime && responsable && v.Etat == ExecutionDalle.EnCours,
                peutReprendre = !p.Supprime && responsable && dernier && v.Etat == ExecutionDalle.Arrete,
                peutNoterEnvoi = !p.Supprime && agit && v.Etat == ExecutionDalle.Termine,
            });
        }
        return Results.Ok(new
        {
            execution = Vue(d, p, actives, versions, user.UserId(), responsable),
            dossiers,
            historique = toutes.Select(a => new { nom = a.UserNom, par = a.AffectePar, debut = a.Debut, fin = a.Fin, retirePar = a.RetirePar }),
        });
    }

    private static object Vue(Dalle d, Projet p, List<AffectationExecution> actives, List<VersionPlan> versions, Guid moi, bool responsable)
    {
        var dernier = versions.OrderByDescending(v => v.Lettre).FirstOrDefault();
        var etat = ExecutionDalle.Etat(dernier?.Etat, actives.Count);
        var agit = responsable || actives.Any(a => a.UserId == moi);
        var fige = d.EtapesFaites >= Etapes.ValidationBct;
        var type = ExecutionDalle.Type(versions.Count <= 1);
        return new
        {
            dalleId = d.Id, projetId = p.Id, projetCode = p.Code, projetLibelle = p.Libelle, ville = p.Ville, supprime = p.Supprime,
            reference = $"{p.Code}-{d.Suffixe}", niveauLabel = Dalle.NiveauLabel(d.Niveau), surface = d.Surface,
            etat, etatLabel = ExecutionDalle.Labels[etat],
            dossier = dernier is null ? null : new { id = dernier.Id, lettre = dernier.Lettre.ToString(), type, typeLabel = ExecutionDalle.Types[type], termineLe = dernier.TermineLe, envoyeLe = dernier.EnvoyeLe },
            dossiers = versions.Count,
            dessinateurs = actives.Select(a => new { userId = a.UserId, nom = a.UserNom, depuis = a.Debut }),
            prevue = d.EtudePlanifiee,
            enRetard = dernier is null && d.EtudePlanifiee is { } prevue && prevue < Jour(DateTime.UtcNow),
            fige,
            peutAffecter = !p.Supprime && responsable,
            peutOuvrir = !p.Supprime && !fige && agit && ExecutionDalle.RefusOuvrir(dernier?.Etat, dernier?.Lettre, agit) is null,
        };
    }
}
