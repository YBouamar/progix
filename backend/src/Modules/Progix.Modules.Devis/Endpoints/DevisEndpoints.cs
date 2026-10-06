using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Devis.Domain;
using Progix.Modules.Devis.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Devis.Endpoints;

public record CreationInput(Guid ProjetId);
public record SystemeInput(int Torons, decimal Ancrages, decimal Gaine);
public record LigneInput(Guid Id, decimal Surface, decimal RatioActif, decimal RatioPassif, List<SystemeInput>? Systemes);
public record EcheanceInput(string? Label, decimal Pourcentage, bool Retenue);
public record ParametresDevisInput(int DureeValidite, string? Modalites, List<EcheanceInput>? Echeances);
public record DevisInput(DateOnly DateDevis, int DureeValidite, Guid? CommercialId, decimal RemiseM2, string? RemiseMotif,
    List<EcheanceInput>? Echeances, string? ModalitesContrat, List<LigneInput>? Lignes);

public static class DevisEndpoints
{
    private static readonly string[] RolesCommerciaux = [Roles.AgentCommercial];

    private const string ContratParDefaut =
        "Délais des études : plan d'exécution en 5 jours par dalle après réception du plan de coffrage, des réservations et des hypothèses de charges.\n" +
        "Modification des plans d'exécution : 10 DH par m². Béton : fck = 35 MPa.";

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/devis").WithTags("Devis").RequireAuthorization(Policies.Devis);

        g.MapGet("/commerciaux", async (IUtilisateursLookup users) =>
            Results.Ok((await users.ParRolesAsync(RolesCommerciaux)).Select(u => new { id = u.Id, nom = u.Nom })));

        // Tableau de bord commercial : devis en attente, à relancer, proches de l'expiration.
        // Valeurs proposées à la création d'un devis.
        app.MapGet("/api/parametres/devis", async (DevisDbContext db, ClaimsPrincipal user) => Results.Ok(ParametresDto(await db.ParametresAsync(), user)))
            .WithTags("Paramétrage").RequireAuthorization();
        app.MapPut("/api/parametres/devis", async (ParametresDevisInput input, DevisDbContext db, ClaimsPrincipal user) =>
        {
            if (!PeutParametrer(user)) return Results.Forbid();
            if (input.DureeValidite is < 1 or > 365) return Conflit("La durée de validité est comprise entre 1 et 365 jours.");
            var echeances = input.Echeances ?? [];
            if (echeances.Any(x => x.Pourcentage <= 0 || string.IsNullOrWhiteSpace(x.Label))) return Conflit("Chaque échéance a un libellé et un pourcentage supérieur à 0.");
            if (echeances.Sum(x => x.Pourcentage) > 100) return Conflit("Le total des échéances ne peut pas dépasser 100 %.");
            if (echeances.Count(x => x.Retenue) > 1) return Conflit("Une seule retenue de garantie est possible.");
            var p = await db.Parametres.FirstOrDefaultAsync();
            if (p is null) db.Parametres.Add(p = new ParametresDevis());
            p.DureeValidite = input.DureeValidite;
            p.Modalites = string.IsNullOrWhiteSpace(input.Modalites) ? null : input.Modalites.Trim();
            p.Echeances = echeances.Select(x => new Echeance { Label = x.Label!.Trim(), Pourcentage = x.Pourcentage, Retenue = x.Retenue }).ToList();
            p.ModifiePar = user.UserName(); p.ModifieLe = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(ParametresDto(p, user));
        }).WithTags("Paramétrage").RequireAuthorization();

