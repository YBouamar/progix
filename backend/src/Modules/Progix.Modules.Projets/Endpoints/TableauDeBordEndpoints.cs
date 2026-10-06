using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

public static class TableauDeBordEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        // Vue d'ensemble des projets : répartition par statut, projets bloqués, validations attendues, stock bas.
        app.MapGet("/api/tableau-de-bord/projets", async (ProjetsDbContext db, string? entite) =>
        {
            var projets = await db.Projets.AsNoTracking().Where(p => !p.Supprime && (string.IsNullOrEmpty(entite) || p.Entite == entite)).ToListAsync();
            var ids = projets.Select(p => p.Id).ToList();

            // Étapes à double validation dont une validation manque : la commande usine une fois saisie, la mise en tension et l'injection avant saisie.
            var etapes = new[] { Etapes.CommandeUsine, Etapes.MiseEnTension, Etapes.Injection };
            var faites = etapes.Select(e => e - 1).ToList();
            var dalles = await db.Dalles.AsNoTracking().Where(d => ids.Contains(d.ProjetId) && faites.Contains(d.EtapesFaites)).Select(d => new { d.Id, d.ProjetId, d.EtapesFaites }).ToListAsync();
            var dalleIds = dalles.Select(d => d.Id).ToList();
            var lignes = await db.Etapes.AsNoTracking().Where(e => dalleIds.Contains(e.DalleId) && etapes.Contains(e.Numero)).ToListAsync();
            var bloques = projets.Where(p => p.Bloque).Select(p => p.Id).ToHashSet();
            var validations = dalles.Count(d =>
            {
                if (bloques.Contains(d.ProjetId)) return false;
                var numero = d.EtapesFaites + 1;
                var e = lignes.FirstOrDefault(x => x.DalleId == d.Id && x.Numero == numero);
                if (e?.Validee == true) return false;
                return numero != Etapes.CommandeUsine || e?.FaitLe != null;
            });

            return Results.Ok(new
            {
                total = projets.Count,
                statuts = StatutsProjet.Labels.Select(s => new { code = s.Key, label = s.Value, nb = projets.Count(p => p.Statut == s.Key) }),
                bloques = projets.Where(p => p.Bloque).OrderByDescending(p => p.BlocageLe).Select(p => new { id = p.Id, code = p.Code, libelle = p.Libelle, motif = p.BlocageMotif, par = p.BlocagePar, le = p.BlocageLe }),
                validationsEnAttente = validations,
            });
        }).WithTags("Tableau de bord").RequireAuthorization(Policies.Commercial);

        // Tableau de bord personnel, sans aucun montant : les projets et les dalles de la personne connectée, selon son rôle.
        app.MapGet("/api/tableau-de-bord/mes-projets", async (ProjetsDbContext db, IPartenairesLookup partenaires, IDevisLookup devis, ClaimsPrincipal user) =>
        {
            var moi = user.UserId();
            var actifs = db.Projets.AsNoTracking().Where(p => !p.Supprime);
            List<Projet> projets;
            HashSet<Guid>? mesDalles = null;
            string perimetre;
            if (user.IsInRole(Roles.AgentCommercial))
            {
                var ids = await devis.ProjetsDuCommercialAsync(moi);
                projets = await actifs.Where(p => ids.Contains(p.Id)).ToListAsync();
                perimetre = "Projets dont un devis vous désigne comme commercial";
            }
            else if (user.IsInRole(Roles.Ingenieur) || user.IsInRole(Roles.ChefChantier))
            {
                var fonction = user.IsInRole(Roles.Ingenieur) ? Fonctions.Ingenieur : Fonctions.ChefChantier;
                var ids = await db.Affectations.AsNoTracking().Where(a => a.UserId == moi && a.Fonction == fonction && a.Fin == null).Select(a => a.ProjetId).ToListAsync();
                projets = await actifs.Where(p => ids.Contains(p.Id)).ToListAsync();
                perimetre = "Projets auxquels vous êtes affecté";
            }
            else if (user.IsInRole(Roles.Dessinateur))
            {
                var miennes = await db.Dalles.AsNoTracking().Where(d => d.DessinateurId == moi).Select(d => new { d.Id, d.ProjetId }).ToListAsync();
                mesDalles = miennes.Select(d => d.Id).ToHashSet();
                var ids = miennes.Select(d => d.ProjetId).Distinct().ToList();
                projets = await actifs.Where(p => ids.Contains(p.Id)).ToListAsync();
                perimetre = "Dalles dont vous êtes le dessinateur";
            }
            else
            {
                // Chefs et responsables supervisent l'ensemble des projets.
                projets = await actifs.ToListAsync();
                perimetre = "Tous les projets";
            }

            var pids = projets.Select(p => p.Id).ToList();
            var dalles = await db.Dalles.AsNoTracking().Where(d => pids.Contains(d.ProjetId)).OrderBy(d => d.Numero).ToListAsync();
            if (mesDalles is not null) dalles = dalles.Where(d => mesDalles.Contains(d.Id)).ToList();
            var enCours = dalles.Where(d => d.EtapesFaites < Etapes.Total).ToList();
            var ids2 = enCours.Select(d => d.Id).ToList();
            var etapes = await db.Etapes.AsNoTracking().Where(e => ids2.Contains(e.DalleId)).ToListAsync();
            var noms = await partenaires.NomsAsync(projets.Select(p => p.ClientId));
            var parId = projets.ToDictionary(p => p.Id);

            var lignes = enCours.Select(d =>
            {
                var p = parId[d.ProjetId];
                var def = Etapes.Get(d.EtapesFaites + 1);
                var e = etapes.FirstOrDefault(x => x.DalleId == d.Id && x.Numero == def.Numero);
                // Ce qui empêche d'avancer : projet bloqué, validations attendues avant la saisie, ou étape saisie en attente de validation.
                string? verrou = p.Bloque ? "Projet bloqué" : null;
                if (verrou is null && def.DoubleValidation && e?.Validee != true)
                {
                    var manque = e?.ValidationTechniqueLe == null && e?.ValidationAdministrativeLe == null ? "validations technique et administrative attendues"
                        : e?.ValidationTechniqueLe == null ? "validation technique attendue"
                        : e?.ValidationAdministrativeLe == null ? "validation administrative attendue" : "validation du responsable des chantiers attendue";
                    if (def.ValidationAvant) verrou = $"Verrouillée : {manque}";
                    else if (e?.FaitLe != null) verrou = $"Saisie, {manque}";
                }
                return new
                {
                    id = d.Id, projetId = p.Id, reference = $"{p.Code}-{d.Suffixe}", projet = p.Libelle,
                    emplacement = string.Join(" · ", new[] { d.Bloc, d.Immeuble, Dalle.NiveauLabel(d.Niveau) }.Where(x => !string.IsNullOrWhiteSpace(x))),
                    etape = def.Numero, etapeLabel = def.Label, faites = d.EtapesFaites, verrou,
                    aFaire = verrou is null && def.Roles.Any(user.IsInRole),
                };
            }).OrderByDescending(x => x.aFaire).ThenByDescending(x => x.faites).ToList();

            return Results.Ok(new
            {
                perimetre,
                nbProjets = projets.Count, nbDalles = dalles.Count, dallesRealisees = dalles.Count(d => d.EtapesFaites >= Etapes.Total),
                dallesEnCours = enCours.Count, enRealisation = projets.Count(p => p.Statut == StatutsProjet.EnRealisation), actions = lignes.Count(l => l.aFaire),
                projets = projets.OrderByDescending(p => p.DateCreation).Select(p => new
                {
                    id = p.Id, code = p.Code, libelle = p.Libelle, client = noms.GetValueOrDefault(p.ClientId, ""), statut = p.Statut,
                    statutLabel = StatutsProjet.Labels.GetValueOrDefault(p.Statut, p.Statut), bloque = p.Bloque,
                    dalles = dalles.Count(d => d.ProjetId == p.Id), realisees = dalles.Count(d => d.ProjetId == p.Id && d.EtapesFaites >= Etapes.Total),
                }),
                dalles = lignes.Take(60),
            });
        }).WithTags("Tableau de bord").RequireAuthorization();
    }
}
