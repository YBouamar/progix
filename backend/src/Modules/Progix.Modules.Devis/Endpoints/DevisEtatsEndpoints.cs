using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Devis.Domain;
using Progix.Modules.Devis.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Devis.Endpoints;

public record MotifInput(string? Motif);

/// <summary>Cycle de vie du devis : envoi, validation, rejet, nouvelle version.</summary>
public record SignatureDevisInput(string? MotDePasse);

public static class DevisEtatsEndpoints
{
    public const int JoursAvantRelance = 7;

    private static readonly string[] RolesValidation = [Roles.Directeur, Roles.ChefProjet, Roles.ResponsableAdministratif];

    /// <summary>Rejet d'un devis, quel que soit son état : responsable administratif, chef de projet, directeur.</summary>
    private static readonly string[] RolesRejet = [Roles.Directeur, Roles.ChefProjet, Roles.ResponsableAdministratif];

    public static bool PeutSigner(ClaimsPrincipal user) => RolesValidation.Any(user.IsInRole);

    /// <summary>
    /// Création et modification d'un devis (contenu, envoi, nouvelle version, import Excel) : tous les rôles du devis sauf l'administrateur,
    /// qui consulte seulement les devis et leur PDF.
    /// </summary>
    public static bool PeutEcrire(ClaimsPrincipal user) => !user.IsInRole(Roles.Administrateur);

    private static DateOnly Aujourdhui => DateOnly.FromDateTime(DateTime.UtcNow);

    public static string Info(DevisEntete d) => d.Etat switch
    {
        EtatsDevis.Brouillon => "Non envoyé",
        EtatsDevis.Envoye => $"Échéance le {d.Echeance:dd/MM/yyyy}",
        EtatsDevis.Expire => $"Expiré le {d.Echeance:dd/MM/yyyy}",
        EtatsDevis.Valide => $"Validé le {d.ValideLe:dd/MM/yyyy} par {d.ValidePar}",
        EtatsDevis.Annule => $"Rejeté le {d.AnnuleLe:dd/MM/yyyy}",
        EtatsDevis.Remplace => $"Remplacé le {d.RemplaceLe:dd/MM/yyyy}",
        _ => "",
    };

    /// <summary>Signalement sur un devis envoyé : expiration proche, ou relance à faire à partir de 7 jours.</summary>
    public static string? Alerte(DevisEntete d)
    {
        if (d.Etat != EtatsDevis.Envoye || d.EnvoyeLe is null) return null;
        var reste = d.Echeance.DayNumber - Aujourdhui.DayNumber;
        if (reste == 0) return "expire aujourd'hui";
        if (reste == 1) return "expire demain";
        return Aujourdhui.DayNumber - DateOnly.FromDateTime(d.EnvoyeLe.Value).DayNumber >= JoursAvantRelance ? "à relancer" : null;
    }

    public static object Actions(DevisEntete d, ClaimsPrincipal user, bool derniereVersion) => new
    {
        modifier = d.Etat == EtatsDevis.Brouillon && PeutEcrire(user),
        // La trame de calcul se télécharge à tout moment ; elle se réimporte seulement dans un brouillon.
        trame = PeutEcrire(user),
        envoyer = d.Etat == EtatsDevis.Brouillon && PeutEcrire(user),
        valider = d.Etat == EtatsDevis.Envoye && RolesValidation.Any(user.IsInRole),
        rejeter = d.Etat is EtatsDevis.Brouillon or EtatsDevis.Envoye or EtatsDevis.Expire or EtatsDevis.Valide && RolesRejet.Any(user.IsInRole),
        nouvelleVersion = derniereVersion && d.Etat is EtatsDevis.Envoye or EtatsDevis.Expire or EtatsDevis.Valide && PeutEcrire(user),
    };

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/devis/{id:guid}").WithTags("Devis").RequireAuthorization(Policies.Devis);