        g.MapGet("/tableau-de-bord", async (DevisDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires, string? entite) =>
        {
            await db.ExpirerAsync();
            var jour = DateOnly.FromDateTime(DateTime.UtcNow);
            var debutMois = new DateTime(jour.Year, jour.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var tous = await db.Devis.AsNoTracking().Where(d => string.IsNullOrEmpty(entite) || d.Entite == entite).ToListAsync();
            var attente = tous.Where(d => d.Etat == EtatsDevis.Envoye).ToList();
            var relancer = attente.Where(d => d.EnvoyeLe is { } e && jour.DayNumber - DateOnly.FromDateTime(e).DayNumber >= DevisEtatsEndpoints.JoursAvantRelance).OrderBy(d => d.EnvoyeLe).ToList();
            var expirent = attente.Where(d => d.Echeance.DayNumber - jour.DayNumber <= 7).OrderBy(d => d.Echeance).ToList();
            var infos = await projets.ProjetsAsync(attente.Select(d => d.ProjetId));
            var noms = await partenaires.NomsAsync(infos.Values.Select(p => p.ClientId));
            object Ligne(DevisEntete d) => new
            {
                id = d.Id, numero = d.Numero, version = d.Version, totalTTC = d.TotalTTC, echeance = d.Echeance, envoyeLe = d.EnvoyeLe,
                client = infos.GetValueOrDefault(d.ProjetId) is { } p ? noms.GetValueOrDefault(p.ClientId, "") : "",
                joursDepuisEnvoi = d.EnvoyeLe is { } e ? jour.DayNumber - DateOnly.FromDateTime(e).DayNumber : 0, joursAvantExpiration = d.Echeance.DayNumber - jour.DayNumber,
            };
            return Results.Ok(new
            {
                enAttente = attente.Count, aRelancer = relancer.Select(Ligne), expirent = expirent.Select(Ligne),
                validesMois = tous.Count(d => d.ValideLe >= debutMois), envoyesMois = tous.Count(d => d.EnvoyeLe >= debutMois),
            });
        });

        g.MapGet("/", async (DevisDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires,
            string? etat, Guid? projetId, string? q, int page = 1, int pageSize = 20) =>
        {
            await db.ExpirerAsync();
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var query = db.Devis.AsNoTracking();
            if (projetId is Guid pid) query = query.Where(d => d.ProjetId == pid);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var like = $"%{q.Trim()}%";
                var ids = await projets.RechercherAsync(q);
                query = query.Where(d => EF.Functions.ILike(d.Numero, like) || ids.Contains(d.ProjetId));
            }
            var parEtat = await query.GroupBy(d => d.Etat).Select(x => new { Etat = x.Key, Nb = x.Count() }).ToListAsync();
            if (!string.IsNullOrWhiteSpace(etat)) query = query.Where(d => d.Etat == etat);
            var total = await query.CountAsync();
            var rows = await query.OrderByDescending(d => d.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var infos = await projets.ProjetsAsync(rows.Select(r => r.ProjetId));
            var noms = await partenaires.NomsAsync(infos.Values.Select(p => p.ClientId));
            return Results.Ok(new
            {
                items = rows.Select(d =>
                {
                    var p = infos.GetValueOrDefault(d.ProjetId);
                    return new
                    {
                        id = d.Id, numero = d.Numero, entite = d.Entite, version = d.Version, projetId = d.ProjetId,
                        projet = p?.Libelle ?? "", projetCode = p?.Code ?? "", client = p is null ? "" : noms.GetValueOrDefault(p.ClientId, ""),
                        commercial = d.CommercialNom, etat = d.Etat, etatLabel = EtatsDevis.Labels.GetValueOrDefault(d.Etat, d.Etat),
                        dateDevis = d.DateDevis, echeance = d.Echeance, totalTTC = d.TotalTTC,
            info = DevisEtatsEndpoints.Info(d), alerte = DevisEtatsEndpoints.Alerte(d),
                    };
                }),
                total, page, pageSize,
                compteurs = EtatsDevis.Labels.Select(e => new { etat = e.Key, label = e.Value, nb = parEtat.FirstOrDefault(x => x.Etat == e.Key)?.Nb ?? 0 }),
            });
        });

        g.MapGet("/{id:guid}", async (Guid id, DevisDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires, ClaimsPrincipal user) =>
        {
            await db.ExpirerAsync();
            var d = await db.Devis.AsNoTracking().Include(x => x.Lignes).Include(x => x.Evenements).FirstOrDefaultAsync(x => x.Id == id);
            return d is null ? Results.NotFound() : Results.Ok(await Detail(d, projets, partenaires, user, db));
        });

        g.MapPost("/", async (CreationInput input, DevisDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires, ClaimsPrincipal user) =>
        {
            var projet = await projets.ProjetAsync(input.ProjetId, avecDalles: true);
            if (projet is null) return Results.NotFound();
            if (projet.Supprime) return Conflit("Un projet supprimé est en lecture seule.");
            if (projet.Dalles.Count == 0) return Conflit("Ajoutez au moins une dalle au projet avant de créer un devis.");

            var aujourdhui = DateOnly.FromDateTime(DateTime.UtcNow);
            await using var tx = await db.Database.BeginTransactionAsync();
            var prm = await db.ParametresAsync();
            var d = new DevisEntete
            {
                Entite = projet.Entite, Numero = $"DEV-{await db.NumeroSuivantAsync($"devis:{projet.Entite}:{aujourdhui.Year}"):00000}-{aujourdhui.Year}", ProjetId = projet.Id, DateDevis = aujourdhui,
                CreePar = user.UserName(), ModalitesContrat = prm.Modalites, DureeValidite = prm.DureeValidite,
                Evenements = [new EvenementDevis { Quoi = "Devis créé", Qui = user.UserName() }],
                Echeances = prm.Echeances.Select(x => new Echeance { Label = x.Label, Pourcentage = x.Pourcentage, Retenue = x.Retenue }).ToList(),
                Lignes = projet.Dalles.Select((dalle, i) => new LigneDevis
                {
                    DalleId = dalle.Id, Ordre = i, Reference = dalle.Reference, Emplacement = dalle.Emplacement, Surface = dalle.Surface,
                }).ToList(),
            };
            Calcul.Recalculer(d, projet.TypeConstruction, await Exonere(projet, partenaires));
            db.Devis.Add(d);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Created($"/api/devis/{d.Id}", await Detail(d, projets, partenaires, user, db));
        });

        g.MapPut("/{id:guid}", async (Guid id, DevisInput input, DevisDbContext db, IProjetsLookup projets,
            IPartenairesLookup partenaires, IUtilisateursLookup users, ClaimsPrincipal user) =>
        {
            var d = await db.Devis.Include(x => x.Lignes).Include(x => x.Evenements).FirstOrDefaultAsync(x => x.Id == id);
            if (d is null) return Results.NotFound();
            if (d.Etat != EtatsDevis.Brouillon) return Conflit("Seul un devis en brouillon est modifiable. Créez une nouvelle version.");
            var projet = await projets.ProjetAsync(d.ProjetId, avecDalles: true);
            if (projet is null) return Results.NotFound();

            var e = new Dictionary<string, string[]>();
            if (input.DureeValidite is < 1 or > 365) e["dureeValidite"] = ["La durée de validité est comprise entre 1 et 365 jours."];
            if (input.RemiseM2 < 0) e["remiseM2"] = ["La remise ne peut pas être négative."];
            if (input.RemiseM2 > 0 && string.IsNullOrWhiteSpace(input.RemiseMotif)) e["remiseMotif"] = ["Le motif est obligatoire dès qu'une remise est saisie."];
            var echeances = input.Echeances ?? [];
            if (echeances.Any(x => x.Pourcentage <= 0 || string.IsNullOrWhiteSpace(x.Label))) e["echeances"] = ["Chaque échéance a un libellé et un pourcentage supérieur à 0."];
            else if (echeances.Sum(x => x.Pourcentage) > 100) e["echeances"] = ["Le total des échéances ne peut pas dépasser 100 %."];
            else if (echeances.Count(x => x.Retenue) > 1) e["echeances"] = ["Une seule retenue de garantie est possible."];
            foreach (var l in input.Lignes ?? [])
            {
                if (l.RatioActif < 0 || l.RatioPassif < 0 || (l.Systemes ?? []).Any(s => s.Torons is < 1 or > 99 || s.Ancrages < 0 || s.Gaine < 0))
                    e["lignes"] = ["Les ratios, ancrages et gaines ne peuvent pas être négatifs."];
            }
            if (e.Count > 0) return Results.ValidationProblem(e);

            d.DateDevis = input.DateDevis;
            d.DureeValidite = input.DureeValidite;
            if (input.CommercialId is Guid cid)
            {
                var c = (await users.ParRolesAsync(RolesCommerciaux)).FirstOrDefault(u => u.Id == cid);
                if (c is null) return Conflit("Commercial introuvable.");
                d.CommercialId = c.Id; d.CommercialNom = c.Nom;
            }
            else { d.CommercialId = null; d.CommercialNom = null; }
            d.RemiseM2 = Math.Round(input.RemiseM2, 2);
            d.RemiseMotif = d.RemiseM2 > 0 ? input.RemiseMotif!.Trim() : null;
            d.Echeances = echeances.Select(x => new Echeance { Label = x.Label!.Trim(), Pourcentage = x.Pourcentage, Retenue = x.Retenue }).ToList();
            d.ModalitesContrat = string.IsNullOrWhiteSpace(input.ModalitesContrat) ? null : input.ModalitesContrat.Trim();
            foreach (var li in input.Lignes ?? [])
            {
                var l = d.Lignes.FirstOrDefault(x => x.Id == li.Id);
                if (l is null) continue;
                l.RatioActif = li.RatioActif;
                l.RatioPassif = li.RatioPassif;
                l.Systemes = (li.Systemes ?? []).Where(s => s.Ancrages > 0 || s.Gaine > 0).GroupBy(s => s.Torons).Select(x => x.First())
                    .OrderBy(s => s.Torons).Select(s => new SystemeLigne { Torons = s.Torons, Ancrages = s.Ancrages, Gaine = s.Gaine }).ToList();
            }
            // La surface vient toujours de la dalle : un brouillon reprend la surface à jour du projet.
            foreach (var l in d.Lignes)
                if (projet.Dalles.FirstOrDefault(x => x.Id == l.DalleId) is { } dalle) { l.Surface = dalle.Surface; l.Reference = dalle.Reference; l.Emplacement = dalle.Emplacement; }
            Calcul.Recalculer(d, projet.TypeConstruction, await Exonere(projet, partenaires));
            d.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(await Detail(d, projets, partenaires, user, db));
        });
    }

