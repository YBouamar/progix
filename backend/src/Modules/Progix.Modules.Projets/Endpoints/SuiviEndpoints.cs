using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

public record RealisationInput(string? Remarque, string? MotDePasse);
public record ValidationInput(string Type);
public record DeverrouillageInput(string? Motif);

/// <summary>Suivi de chantier : les 11 étapes d'une dalle, dans l'ordre strict et sans exception.</summary>
public static class SuiviEndpoints
{
    private static bool Direction(ClaimsPrincipal u) => u.IsInRole(Roles.Directeur) || u.IsInRole(Roles.ChefProjet);
    private static bool PeutRealiser(ClaimsPrincipal u, Etapes.Definition e) => Direction(u) || e.Roles.Any(u.IsInRole);
    private static bool PeutValiderTechnique(ClaimsPrincipal u) => Direction(u);
    private static bool PeutValiderChantier(ClaimsPrincipal u) => Direction(u) || u.IsInRole(Roles.ResponsableChantier);
    private static bool PeutValiderAdministratif(ClaimsPrincipal u) => Direction(u) || u.IsInRole(Roles.ResponsableAdministratif);

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/projets/{id:guid}/dalles/{did:guid}/etapes").WithTags("Suivi de chantier").RequireAuthorization();

        g.MapGet("/", async (Guid id, Guid did, ProjetsDbContext db, ClaimsPrincipal user, IEncoursProjet encours) =>
        {
            var (projet, dalle) = await Charger(db, id, did, suivi: false);
            return projet is null || dalle is null ? Results.NotFound() : Results.Ok(await Vue(db, projet, dalle, user, encours));
        });

        g.MapPost("/{n:int}/realiser", async (Guid id, Guid did, int n, RealisationInput input, ProjetsDbContext db, IFichiersLookup fichiers, IUtilisateursLookup users, ClaimsPrincipal user, IEncoursProjet encours, INotificationsDirection notifications, ILoggerFactory logs) =>
        {
            var (projet, dalle) = await Charger(db, id, did, suivi: true);
            if (projet is null || dalle is null) return Results.NotFound();
            if (Refus(projet) is { } refus) return ProjetsEndpoints.Conflit(refus);
            if (n != dalle.EtapesFaites + 1) return ProjetsEndpoints.Conflit("Les étapes se réalisent dans l'ordre : terminez d'abord l'étape en cours.");
            var def = Etapes.Get(n);
            if (!PeutRealiser(user, def)) return Results.Forbid();
            if (n == 1 && await VersionsEndpoints.ManqueAsync(db, fichiers, did) is { } manque) return ProjetsEndpoints.Conflit(manque);
            if (FichiersPhotos.EtapesAvecPhoto.Contains(n) && !(await fichiers.CategoriesAsync(FichiersPhotos.Nom, did)).Contains(n.ToString()))
                return ProjetsEndpoints.Conflit("Ajoutez au moins une photo avant de valider cette étape.");
            if (n == FichiersRapport.NumeroEtape)
            {
                if (!await db.ProjetPartenaires.AnyAsync(x => x.ProjetId == id && x.Role == "laboratoire"))
                    return ProjetsEndpoints.Conflit("Le laboratoire du projet est requis : ajoutez-le dans les partenaires du projet.");
                if (await RapportsEcrasementEndpoints.RefusAsync(db, fichiers, did) is { } rapport) return ProjetsEndpoints.Conflit(rapport);
            }
            if (n == AutoControleEndpoints.Etape && await AutoControleEndpoints.RefusAsync(db, did) is { } ficheIncomplete) return ProjetsEndpoints.Conflit(ficheIncomplete);
            var remarque = string.IsNullOrWhiteSpace(input.Remarque) ? null : input.Remarque.Trim();
            if (n == Etapes.MiseEnTension && await TensionEndpoints.RefusAsync(db, did, remarque) is { } horsTolerance) return ProjetsEndpoints.Conflit(horsTolerance);

            if (n == Etapes.CommandeUsine && !await db.CommandesUsine.AnyAsync(c => c.DalleId == did && c.NbCables > 0))
                return ProjetsEndpoints.Conflit("Saisissez les câbles de la commande usine avant de la soumettre à validation.");
            if (n == Etapes.Injection)
            {
                if (await InjectionEndpoints.RefusAsync(db, did) is { } incomplet) return ProjetsEndpoints.Conflit(incomplet);
                // L'exécutant signe la fiche d'injection avec son mot de passe.
                if (string.IsNullOrEmpty(input.MotDePasse) || !await users.VerifierMotDePasseAsync(user.UserId(), input.MotDePasse))
                    return ProjetsEndpoints.Conflit("Mot de passe incorrect : il sert de signature à la fiche d'injection.");
                await InjectionEndpoints.SignerAsync(db, did, user.UserName());
            }

            var e = await db.Etapes.FirstOrDefaultAsync(x => x.DalleId == did && x.Numero == n);
            if (e?.FaitLe != null) return ProjetsEndpoints.Conflit("Cette étape est déjà réalisée et attend ses validations.");
            if (def.ValidationAvant && e?.Validee != true)
                return ProjetsEndpoints.Conflit(def.ValidationChantier ? "Les validations technique, administrative et du responsable des chantiers sont requises avant la saisie." : "La validation technique et la validation administrative sont requises avant la saisie.");
            if (e is null) db.Etapes.Add(e = new EtapeDalle { DalleId = did, Numero = n });
            e.FaitPar = user.UserName();
            e.FaitLe = DateTime.UtcNow;
            e.Remarque = remarque;
            if (e.Terminee) dalle.EtapesFaites = n;
            await db.SaveChangesAsync();
            await MajStatut(db, projet);
            if (e.Terminee && n is 6 or Etapes.MiseEnTension or Etapes.Injection)
                await PrevenirDirectionAsync(db, notifications, logs, projet, dalle, def, e);
            return Results.Ok(await Vue(db, projet, dalle, user, encours));
        });

