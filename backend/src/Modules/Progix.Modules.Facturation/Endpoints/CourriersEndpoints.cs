using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Progix.Modules.Facturation.Domain;
using Progix.Modules.Facturation.Infrastructure;
using Progix.Modules.Facturation.Pdf;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Endpoints;

public record LigneCourrierInput(Guid? DalleId, string? NumeroPlan, string? Designation, int Nombre);
public record CourrierInput(string Type, DateOnly DateCourrier, string? Lieu, string? Objet, string? Entreprise, bool Modification, List<LigneCourrierInput>? Lignes);

public static class CourriersEndpoints
{
    /// <summary>Bordereaux et attestations : bureau, direction et ingénieurs.</summary>
    public static readonly string[] RolesCourriers = [.. Policies.RolesCommercial, Roles.IngenieurChef, Roles.Ingenieur];

    /// <summary>Attestation de conformité, et avis de conformité des dalles : directeur, chef de projet, responsable administratif.</summary>
    public static readonly string[] RolesAttestation = [Roles.Directeur, Roles.ChefProjet, Roles.ResponsableAdministratif];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api").WithTags("Courriers").RequireAuthorization();

        g.MapGet("/projets/{id:guid}/courriers", async (Guid id, FacturationDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires, ClaimsPrincipal user) =>
        {
            if (!RolesCourriers.Any(user.IsInRole)) return Results.Forbid();
            var p = await projets.ProjetAsync(id, true);
            if (p is null) return Results.NotFound();
            var (bct, bet, client, _, _) = await Acteurs(p, projets, partenaires);
            var courriers = await db.Courriers.AsNoTracking().Where(c => c.ProjetId == id).OrderByDescending(c => c.CreatedAt).ToListAsync();
            return Results.Ok(new
            {
                lectureSeule = p.Supprime, peutAnnuler = EncaissementsEndpoints.RolesAnnulation.Any(user.IsInRole),
                peutConformite = RolesAttestation.Any(user.IsInRole),
                preparation = new
                {
                    objet = p.Libelle, entreprise = client, bct, bet, ville = p.Ville,
                    dalles = p.Dalles.Select(d => new { id = d.Id, reference = d.Reference, emplacement = d.Emplacement, surface = d.Surface, realisee = d.EtapesFaites >= 11, conforme = d.Conforme }),
                },
                types = TypesCourrier.Labels.Where(t => t.Key != TypesCourrier.Attestation || RolesAttestation.Any(user.IsInRole)).Select(t => new { code = t.Key, label = t.Value }),
                courriers = courriers.Select(Dto),
            });
        });

        g.MapPost("/projets/{id:guid}/courriers", async (Guid id, CourrierInput input, FacturationDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires, ClaimsPrincipal user) =>
        {
            if (!RolesCourriers.Any(user.IsInRole)) return Results.Forbid();
            var p = await projets.ProjetAsync(id, true);
            if (p is null) return Results.NotFound();
            if (p.Supprime) return Conflit("Un projet supprimé est en lecture seule.");
            if (!TypesCourrier.Labels.ContainsKey(input.Type)) return Conflit("Type de courrier inconnu.");
            var (bct, bet, client, bctId, betId) = await Acteurs(p, projets, partenaires);
            var attestation = input.Type == TypesCourrier.Attestation;
            if (attestation && !RolesAttestation.Any(user.IsInRole)) return Results.Forbid();
            if (input.Type != TypesCourrier.BordereauBet && bct is null) return Conflit("Aucun bureau de contrôle n'est rattaché au projet : ajoutez-le dans les partenaires du projet.");
            if (input.Type == TypesCourrier.BordereauBet && bet is null) return Conflit("Aucun bureau d'études n'est rattaché au projet : ajoutez-le dans les partenaires du projet.");
            var lignes = input.Lignes ?? [];
            if (lignes.Count == 0) return Conflit(attestation ? "Sélectionnez au moins une dalle à attester." : "Ajoutez au moins un plan au bordereau.");

            var c = new Courrier
            {
                Entite = p.Entite, Type = input.Type, ProjetId = p.Id, DateCourrier = input.DateCourrier, Lieu = string.IsNullOrWhiteSpace(input.Lieu) ? "Casablanca" : input.Lieu.Trim(),
                Objet = string.IsNullOrWhiteSpace(input.Objet) ? p.Libelle : input.Objet.Trim(), Entreprise = string.IsNullOrWhiteSpace(input.Entreprise) ? client : input.Entreprise.Trim(),
                Bct = bct, Bet = bet, Ville = p.Ville, DestinataireId = input.Type == TypesCourrier.BordereauBct ? bctId : input.Type == TypesCourrier.BordereauBet ? betId : null, Modification = input.Modification && !attestation, EmisPar = user.UserName(),
            };
            foreach (var l in lignes)
            {
                var d = p.Dalles.FirstOrDefault(x => x.Id == l.DalleId);
                if (l.DalleId is not null && d is null) return Conflit("Une dalle sélectionnée n'appartient pas à ce projet.");
                if (attestation)
                {
                    if (d is null) return Conflit("Une attestation porte sur des dalles du projet.");
                    // L'attestation affirme que pose, mise en tension et injection sont faites.
                    if (d.EtapesFaites < 11) return Conflit($"La dalle {d.Reference} n'est pas encore réalisée : elle ne peut pas être attestée.");
                    if (d.Conforme != true) return Conflit($"La dalle {d.Reference} n'est pas déclarée conforme : seule une dalle conforme figure sur une attestation.");
                    c.Lignes.Add(new LigneCourrier { DalleId = d.Id, Reference = d.Reference, Designation = string.IsNullOrWhiteSpace(l.Designation) ? d.Emplacement : l.Designation.Trim(), Surface = d.Surface });
                    continue;
                }
                if (string.IsNullOrWhiteSpace(l.NumeroPlan)) return Conflit("Le numéro de plan est obligatoire sur chaque ligne.");
                if (l.Nombre <= 0) return Conflit("Le nombre d'exemplaires doit être supérieur à zéro.");
                c.Lignes.Add(new LigneCourrier
                {
                    DalleId = d?.Id, Reference = d?.Reference, NumeroPlan = l.NumeroPlan.Trim(), Nombre = l.Nombre, Surface = d?.Surface,
                    Designation = string.IsNullOrWhiteSpace(l.Designation) ? d?.Emplacement ?? "" : l.Designation.Trim(),
                });
            }
            c.Numero = await NumerotationCourriers.SuivantAsync(db, c);
            db.Courriers.Add(c);
            await db.SaveChangesAsync();
            return Results.Ok(Dto(c));
        });

