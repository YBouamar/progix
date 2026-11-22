using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Progix.Modules.Negoce.Application;
using Progix.Modules.Negoce.Domain;
using Progix.Modules.Negoce.Infrastructure;
using Progix.SharedKernel;
using static Progix.Modules.Negoce.Application.Reponses;

namespace Progix.Modules.Negoce.Endpoints;

/// <summary>
/// Indicateurs des ventes directes, des achats et du stock. Chaque bloc n'est renvoyé que si la personne a le droit de lecture
/// sur le module concerné ; les coûts (marge, valeur du stock) exigent en plus le droit sur les prix d'achat.
/// </summary>
public static class TableauDeBordEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/negoce/tableau-de-bord", async (DateOnly? debut, DateOnly? fin, string? entite, NegoceDbContext db, GestionStock stock, DroitsNegoce droits, ClaimsPrincipal user) =>
        {
            var jour = DateOnly.FromDateTime(DateTime.UtcNow);
            var du = debut ?? new DateOnly(jour.Year, jour.Month, 1);
            var au = fin ?? du.AddMonths(1).AddDays(-1);
            if (au < du) return Conflit("La fin de la période ne peut pas précéder son début.");
            var (pDu, pAu) = Indicateurs.Precedente(du, au);
            bool Peut(int niveau) => niveau >= Habilitations.Lecture;
            var voitVentes = Peut(await droits.NiveauAsync(user, Habilitations.NegoceFactures));
            var voitDevis = Peut(await droits.NiveauAsync(user, Habilitations.NegoceDevis));
            var voitAchats = Peut(await droits.NiveauAsync(user, Habilitations.NegoceAchats));
            var voitPaiementsFournisseurs = Peut(await droits.NiveauAsync(user, Habilitations.NegocePaiementsFournisseurs));
            var voitStock = Peut(await droits.NiveauAsync(user, Habilitations.NegoceStock));
            var voitCouts = await droits.VoitPrixAchatAsync(user);

            object? ventes = null, pipeline = null, achats = null, stockBloc = null;

            if (voitVentes || voitDevis)
            {
                var documents = await db.Documents.AsNoTracking().Include(d => d.Lignes).Include(d => d.Paiements)
                    .Where(d => string.IsNullOrEmpty(entite) || d.Entite == entite).ToListAsync();
                var factures = documents.Where(d => d.Type == TypesVente.Facture).ToList();
                var avoirs = documents.Where(d => d.Type == TypesVente.Avoir).ToLookup(d => d.ParentId);
                var enfants = documents.Where(d => d.Type is TypesVente.Livraison or TypesVente.Facture).ToLookup(d => d.ParentId);
                var commandes = documents.Where(d => d.Type == TypesVente.Commande).ToList();
                var devis = documents.Where(d => d.Type == TypesVente.Devis).ToList();

                if (voitVentes)
                {
                    object Periode(DateOnly a, DateOnly b) => new
                    {
                        debut = a, fin = b, chiffreAffaires = Indicateurs.ChiffreAffaires(factures, avoirs, a, b), encaisse = Indicateurs.Encaisse(factures, a, b),
                        margeBrute = voitCouts ? Indicateurs.MargeBrute(documents.Where(d => d.Type == TypesVente.Livraison), a, b) : (decimal?)null,
                    };
                    var impayees = factures.Where(f => !f.Annule).Select(f => (f, reste: VentesSupport.ResteAPayer(f, avoirs[f.Id]))).Where(t => t.reste > 0.005m).ToList();
                    var enRetard = impayees.Where(t => t.f.Echeance is { } e && e < jour).ToList();
                    var duPeriode = factures.Where(f => !f.Annule && Indicateurs.Dans(f.DateDocument, du, au)).ToList();
                    ventes = new
                    {
                        periode = Periode(du, au), precedente = Periode(pDu, pAu),
                        creances = new { nb = impayees.Count, total = impayees.Sum(t => t.reste) },
                        retards = new { nb = enRetard.Count, total = enRetard.Sum(t => t.reste) },
                        nbFactures = duPeriode.Count,
                        topClients = duPeriode.GroupBy(f => f.ClientNom).Select(g => new { client = g.Key, montant = g.Sum(f => Indicateurs.Du(f, avoirs[f.Id])) })
                            .OrderByDescending(x => x.montant).Take(5).ToList(),
                        topArticles = duPeriode.SelectMany(f => f.Lignes).GroupBy(l => l.Designation)
                            .Select(g => new { article = g.Key, montantHt = g.Sum(l => Montants.LigneHT(l.Quantite, l.PrixUnitaire)) }).OrderByDescending(x => x.montantHt).Take(5).ToList(),
                    };
                }
                if (voitDevis)
                {
                    var ouverts = devis.Where(d => d.Etat == EtatsVente.Ouvert).ToList();
                    var (nbAFacturer, htAFacturer) = Indicateurs.AFacturer(commandes, enfants);
                    pipeline = new
                    {
                        devisOuverts = new { nb = ouverts.Count, total = ouverts.Sum(d => d.TotalTTC), expires = ouverts.Count(d => d.Echeance is { } e && e < jour) },
                        devisPeriode = devis.Count(d => !d.Annule && Indicateurs.Dans(d.DateDocument, du, au)),
                        tauxConversion = Indicateurs.TauxConversion(devis, du, au),
                        commandesOuvertes = commandes.Count(c => c.Etat == EtatsVente.Ouvert),
                        aLivrer = Indicateurs.ALivrer(commandes, enfants),
                        aFacturer = new { nb = nbAFacturer, montantHt = htAFacturer },
                    };
                }
            }

            if (voitAchats)
            {
                var commandesAchat = await db.CommandesAchat.AsNoTracking().Include(c => c.Lignes).Include(c => c.Paiements)
                    .Where(c => !c.Annule && (string.IsNullOrEmpty(entite) || c.Entite == entite)).ToListAsync();
                var enCours = commandesAchat.Where(c => c.Etat is EtatsAchat.EnAttente or EtatsAchat.Partielle).ToList();
                decimal Commande(DateOnly a, DateOnly b) => commandesAchat.Where(c => Indicateurs.Dans(c.DateCommande, a, b)).Sum(c => c.TotalTTC);
                var dues = commandesAchat.Where(c => c.ResteAPayer > 0.005m).ToList();
                achats = new
                {
                    commande = Commande(du, au), commandePrecedente = Commande(pDu, pAu),
                    enCours = new { nb = enCours.Count, total = enCours.Sum(c => c.TotalTTC), partielles = enCours.Count(c => c.Etat == EtatsAchat.Partielle) },
                    resteAPayer = voitPaiementsFournisseurs ? new { nb = dues.Count, total = dues.Sum(c => c.ResteAPayer) } : null,
                    topFournisseurs = commandesAchat.Where(c => Indicateurs.Dans(c.DateCommande, du, au)).GroupBy(c => c.FournisseurNom)
                        .Select(g => new { fournisseur = g.Key, montant = g.Sum(c => c.TotalTTC) }).OrderByDescending(x => x.montant).Take(5).ToList(),
                };
            }

            if (voitStock)
            {
                var articles = await db.Articles.AsNoTracking().Include(a => a.Unite).Where(a => a.Actif).ToListAsync();
                var stocks = await stock.StocksAsync();
                var lignes = articles.Select(a => (a, s: stocks.GetValueOrDefault(a.Id))).ToList();
                var bas = lignes.Where(t => Indicateurs.SousSeuil(t.s, t.a.SeuilAlerte)).OrderBy(t => t.a.SeuilAlerte > 0 ? t.s / t.a.SeuilAlerte : 0).ToList();
                var mouvements = await db.Mouvements.AsNoTracking().Where(m => m.Date >= du && m.Date <= au).GroupBy(m => m.Type).Select(g => new { type = g.Key, nb = g.Count() }).ToListAsync();
                stockBloc = new
                {
                    articles = articles.Count,
                    sousSeuil = bas.Count, ruptures = bas.Count(t => t.s <= 0),
                    valeur = voitCouts ? lignes.Where(t => t.s > 0).Sum(t => Math.Round(t.s * t.a.CoutMoyen, 2)) : (decimal?)null,
                    mouvements = new { total = mouvements.Sum(m => m.nb), entrees = mouvements.Where(m => m.type is TypesMouvement.Entree or TypesMouvement.Production).Sum(m => m.nb), sorties = mouvements.Where(m => m.type == TypesMouvement.Sortie).Sum(m => m.nb) },
                    aSurveiller = bas.Take(8).Select(t => new { id = t.a.Id, code = t.a.Code, designation = t.a.Designation, unite = t.a.Unite?.Symbole, stock = t.s, seuil = t.a.SeuilAlerte }).ToList(),
                };
            }

            return Results.Ok(new { debut = du, fin = au, ventes, pipeline, achats, stock = stockBloc });
        }).WithTags("Négoce - tableau de bord").RequireAuthorization();
    }
}