        g.MapPost("/{n:int}/valider", async (Guid id, Guid did, int n, ValidationInput input, ProjetsDbContext db, ClaimsPrincipal user, IEncoursProjet encours) =>
        {
            var (projet, dalle) = await Charger(db, id, did, suivi: true);
            if (projet is null || dalle is null) return Results.NotFound();
            if (Refus(projet) is { } refus) return ProjetsEndpoints.Conflit(refus);
            var e = await db.Etapes.FirstOrDefaultAsync(x => x.DalleId == did && x.Numero == n);
            if (n < 1 || n > Etapes.Total || !Etapes.Get(n).DoubleValidation || n != dalle.EtapesFaites + 1 || (e is null && !Etapes.Get(n).ValidationAvant))
                return ProjetsEndpoints.Conflit("Cette étape n'attend pas de validation.");
            if (e is null) db.Etapes.Add(e = new EtapeDalle { DalleId = did, Numero = n });
            if (input.Type == "technique")
            {
                if (!PeutValiderTechnique(user)) return Results.Forbid();
                e.ValidationTechniquePar ??= user.UserName();
                e.ValidationTechniqueLe ??= DateTime.UtcNow;
            }
            else if (input.Type == "administrative")
            {
                if (!PeutValiderAdministratif(user)) return Results.Forbid();
                e.ValidationAdministrativePar ??= user.UserName();
                e.ValidationAdministrativeLe ??= DateTime.UtcNow;
            }
            else if (input.Type == "chantier" && Etapes.Get(n).ValidationChantier)
            {
                if (!PeutValiderChantier(user)) return Results.Forbid();
                e.ValidationChantierPar ??= user.UserName();
                e.ValidationChantierLe ??= DateTime.UtcNow;
            }
            else return Results.NotFound();
            if (e.Terminee) dalle.EtapesFaites = n;
            await db.SaveChangesAsync();
            await MajStatut(db, projet);
            return Results.Ok(await Vue(db, projet, dalle, user, encours));
        });

