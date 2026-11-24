using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Facturation.Domain;
using Progix.Modules.Facturation.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Endpoints;

public record FactureInput(Guid ProjetId, string Type, List<Guid>? BonIds, Guid? DevisId, decimal? AcomptePct, DateOnly DateFacture, DateOnly Echeance, string? ModeReglement = null, decimal? AcompteTTC = null);

public static class FacturesEndpoints
{
    public const decimal TauxTva = 20m;

    /// <summary>Délai légal de paiement : l'échéance tombe moins de 90 jours après la date de facture.</summary>
    public const int DelaiMaxJours = 90;

    private static DateOnly Aujourdhui => DateOnly.FromDateTime(DateTime.UtcNow);
    internal static string Statut(Facture f) =>
        f.Annulee ? "annulee" : f.Reste <= 0 ? "payee" : f.Echeance < Aujourdhui ? "retard" : f.Regle > 0 ? "partielle" : "impayee";
    private static readonly Dictionary<string, string> Statuts = new()
    {
        ["impayee"] = "Impayée", ["partielle"] = "Partielle", ["retard"] = "En retard", ["payee"] = "Payée", ["annulee"] = "Annulée",
    };

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/factures").WithTags("Facturation").RequireAuthorization(Policies.Commercial);

        g.MapGet("/", async (FacturationDbContext db, string? statut, Guid? projetId, int? annee, string? q, int page = 1, int pageSize = 20) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var query = db.Factures.AsNoTracking().Include(f => f.Avoirs).AsQueryable();
            if (projetId is Guid pid) query = query.Where(f => f.ProjetId == pid);
            if (annee is int a) query = query.Where(f => f.DateFacture >= new DateOnly(a, 1, 1) && f.DateFacture <= new DateOnly(a, 12, 31));
            if (!string.IsNullOrWhiteSpace(q))
            {
                var like = $"%{q.Trim()}%";
                query = query.Where(f => EF.Functions.ILike(f.Numero, like) || EF.Functions.ILike(f.ProjetLibelle, like)
                    || EF.Functions.ILike(f.ProjetCode, like) || EF.Functions.ILike(f.ClientNom, like));
            }
            var toutes = await query.OrderByDescending(f => f.DateFacture).ThenByDescending(f => f.Numero).ToListAsync();
            await db.ImputerAsync(toutes);
            var filtrees = string.IsNullOrEmpty(statut) ? toutes : toutes.Where(f => Statut(f) == statut).ToList();
            return Results.Ok(new
            {
                indicateurs = new
                {
                    facture = toutes.Sum(f => f.Du), encaisse = toutes.Sum(f => f.Regle), reste = toutes.Sum(f => Math.Max(0, f.Reste)),
                    retard = toutes.Where(f => Statut(f) == "retard").Sum(f => f.Reste),
                },
                compteurs = Statuts.Select(s => new { statut = s.Key, label = s.Value, nb = toutes.Count(f => Statut(f) == s.Key) }),
                items = filtrees.Skip((page - 1) * pageSize).Take(pageSize).Select(f => Dto(f, false)),
                total = filtrees.Count, page, pageSize,
            });
        });

        g.MapGet("/{id:guid}", async (Guid id, FacturationDbContext db) =>
        {
            var f = await db.Factures.AsNoTracking().Include(x => x.Lignes).Include(x => x.Avoirs).FirstOrDefaultAsync(x => x.Id == id);
            if (f is null) return Results.NotFound();
            await db.ImputerAsync([f]);
            return Results.Ok(Dto(f, true, await PaiementsDe(db, f)));
        });

        // Situation financière du projet. Reste dû = commandé (devis validés, TTC) moins avoirs moins encaissé.
        // Le reste par facture (facturé moins avoirs moins réglé par imputation) reste affiché à côté, comme le reste à facturer.
        g.MapGet("/situation", async (Guid projetId, FacturationDbContext db, IDevisLookup devis) =>
        {
            var commande = (await devis.ValidesAsync(projetId)).Sum(d => d.TotalTTC);
            var factures = await db.Factures.AsNoTracking().Include(f => f.Avoirs).Where(f => f.ProjetId == projetId).ToListAsync();
            var encaissements = await db.Encaissements.AsNoTracking().Where(e => e.ProjetId == projetId && !e.Annule).ToListAsync();
            Imputation.Imputer(factures, encaissements, Aujourdhui);
            var facture = factures.Sum(f => f.TotalTTC);
            var avoirs = factures.Sum(f => f.Avoirs_TTC);
            var encaisse = encaissements.Where(e => !e.EnAttente(Aujourdhui)).Sum(e => e.Remboursement ? -e.Montant : e.Montant);
            return Results.Ok(new
            {
                commande, facture, avoirs, encaisse, effetsEnAttente = encaissements.Where(e => e.EnAttente(Aujourdhui)).Sum(e => e.Montant),
                resteDu = commande - avoirs - encaisse,
                resteAFacturer = Math.Max(0, commande - facture),
                resteSurFactures = factures.Where(f => !f.Annulee).Sum(f => Math.Max(0, f.Reste)),
            });
        });

        // Tout ce qu'il faut pour préparer une facture : client, devis validés, bons non facturés chiffrés, acomptes déjà émis.
        g.MapGet("/preparation", async (Guid projetId, FacturationDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires, IDevisLookup devis) =>
        {
            var ctx = await Contexte(projetId, db, projets, partenaires, devis);
            if (ctx is null) return Results.NotFound();
            return Results.Ok(new
            {
                projetId, code = ctx.Projet.Code, libelle = ctx.Projet.Libelle, entite = ctx.Projet.Entite,
                client = ctx.Client.Nom, clientIdentifiants = ctx.Client.Identifiants, exonereTva = ctx.Client.ExonereTva,
                refus = Refus(ctx), tauxTva = ctx.Client.ExonereTva ? 0 : TauxTva, delaiMaxJours = DelaiMaxJours,
                devis = ctx.Devis.Select(d => new { id = d.Id, numero = d.Numero, version = d.Version, netHT = d.NetHT, acompteDejaFacture = ctx.AcomptePct(d.Id) }),
                bons = ctx.Bons.Select(b =>
                {
                    var lignes = b.Lignes.OrderBy(l => l.Ordre).Select(l => Chiffrer(ctx, l)).ToList();
                    return new
                    {
                        id = b.Id, numero = b.Numero, dateLivraison = b.DateLivraison, surface = b.Surface,
                        facturable = lignes.All(l => l.Prix != null),
                        totalHT = lignes.Sum(l => l.Montant), acompte = lignes.Sum(l => l.Acompte),
                        lignes = lignes.Select(l => new { reference = l.Ligne.Reference, emplacement = l.Ligne.Emplacement, surface = l.Ligne.Surface, prixM2 = l.Prix, montantHT = l.Montant }),
                    };
                }),
            });
        });

        g.MapPost("/", async (FactureInput input, FacturationDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires, IDevisLookup devis, ClaimsPrincipal user) =>
        {
            var ctx = await Contexte(input.ProjetId, db, projets, partenaires, devis);
            if (ctx is null) return Results.NotFound();
            if (Refus(ctx) is { } refus) return Conflit(refus);
            if (input.Echeance < input.DateFacture) return Conflit("L'échéance ne peut pas précéder la date de facture.");
            if (input.Echeance.DayNumber - input.DateFacture.DayNumber >= DelaiMaxJours)
                return Conflit($"Délai légal : l'échéance doit tomber moins de {DelaiMaxJours} jours après la date de facture.");

            if (!string.IsNullOrEmpty(input.ModeReglement) && !ModesPaiement.Libelles.ContainsKey(input.ModeReglement)) return Conflit("Mode de règlement inconnu.");
            var f = new Facture
            {
                ModeReglement = string.IsNullOrEmpty(input.ModeReglement) ? ModesPaiement.Virement : input.ModeReglement,
                Entite = ctx.Projet.Entite, Type = input.Type, ProjetId = ctx.Projet.Id, ProjetCode = ctx.Projet.Code, ProjetLibelle = ctx.Projet.Libelle,
                ClientId = ctx.Client.Id, ClientNom = ctx.Client.Nom, ClientIdentifiants = ctx.Client.Identifiants, ClientAdresse = ctx.Client.Adresse,
                DateFacture = input.DateFacture, Echeance = input.Echeance, EmisPar = user.UserName(), TauxTva = ctx.Client.ExonereTva ? 0 : TauxTva,
            };
            var bons = new List<BonLivraison>();

            // Une facture se crée uniquement à partir de bons de livraison : plus de facture d'acompte.
            // Les acomptes déjà facturés restent déduits au prorata des factures de livraison (Chiffrer).
            if (input.Type == TypesFacture.Acompte)
                return Conflit("Une facture s'établit uniquement à partir d'un bon de livraison. Le paiement à la signature se saisit dans les paiements du projet, sur les dalles.");
            if (input.Type == TypesFacture.Livraison)
            {
                var ids = (input.BonIds ?? []).Distinct().ToList();
                if (ids.Count == 0) return Conflit("Sélectionnez au moins un bon de livraison.");
                bons = await db.BonsLivraison.Include(b => b.Lignes).Where(b => ids.Contains(b.Id) && b.ProjetId == ctx.Projet.Id).OrderBy(b => b.DateLivraison).ThenBy(b => b.Numero).ToListAsync();
                if (bons.Count != ids.Count) return Conflit("Un bon de livraison sélectionné n'appartient pas à ce projet.");
                if (bons.Any(b => b.FactureId != null)) return Conflit("Un bon de livraison sélectionné est déjà facturé.");
                var ordre = 0;
                foreach (var l in bons.SelectMany(b => b.Lignes.OrderBy(x => x.Ordre)))
                {
                    var c = Chiffrer(ctx, l);
                    if (c.Prix is null) return Conflit($"La dalle {l.Reference} ne figure dans aucun devis validé : son prix est inconnu.");
                    f.Lignes.Add(new LigneFacture { Ordre = ordre++, Libelle = $"{l.Reference} · {l.Emplacement}", DalleId = l.DalleId, Surface = l.Surface, PrixM2 = c.Prix, MontantHT = c.Montant });
                    f.AcompteDeduit += c.Acompte;
                }
                f.TotalHT = f.Lignes.Sum(x => x.MontantHT);
                f.Origine = bons.Count == 1 ? bons[0].Numero : $"{bons.Count} BL : {string.Join(", ", bons.Select(b => b.Numero))}";
            }
            else return Conflit("Type de facture inconnu.");

            f.NetHT = f.TotalHT - f.AcompteDeduit;
            f.Tva = Math.Round(f.NetHT * f.TauxTva / 100m, 2);
            f.TotalTTC = f.NetHT + f.Tva;
            f.Parts = Imputation.PartsLivraison(f);

            // Une facture à régler en espèces respecte le plafond par client et par mois civil, toutes entités confondues.
            if (f.ModeReglement == ModesPaiement.Especes)
            {
                var plafond = EncaissementsEndpoints.PlafondEspeces;
                var fr = new System.Globalization.CultureInfo("fr-FR");
                string DH(decimal v) => v.ToString("N2", fr) + " DH";
                if (f.TotalTTC > plafond)
                    return Conflit($"Une facture de {DH(f.TotalTTC)} TTC ne peut pas être réglée en espèces : le plafond est de {DH(plafond)} par client et par mois. Choisissez un autre mode de règlement.");
                var debut = new DateOnly(f.DateFacture.Year, f.DateFacture.Month, 1);
                var duMois = await db.Factures.AsNoTracking().Include(x => x.Avoirs)
                    .Where(x => x.ClientId == f.ClientId && x.ModeReglement == ModesPaiement.Especes && x.DateFacture >= debut && x.DateFacture < debut.AddMonths(1)).ToListAsync();
                var deja = duMois.Where(x => !x.Annulee).Sum(x => x.Du);
                if (deja + f.TotalTTC > plafond)
                    return Conflit($"Plafond des espèces dépassé pour ce client en {f.DateFacture.ToString("MMMM yyyy", fr)} : {DH(deja)} déjà facturés en espèces sur {DH(plafond)}. Il reste {DH(Math.Max(0, plafond - deja))} possibles en espèces.");
            }

            await using var tx = await db.Database.BeginTransactionAsync();
            var annee = f.DateFacture.Year;
            f.Numero = $"FAC-{await db.NumeroSuivantAsync($"facture:{f.Entite}:{annee}"):00000}-{annee}";
            db.Factures.Add(f);
            foreach (var b in bons) b.FactureId = f.Id;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(Dto(f, true));
        });
    }

    private static IResult Conflit(string message) => Results.Conflict(new { message });

    private sealed record Ctx(ProjetResume Projet, PartenaireLegal Client, IReadOnlyList<DevisValide> Devis, List<BonLivraison> Bons, List<Facture> Acomptes)
    {
        public decimal AcomptePct(Guid devisId) => Acomptes.Where(a => a.DevisId == devisId).Sum(a => a.AcomptePct ?? 0);
    }

    private static async Task<Ctx?> Contexte(Guid projetId, FacturationDbContext db, IProjetsLookup projets, IPartenairesLookup partenaires, IDevisLookup devis)
    {
        var projet = await projets.ProjetAsync(projetId);
        if (projet is null) return null;
        var client = await partenaires.LegalAsync(projet.ClientId);
        if (client is null) return null;
        var bons = await db.BonsLivraison.AsNoTracking().Include(b => b.Lignes).Where(b => b.ProjetId == projetId && b.FactureId == null)
            .OrderBy(b => b.DateLivraison).ThenBy(b => b.Numero).ToListAsync();
        var acomptes = await db.Factures.AsNoTracking().Where(f => f.ProjetId == projetId && f.Type == TypesFacture.Acompte && !f.Avoirs.Any(a => a.Total && !a.Annule)).ToListAsync();
        return new Ctx(projet, client, await devis.ValidesAsync(projetId), bons, acomptes);
    }

    private static string? Refus(Ctx c) =>
        c.Projet.Supprime ? "Un projet supprimé est en lecture seule."
        : c.Projet.Bloque ? "Projet bloqué : la facturation est suspendue."
        : c.Devis.Count == 0 ? "Aucun devis validé pour ce projet : la facturation reprend les prix du devis validé."
        : c.Client.Manques.Count > 0 ? $"Fiche du client incomplète pour facturer : il manque {string.Join(", ", c.Client.Manques)}."
        : null;

    /// <summary>Prix d'une dalle livrée : celui du devis validé le plus récent qui la contient. L'acompte de ce devis est déduit au prorata.</summary>
    private static (LigneLivraison Ligne, decimal? Prix, decimal Montant, decimal Acompte) Chiffrer(Ctx c, LigneLivraison l)
    {
        var d = c.Devis.FirstOrDefault(x => x.PrixNet.ContainsKey(l.DalleId));
        if (d is null) return (l, null, 0, 0);
        var prix = d.PrixNet[l.DalleId];
        var montant = Math.Round(prix * l.Surface, 2);
        return (l, prix, montant, Math.Round(montant * Math.Min(100, c.AcomptePct(d.Id)) / 100m, 2));
    }

    internal static DateOnly Jour => Aujourdhui;

    /// <summary>Paiements reçus sur les dalles d'une facture (ou, avant le suivi par dalle, sur la facture elle-même).</summary>
    internal static async Task<List<Encaissement>> PaiementsDe(FacturationDbContext db, Facture f)
    {
        var dalles = f.Parts.Select(p => p.DalleId).ToHashSet();
        var tous = await db.Encaissements.AsNoTracking().Where(e => e.ProjetId == f.ProjetId || e.FactureId == f.Id).ToListAsync();
        return tous.Where(e => e.FactureId == f.Id || e.Dalles.Any(d => dalles.Contains(d.DalleId))).ToList();
    }

    internal static object Dto(Facture f, bool detail, IEnumerable<Encaissement>? paiements = null) => new
    {
        id = f.Id, numero = f.Numero, ancienNumero = f.AncienNumero, entite = f.Entite, type = f.Type, projetId = f.ProjetId, projetCode = f.ProjetCode, projet = f.ProjetLibelle,
        client = f.ClientNom, clientIdentifiants = f.ClientIdentifiants, clientAdresse = f.ClientAdresse,
        dateFacture = f.DateFacture, echeance = f.Echeance, modeReglement = f.ModeReglement, origine = f.Origine, devisNumero = f.DevisNumero, acomptePct = f.AcomptePct,
        totalHT = f.TotalHT, acompteDeduit = f.AcompteDeduit, netHT = f.NetHT, tauxTva = f.TauxTva, tva = f.Tva, totalTTC = f.TotalTTC,
        avoirsTTC = f.Avoirs_TTC, du = f.Du, regle = f.Regle, reste = f.Reste, effetsEnAttente = f.EffetsEnAttente, aCouvrir = f.ACouvrir, statut = Statut(f), statutLabel = Statuts[Statut(f)], signePar = f.SignePar, signeLe = f.SigneLe, emisPar = f.EmisPar, emisLe = f.CreatedAt,
        lignes = detail ? f.Lignes.OrderBy(l => l.Ordre).Select(l => new { libelle = l.Libelle, surface = l.Surface, prixM2 = l.PrixM2, montantHT = l.MontantHT }) : null,
        encaissements = detail ? (paiements ?? []).OrderBy(e => e.DatePaiement).ThenBy(e => e.CreatedAt).Select(e => new
        {
            id = e.Id, datePaiement = e.DatePaiement, mode = e.Mode, modeLabel = ModesPaiement.Libelles.GetValueOrDefault(e.Mode, e.Mode), montant = e.Montant,
            reference = e.Reference, banque = e.Banque, numeroRecu = e.NumeroRecu, echeanceEffet = e.EcheanceEffet, enAttente = e.EnAttente(Aujourdhui), remboursement = e.Remboursement, saisiPar = e.SaisiPar, annule = e.Annule, motifAnnulation = e.MotifAnnulation, annulePar = e.AnnulePar,
            dalles = e.Dalles.Select(x => new { reference = x.Reference, montant = x.Montant }),
        }) : null,
        avoirs = detail ? f.Avoirs.OrderBy(a => a.DateAvoir).ThenBy(a => a.Numero).Select(a => new
        {
            id = a.Id, numero = a.Numero, ancienNumero = a.AncienNumero, dateAvoir = a.DateAvoir, motif = a.Motif, total = a.Total,
            montantHT = a.MontantHT, tva = a.Tva, totalTTC = a.TotalTTC, emisPar = a.EmisPar, signePar = a.SignePar, signeLe = a.SigneLe,
            annule = a.Annule, motifAnnulation = a.MotifAnnulation, annulePar = a.AnnulePar,
        }) : null,
        plafondEspeces = EncaissementsEndpoints.PlafondEspeces,
    };
}