        g.MapPost("/courriers/{id:guid}/signer", async (Guid id, SignatureInput input, FacturationDbContext db, IUtilisateursLookup users, ClaimsPrincipal user) =>
        {
            if (!RolesCourriers.Any(user.IsInRole)) return Results.Forbid();
            var c = await db.Courriers.FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return Results.NotFound();
            if (c.Annule) return Conflit("Ce courrier est annulé.");
            if (c.Type == TypesCourrier.Attestation && !RolesAttestation.Any(user.IsInRole)) return Results.Forbid();
            if (c.Jeton is not null) return Conflit("Ce courrier est déjà signé.");
            if (string.IsNullOrEmpty(input.MotDePasse) || !await users.VerifierMotDePasseAsync(user.UserId(), input.MotDePasse))
                return Conflit("Mot de passe incorrect : il signe électroniquement le document.");
            c.SignePar = user.UserName(); c.SigneLe = DateTime.UtcNow; c.Jeton = PdfEndpoints.NouveauJeton();
            await db.SaveChangesAsync();
            return Results.Ok(Dto(c));
        });

        g.MapPost("/courriers/{id:guid}/annuler", async (Guid id, AnnulationInput input, FacturationDbContext db, ClaimsPrincipal user) =>
        {
            if (!EncaissementsEndpoints.RolesAnnulation.Any(user.IsInRole)) return Results.Forbid();
            var c = await db.Courriers.FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return Results.NotFound();
            if (c.Annule) return Conflit("Ce courrier est déjà annulé.");
            if (string.IsNullOrWhiteSpace(input.Motif)) return Conflit("Le motif de l'annulation est obligatoire.");
            c.Annule = true; c.MotifAnnulation = input.Motif.Trim(); c.AnnulePar = user.UserName(); c.AnnuleLe = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(Dto(c));
        });

        g.MapGet("/courriers/{id:guid}/pdf", async (Guid id, FacturationDbContext db, IConfiguration config, ClaimsPrincipal user) =>
        {
            if (!RolesCourriers.Any(user.IsInRole)) return Results.Forbid();
            var c = await db.Courriers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return Results.NotFound();
            if (c.Jeton is null) return Conflit("Ce document n'est pas encore signé : signez-le avant de l'imprimer ou de l'envoyer.");
            var e = await db.Entites.AsNoTracking().FirstOrDefaultAsync(x => x.Code == c.Entite);
            if (e is null) return Conflit("L'entité émettrice n'est pas paramétrée.");
            return Results.File(CourrierPdf.Generer(c, e, PdfEndpoints.Lien(config, c.Jeton)), "application/pdf", $"{(c.Type == TypesCourrier.BordereauBet ? "bordereau-depot-" : c.Type == TypesCourrier.BordereauBct ? "bordereau-envoi-" : "")}{c.Numero.Replace('/', '-')}.pdf");
        });
    }

    private static async Task<(string? Bct, string? Bet, string? Client, Guid? BctId, Guid? BetId)> Acteurs(ProjetResume p, IProjetsLookup projets, IPartenairesLookup partenaires)
    {
        var intervenants = await projets.IntervenantsAsync(p.Id);
        var noms = await partenaires.NomsAsync(intervenants.Select(i => i.PartenaireId).Append(p.ClientId));
        (string?, Guid?) Qui(string role)
        {
            var i = intervenants.FirstOrDefault(x => x.Role == role && !string.IsNullOrWhiteSpace(noms.GetValueOrDefault(x.PartenaireId)));
            return i is null ? (null, null) : (noms[i.PartenaireId], i.PartenaireId);
        }
        var (bct, bctId) = Qui("bct"); var (bet, betId) = Qui("bet");
        return (bct, bet, noms.GetValueOrDefault(p.ClientId), bctId, betId);
    }

    private static IResult Conflit(string message) => Results.Conflict(new { message });

    private static object Dto(Courrier c) => new
    {
        id = c.Id, numero = c.Numero, ancienNumero = c.AncienNumero, type = c.Type, typeLabel = TypesCourrier.Labels.GetValueOrDefault(c.Type, c.Type), dateCourrier = c.DateCourrier,
        objet = c.Objet, modification = c.Modification, nbLignes = c.Lignes.Count, emisPar = c.EmisPar, signePar = c.SignePar, signeLe = c.SigneLe,
        annule = c.Annule, motifAnnulation = c.MotifAnnulation, annulePar = c.AnnulePar,
    };
}