    private static bool PeutParametrer(ClaimsPrincipal u) => u.IsInRole(Roles.Directeur) || u.IsInRole(Roles.ChefProjet) || u.IsInRole(Roles.ResponsableAdministratif);
    private static object ParametresDto(ParametresDevis p, ClaimsPrincipal user) => new
    {
        dureeValidite = p.DureeValidite, modalites = p.Modalites, modifiable = PeutParametrer(user), modifiePar = p.ModifiePar, modifieLe = p.ModifieLe,
        echeances = p.Echeances.Select(x => new { label = x.Label, pourcentage = x.Pourcentage, retenue = x.Retenue }),
    };

    internal static IResult Conflit(string message) => Results.Conflict(new { message });

    internal static async Task<bool> Exonere(ProjetResume projet, IPartenairesLookup partenaires) =>
        (await partenaires.FichesAsync([projet.ClientId])).GetValueOrDefault(projet.ClientId)?.ExonereTva ?? false;

    internal static async Task<object> Detail(DevisEntete d, IProjetsLookup projets, IPartenairesLookup partenaires, ClaimsPrincipal user, DevisDbContext db)
    {
        var derniere = !await db.Devis.AnyAsync(x => x.Entite == d.Entite && x.Numero == d.Numero && x.Version > d.Version);
        var p = await projets.ProjetAsync(d.ProjetId);
        var client = p is null ? null : (await partenaires.FichesAsync([p.ClientId])).GetValueOrDefault(p.ClientId);
        return new
        {
            id = d.Id, numero = d.Numero, version = d.Version, etat = d.Etat, etatLabel = EtatsDevis.Labels.GetValueOrDefault(d.Etat, d.Etat),
            projetId = d.ProjetId, projet = p?.Libelle, projetCode = p?.Code, entite = p?.Entite, typeConstruction = p?.TypeConstruction,
            client = client?.Nom, exonereTva = d.ExonereTva,
            dateDevis = d.DateDevis, dureeValidite = d.DureeValidite, echeance = d.Echeance,
            commercialId = d.CommercialId, commercial = d.CommercialNom,
            remiseM2 = d.RemiseM2, remiseMotif = d.RemiseMotif,
            echeances = d.Echeances.Select(x => new { label = x.Label, pourcentage = x.Pourcentage, retenue = x.Retenue }),
            modalitesContrat = d.ModalitesContrat,
            forfait = d.Forfait, surfaceTotale = d.SurfaceTotale, totalHT = d.TotalHT, remiseTotale = d.RemiseTotale,
            netHT = d.NetHT, tva = d.Tva, totalTTC = d.TotalTTC, creePar = d.CreePar, createdAt = d.CreatedAt,
            signePar = d.SignePar, signeLe = d.SigneLe, signable = d.Etat != EtatsDevis.Brouillon && d.Jeton is null && DevisEtatsEndpoints.PeutSigner(user),
            info = DevisEtatsEndpoints.Info(d), alerte = DevisEtatsEndpoints.Alerte(d), annuleMotif = d.AnnuleMotif,
            actions = DevisEtatsEndpoints.Actions(d, user, derniere),
            historique = d.Evenements.OrderByDescending(e => e.CreatedAt).Select(e => new { quand = e.CreatedAt, quoi = e.Quoi, qui = e.Qui }),
            lignes = d.Lignes.OrderBy(l => l.Ordre).Select(l => new
            {
                id = l.Id, reference = l.Reference, emplacement = l.Emplacement, surface = l.Surface,
                ratioActif = l.RatioActif, ratioPassif = l.RatioPassif, prixM2 = l.PrixM2, montantHT = l.MontantHT,
                systemes = l.Systemes.Select(s => new { torons = s.Torons, ancrages = s.Ancrages, gaine = s.Gaine }),
            }),
        };
    }
}