        // Déverrouillage : le directeur ou le chef de projet rouvre la dernière étape réalisée, avec un motif.
        g.MapPost("/{n:int}/deverrouiller", async (Guid id, Guid did, int n, DeverrouillageInput input, ProjetsDbContext db, ClaimsPrincipal user, IEncoursProjet encours) =>
        {
            if (!Direction(user)) return Results.Forbid();
            var (projet, dalle) = await Charger(db, id, did, suivi: true);
            if (projet is null || dalle is null) return Results.NotFound();
            if (projet.Supprime) return ProjetsEndpoints.Conflit("Un projet supprimé est en lecture seule.");
            if (string.IsNullOrWhiteSpace(input.Motif))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["motif"] = ["Le motif est obligatoire."] });
            var derniere = await db.Etapes.Where(x => x.DalleId == did).OrderByDescending(x => x.Numero).FirstOrDefaultAsync();
            if (derniere is null || derniere.Numero != n) return ProjetsEndpoints.Conflit("Seule la dernière étape réalisée peut être déverrouillée.");
            // Déverrouiller la commande usine la ramène avant validation : son suivi logistique repart de zéro.
            if (n == Etapes.CommandeUsine && await db.CommandesUsine.FirstOrDefaultAsync(c => c.DalleId == did) is { } commande)
                commande.FabricationLe = commande.LivreeLe = commande.RecueLe = null;
            db.Etapes.Remove(derniere);
            db.EtapeTraces.Add(new EtapeTrace { DalleId = did, Numero = n, Motif = input.Motif.Trim(), Par = user.UserName() });
            dalle.EtapesFaites = n - 1;
            await db.SaveChangesAsync();
            await MajStatut(db, projet);
            return Results.Ok(await Vue(db, projet, dalle, user, encours));
        });
    }

    private static async Task<(Projet?, Dalle?)> Charger(ProjetsDbContext db, Guid id, Guid did, bool suivi)
    {
        var projet = suivi ? await db.Projets.FirstOrDefaultAsync(p => p.Id == id) : await db.Projets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        var dalle = suivi ? await db.Dalles.FirstOrDefaultAsync(d => d.Id == did && d.ProjetId == id)
            : await db.Dalles.AsNoTracking().FirstOrDefaultAsync(d => d.Id == did && d.ProjetId == id);
        return (projet, dalle);
    }

    private static string? Refus(Projet p) =>
        p.Supprime ? "Un projet supprimé est en lecture seule."
        : p.Bloque ? "Projet bloqué : les étapes de chantier sont suspendues."
        : p.Statut == StatutsProjet.Cree ? "Le suivi de chantier commence après la validation d'un devis."
        : null;

    /// <summary>Le projet est en réalisation dès la réception d'un coffrage, terminé quand toutes ses dalles sont réalisées.</summary>
    private static async Task MajStatut(ProjetsDbContext db, Projet projet)
    {
        if (projet.Statut == StatutsProjet.Cree) return;
        var faites = await db.Dalles.Where(d => d.ProjetId == projet.Id).Select(d => d.EtapesFaites).ToListAsync();
        var statut = faites.Count > 0 && faites.All(f => f >= Etapes.Total) ? StatutsProjet.Termine
            : faites.Any(f => f >= Etapes.ReceptionCoffrage) ? StatutsProjet.EnRealisation
            : StatutsProjet.EnEtude;
        if (statut == projet.Statut) return;
        projet.Statut = statut;
        projet.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>Réception de dalle, mise en tension et injection : un courriel part aux directeurs. Un échec d'envoi ne bloque pas l'étape.</summary>
    private static async Task PrevenirDirectionAsync(ProjetsDbContext db, INotificationsDirection notifications, ILoggerFactory logs, Projet projet, Dalle dalle, Etapes.Definition def, EtapeDalle e)
    {
        var reference = $"{projet.Code}-{dalle.Suffixe}";
        try
        {
            var lignes = new List<KeyValuePair<string, string>>
            {
                new("Projet", $"{projet.Code} · {projet.Libelle}"), new("Dalle", reference), new("Étape", $"{def.Numero} · {def.Label}"),
                new("Réalisée par", $"{e.FaitPar}, le {e.FaitLe!.Value.AddHours(1):dd/MM/yyyy à HH:mm}"),
            };
            if (def.Numero == Etapes.MiseEnTension)
            {
                var s = TensionEndpoints.Calculer(await db.MesuresTension.AsNoTracking().Where(m => m.DalleId == dalle.Id).ToListAsync());
                lignes.Add(new("Allongements", s.Ecart is null ? "non calculés"
                    : $"écart du groupe {s.Ecart:+0.0;-0.0} %, {(s.Conforme == true && s.HorsTolerance == 0 ? "conforme" : $"{s.HorsTolerance} tendon(s) hors tolérance")}"));
            }
            if (!string.IsNullOrWhiteSpace(e.Remarque)) lignes.Add(new("Remarque", e.Remarque));
            await notifications.SignalerAsync($"{def.Label} réalisée · {reference}", $"{def.Label} réalisée sur la dalle {reference}", lignes, $"/projets/{projet.Id}#dalles");
        }
        catch (Exception ex)
        {
            logs.CreateLogger("Notifications").LogWarning(ex, "Courriel aux directeurs non préparé pour la dalle {Dalle}", reference);
        }
    }

    private static async Task<object> Vue(ProjetsDbContext db, Projet projet, Dalle dalle, ClaimsPrincipal user, IEncoursProjet encours)
    {
        var faites = await db.Etapes.AsNoTracking().Where(e => e.DalleId == dalle.Id).ToListAsync();
        var traces = await db.EtapeTraces.AsNoTracking().Where(t => t.DalleId == dalle.Id).OrderByDescending(t => t.CreatedAt).ToListAsync();
        var refus = Refus(projet);
        var courante = dalle.EtapesFaites + 1;
        var derniere = faites.Count == 0 ? 0 : faites.Max(e => e.Numero);
        var laboratoire = await db.ProjetPartenaires.AnyAsync(x => x.ProjetId == projet.Id && x.Role == "laboratoire");
        // Rappel avant l'injection : il informe, il ne bloque pas. Les montants ne vont qu'aux rôles qui les voient.
        object? rappelPaiement = null;
        if (dalle.EtapesFaites is >= 8 and < 10 && await encours.EncoursAsync(projet.Id) is { Factures: > 0 } du)
        {
            var montants = Roles.All.Any(r => r.VoitMontants && user.IsInRole(r.Code));
            rappelPaiement = new { factures = du.Factures, echues = du.Echues, reste = montants ? du.Reste : (decimal?)null, resteEchu = montants ? du.ResteEchu : (decimal?)null };
        }
        return new
        {
            rappelPaiement,
            dalle = DallesEndpoints.Dto(dalle, projet.Code),
            statutProjet = projet.Statut,
            suspendu = refus,
            laboratoire,
            etapes = Etapes.All.Select(def =>
            {
                var e = faites.FirstOrDefault(x => x.Numero == def.Numero);
                var attente = def.DoubleValidation && (def.ValidationAvant ? e?.Validee != true : e is not null && !e.Validee);
                var pretASaisir = def.ValidationAvant ? e?.Validee == true && e.FaitLe == null : e is null;
                var ouvert = refus is null && def.Numero == courante;
                return new
                {
                    numero = def.Numero, label = def.Label, doubleValidation = def.DoubleValidation, validationAvant = def.ValidationAvant, validationChantier = def.ValidationChantier,
                    validationChantierPar = e?.ValidationChantierPar, validationChantierLe = e?.ValidationChantierLe,
                    peutValiderChantier = ouvert && attente && def.ValidationChantier && e?.ValidationChantierLe is null && PeutValiderChantier(user),
                    etat = e?.Terminee == true ? "terminee" : def.Numero != courante ? "a_venir" : attente ? "validation" : "courante",
                    faitPar = e?.FaitPar, faitLe = e?.FaitLe, remarque = e?.Remarque,
                    validationTechniquePar = e?.ValidationTechniquePar, validationTechniqueLe = e?.ValidationTechniqueLe,
                    validationAdministrativePar = e?.ValidationAdministrativePar, validationAdministrativeLe = e?.ValidationAdministrativeLe,
                    peutRealiser = ouvert && pretASaisir && PeutRealiser(user, def),
                    peutValiderTechnique = ouvert && attente && e?.ValidationTechniqueLe is null && PeutValiderTechnique(user),
                    peutValiderAdministratif = ouvert && attente && e?.ValidationAdministrativeLe is null && PeutValiderAdministratif(user),
                    peutDeverrouiller = !projet.Supprime && e is not null && def.Numero == derniere && Direction(user),
                    deverrouillages = traces.Where(t => t.Numero == def.Numero).Select(t => new { motif = t.Motif, par = t.Par, date = t.CreatedAt }),
                };
            }),
        };
    }
}