        // Signature électronique : exigée avant d'imprimer ou d'envoyer le devis. Un brouillon, encore modifiable, ne se signe pas.
        g.MapPost("/signer", async (Guid id, SignatureDevisInput input, DevisDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires, IUtilisateursLookup users, ClaimsPrincipal user) =>
        {
            if (!PeutSigner(user)) return Results.Forbid();
            var d = await db.Devis.Include(x => x.Lignes).Include(x => x.Evenements).FirstOrDefaultAsync(x => x.Id == id);
            if (d is null) return Results.NotFound();
            if (d.Etat == EtatsDevis.Brouillon) return DevisEndpoints.Conflit("Un brouillon reste modifiable : marquez le devis comme envoyé avant de le signer.");
            if (d.Jeton is not null) return DevisEndpoints.Conflit("Ce devis est déjà signé.");
            if (string.IsNullOrEmpty(input.MotDePasse) || !await users.VerifierMotDePasseAsync(user.UserId(), input.MotDePasse))
                return DevisEndpoints.Conflit("Mot de passe incorrect : il signe électroniquement le document.");
            d.SignePar = user.UserName(); d.SigneLe = DateTime.UtcNow;
            d.Jeton = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            Tracer(db, d, user, "Devis signé électroniquement");
            await db.SaveChangesAsync();
            return Results.Ok(await DevisEndpoints.Detail(d, projets, partenaires, user, db));
        });

        g.MapPost("/envoyer", (Guid id, DevisDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires, ClaimsPrincipal user) =>
            Transition(id, db, projets, partenaires, user, d =>
            {
                if (!PeutEcrire(user)) return "Votre rôle permet de consulter les devis, pas de les envoyer.";
                if (d.Etat != EtatsDevis.Brouillon) return "Seul un brouillon peut être envoyé.";
                if (d.Lignes.FirstOrDefault(l => l.Surface <= 0) is { } sansSurface)
                    return $"La dalle {sansSurface.Reference} n'a pas de surface : renseignez-la ou importez ses fichiers ADAPT avant l'envoi.";
                if (d.Echeances.Sum(e => e.Pourcentage) != 100) return "Le total des modalités de paiement doit faire 100 % avant l'envoi.";
                if (d.Echeance < Aujourdhui) return "L'échéance du devis est déjà passée : corrigez la date ou la durée de validité.";
                d.Etat = EtatsDevis.Envoye;
                d.EnvoyeLe = DateTime.UtcNow;
                Tracer(db, d, user, $"Devis envoyé, échéance le {d.Echeance:dd/MM/yyyy}");
                return null;
            }));

        g.MapPost("/valider", (Guid id, DevisDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires, IProjetsStatut statut, ClaimsPrincipal user) =>
            Transition(id, db, projets, partenaires, user, d =>
            {
                if (!RolesValidation.Any(user.IsInRole)) return "Vous n'avez pas le droit de valider un devis.";
                if (d.Etat == EtatsDevis.Expire) return "Ce devis est expiré : créez une nouvelle version.";
                if (d.Etat != EtatsDevis.Envoye) return "Seul un devis envoyé peut être validé.";
                d.Etat = EtatsDevis.Valide;
                d.ValideLe = DateTime.UtcNow;
                d.ValidePar = user.UserName();
                Tracer(db, d, user, "Devis validé");
                return null;
            }, d => statut.PasserEnEtudeAsync(d.ProjetId)));

        g.MapPost("/annuler", (Guid id, MotifInput input, DevisDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires, IProjetsStatut statut, ClaimsPrincipal user) =>
            Transition(id, db, projets, partenaires, user, d =>
            {
                if (!RolesRejet.Any(user.IsInRole)) return "Un devis ne peut être rejeté que par le responsable administratif, le chef de projet ou le directeur.";
                if (d.Etat is not (EtatsDevis.Brouillon or EtatsDevis.Envoye or EtatsDevis.Expire or EtatsDevis.Valide)) return "Ce devis ne peut plus être rejeté.";
                if (string.IsNullOrWhiteSpace(input.Motif)) return "Le motif est obligatoire.";
                d.Etat = EtatsDevis.Annule;
                d.AnnuleLe = DateTime.UtcNow;
                d.AnnuleMotif = input.Motif.Trim();
                Tracer(db, d, user, $"Devis rejeté. Motif : {d.AnnuleMotif}");
                return null;
            }, async d =>
            {
                // Sans aucun devis validé, le projet n'est plus en étude.
                if (!await db.Devis.AnyAsync(x => x.ProjetId == d.ProjetId && x.Etat == EtatsDevis.Valide)) await statut.RevenirCreeAsync(d.ProjetId);
            }));

