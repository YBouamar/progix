using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Facturation.Domain;
using Progix.Modules.Facturation.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Endpoints;

public record DepenseDalleInput(Guid DalleId, string? Categorie, DateOnly DateDepense, decimal Montant, string Mode, string? Reference, string? Libelle);
public record CategorieInput(Guid? Id, string? Libelle, bool Actif);
public record CategoriesInput(List<CategorieInput>? Categories);

public static class DepensesDalleEndpoints
{
    /// <summary>Saisie d'une dépense de dalle : bureau et chantier.</summary>
    public static readonly string[] RolesSaisie =
        [Roles.Administrateur, Roles.ResponsableAdministratif, Roles.ChefChantier, Roles.ResponsableChantier, Roles.ChefProjet, Roles.Directeur];

    public static void Map(IEndpointRouteBuilder app)
    {
        var param = app.MapGroup("/api/parametres/categories-depense").WithTags("Paramétrage").RequireAuthorization();
        param.MapGet("/", async (FacturationDbContext db, ClaimsPrincipal user) => Results.Ok(await Parametres(db, user)));
        param.MapPut("/", async (CategoriesInput input, FacturationDbContext db, ClaimsPrincipal user) =>
        {
            if (!Policies.RolesCommercial.Any(user.IsInRole)) return Results.Forbid();
            var liste = (input.Categories ?? []).Where(c => !string.IsNullOrWhiteSpace(c.Libelle)).ToList();
            if (liste.GroupBy(c => c.Libelle!.Trim().ToUpperInvariant()).Any(x => x.Count() > 1)) return Conflit("Une catégorie figure deux fois dans la liste.");
            var existantes = await db.CategoriesDepense.ToListAsync();
            // Une catégorie retirée est supprimée ; les dépenses déjà saisies gardent son libellé.
            db.CategoriesDepense.RemoveRange(existantes.Where(e => liste.All(c => c.Id != e.Id)));
            for (var i = 0; i < liste.Count; i++)
            {
                var c = existantes.FirstOrDefault(e => e.Id == liste[i].Id);
                if (c is null) db.CategoriesDepense.Add(c = new CategorieDepense());
                c.Libelle = liste[i].Libelle!.Trim(); c.Actif = liste[i].Actif; c.Ordre = i;
            }
            await db.SaveChangesAsync();
            return Results.Ok(await Parametres(db, user));
        });

        var g = app.MapGroup("/api/projets/{id:guid}/depenses").WithTags("Dépenses de dalle").RequireAuthorization();

        g.MapGet("/", async (Guid id, FacturationDbContext db, IProjetsLookup projets, ClaimsPrincipal user) =>
        {
            var commercial = Policies.RolesCommercial.Any(user.IsInRole);
            var saisie = await PeutSaisir(user, id, projets);
            if (!commercial && !saisie) return Results.Forbid();
            var projet = await projets.ProjetAsync(id, true);
            if (projet is null) return Results.NotFound();
            var depenses = await db.Depenses.AsNoTracking().Where(x => x.ProjetId == id).OrderByDescending(x => x.DateDepense).ThenByDescending(x => x.CreatedAt).ToListAsync();
            var total = depenses.Where(x => !x.Annule).Sum(x => x.Montant);
            object? synthese = null;
            if (commercial)
            {
                // Le facturé et l'encaissé ne sont montrés qu'aux rôles commerciaux.
                var factures = await db.Factures.AsNoTracking().Include(f => f.Avoirs).Where(f => f.ProjetId == id).ToListAsync();
                var facture = factures.Sum(f => f.Du);
                var jour = DateOnly.FromDateTime(DateTime.UtcNow);
                var encaisse = (await db.Encaissements.AsNoTracking().Where(e => e.ProjetId == id && !e.Annule).ToListAsync())
                    .Where(e => !e.EnAttente(jour)).Sum(e => e.Remboursement ? -e.Montant : e.Montant);
                synthese = new { facture, encaisse, depenses = total, ecart = facture - total };
            }
            return Results.Ok(new
            {
                peutSaisir = saisie && !projet.Supprime, peutAnnuler = EncaissementsEndpoints.RolesAnnulation.Any(user.IsInRole), total, synthese,
                categories = await db.CategoriesDepense.AsNoTracking().Where(c => c.Actif).OrderBy(c => c.Ordre).Select(c => c.Libelle).ToListAsync(),
                dalles = projet.Dalles.Select(d => new
                {
                    id = d.Id, reference = d.Reference, emplacement = d.Emplacement,
                    total = depenses.Where(x => x.DalleId == d.Id && !x.Annule).Sum(x => x.Montant),
                    depenses = depenses.Where(x => x.DalleId == d.Id).Select(x => new
                    {
                        id = x.Id, date = x.DateDepense, categorie = x.Categorie, libelle = x.Libelle, montant = x.Montant, mode = x.Mode,
                        modeLabel = ModesPaiement.Libelles.GetValueOrDefault(x.Mode, x.Mode), reference = x.Reference, saisiPar = x.SaisiPar,
                        annule = x.Annule, motifAnnulation = x.MotifAnnulation, annulePar = x.AnnulePar,
                    }),
                }),
            });
        });

        g.MapPost("/", async (Guid id, DepenseDalleInput input, FacturationDbContext db, IProjetsLookup projets, ClaimsPrincipal user) =>
        {
            if (!await PeutSaisir(user, id, projets)) return Results.Forbid();
            var projet = await projets.ProjetAsync(id, true);
            if (projet is null) return Results.NotFound();
            if (projet.Supprime) return Conflit("Un projet supprimé est en lecture seule.");
            var dalle = projet.Dalles.FirstOrDefault(d => d.Id == input.DalleId);
            if (dalle is null) return Conflit("Choisissez la dalle concernée : une dépense se rattache à une dalle précise.");
            var categorie = input.Categorie?.Trim();
            if (string.IsNullOrEmpty(categorie) || !await db.CategoriesDepense.AnyAsync(c => c.Actif && c.Libelle == categorie)) return Conflit("Choisissez une catégorie de dépense.");
            if (input.Montant <= 0) return Conflit("Le montant doit être supérieur à zéro.");
            if (!ModesPaiement.Libelles.ContainsKey(input.Mode)) return Conflit("Mode de paiement inconnu.");
            if (input.DateDepense > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))) return Conflit("La date de la dépense ne peut pas être dans le futur.");
            db.Depenses.Add(new Depense
            {
                Entite = projet.Entite, ProjetId = projet.Id, ProjetCode = projet.Code, DalleId = dalle.Id, DalleReference = dalle.Reference, Categorie = categorie,
                DateDepense = input.DateDepense, Libelle = input.Libelle?.Trim() ?? "", Montant = Math.Round(input.Montant, 2), Mode = input.Mode,
                Reference = string.IsNullOrWhiteSpace(input.Reference) ? null : input.Reference.Trim(), SaisiPar = user.UserName(),
            });
            await db.SaveChangesAsync();
            return Results.Ok(new { ok = true });
        });
    }

    /// <summary>Un chef de chantier ne saisit que sur les projets auxquels il est affecté.</summary>
    private static async Task<bool> PeutSaisir(ClaimsPrincipal user, Guid projetId, IProjetsLookup projets) =>
        RolesSaisie.Any(user.IsInRole) && (!user.IsInRole(Roles.ChefChantier) || await projets.EstChefChantierAsync(projetId, user.UserId()));

    private static async Task<object> Parametres(FacturationDbContext db, ClaimsPrincipal user) => new
    {
        modifiable = Policies.RolesCommercial.Any(user.IsInRole),
        categories = await db.CategoriesDepense.AsNoTracking().OrderBy(c => c.Ordre).Select(c => new { id = c.Id, libelle = c.Libelle, actif = c.Actif }).ToListAsync(),
    };

    private static IResult Conflit(string message) => Results.Conflict(new { message });
}