        // Nouvelle version : copie en brouillon avec les dalles et surfaces à jour du projet ; l'ancienne version est remplacée.
        g.MapPost("/nouvelle-version", async (Guid id, DevisDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires, ClaimsPrincipal user) =>
        {
            if (!PeutEcrire(user)) return Results.Forbid();
            var d = await db.Devis.Include(x => x.Lignes).Include(x => x.Evenements).FirstOrDefaultAsync(x => x.Id == id);
            if (d is null) return Results.NotFound();
            if (d.Etat is not (EtatsDevis.Envoye or EtatsDevis.Expire or EtatsDevis.Valide))
                return DevisEndpoints.Conflit("Une nouvelle version se crée à partir d'un devis envoyé, expiré ou validé.");
            if (await db.Devis.AnyAsync(x => x.Entite == d.Entite && x.Numero == d.Numero && x.Version > d.Version))
                return DevisEndpoints.Conflit("Une version plus récente de ce devis existe déjà.");
            var projet = await projets.ProjetAsync(d.ProjetId, avecDalles: true);
            if (projet is null) return Results.NotFound();
            if (projet.Supprime) return DevisEndpoints.Conflit("Un projet supprimé est en lecture seule.");

            var n = new DevisEntete
            {
                Numero = d.Numero, Entite = d.Entite, Version = d.Version + 1, ProjetId = d.ProjetId, DateDevis = Aujourdhui, DureeValidite = d.DureeValidite,
                CommercialId = projet.CommercialId, CommercialNom = projet.CommercialNom, IngenieurId = d.IngenieurId, IngenieurNom = d.IngenieurNom, RemiseM2 = d.RemiseM2, RemiseMotif = d.RemiseMotif,
                Echeances = d.Echeances.Select(e => new Echeance { Label = e.Label, Pourcentage = e.Pourcentage, Retenue = e.Retenue }).ToList(),
                ModalitesContrat = d.ModalitesContrat, CreePar = user.UserName(),
                Evenements = [new EvenementDevis { Quoi = $"Version {d.Version + 1} créée à partir de la version {d.Version}", Qui = user.UserName() }],
                Lignes = projet.Dalles.Select((dalle, i) =>
                {
                    var ancienne = d.Lignes.FirstOrDefault(l => l.DalleId == dalle.Id);
                    return new LigneDevis
                    {
                        DalleId = dalle.Id, Ordre = i, Reference = dalle.Reference, Emplacement = dalle.Emplacement, Surface = dalle.Surface,
                        RatioActif = ancienne?.RatioActif ?? 0, RatioPassif = ancienne?.RatioPassif ?? 0,
                        Systemes = (ancienne?.Systemes ?? []).Select(s => new SystemeLigne { Torons = s.Torons, Ancrages = s.Ancrages, Gaine = s.Gaine }).ToList(),
                    };
                }).ToList(),
            };
            Calcul.Recalculer(n, (await db.ParametresAsync()).Forfaits, await DevisEndpoints.Exonere(projet, partenaires));
            d.Etat = EtatsDevis.Remplace;
            d.RemplaceLe = DateTime.UtcNow;
            Tracer(db, d, user, $"Remplacé par la version {n.Version}");
            db.Devis.Add(n);
            await db.SaveChangesAsync();
            return Results.Ok(await DevisEndpoints.Detail(n, projets, partenaires, user, db));
        });
    }

    // Ajout explicite au contexte : la clé étant déjà renseignée, un simple ajout à la collection serait pris pour une modification.
    private static void Tracer(DevisDbContext db, DevisEntete d, ClaimsPrincipal user, string quoi) =>
        db.Evenements.Add(new EvenementDevis { DevisId = d.Id, Quoi = quoi, Qui = user.UserName() });

    private static async Task<IResult> Transition(Guid id, DevisDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires,
        ClaimsPrincipal user, Func<DevisEntete, string?> appliquer, Func<DevisEntete, Task>? apres = null)
    {
        await db.ExpirerAsync();
        var d = await db.Devis.Include(x => x.Lignes).Include(x => x.Evenements).FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return Results.NotFound();
        var refus = appliquer(d);
        if (refus is not null) return DevisEndpoints.Conflit(refus);
        d.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        if (apres is not null) await apres(d);
        return Results.Ok(await DevisEndpoints.Detail(d, projets, partenaires, user, db));
    }
}
