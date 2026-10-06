using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.Modules.Projets.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Endpoints;

public record EtatCommandeInput(string Etat);
public record ArticleInput(string Entite, string Code, decimal StockInitial, decimal Seuil);
public record MouvementInput(string Entite, string Code, string Sens, decimal Quantite, string? Motif);

/// <summary>Logistique : suivi des commandes usine et état de stock par entité.</summary>
public static class LogistiqueEndpoints
{
    private static readonly (string Code, string Label)[] Etats =
    [
        ("attente", "En attente de validation"), ("validee", "Validée"), ("fabrication", "En fabrication"), ("livree", "Livrée"), ("recue", "Reçue"),
    ];

    private static string Etat(CommandeUsine c, EtapeDalle e) =>
        c.RecueLe != null ? "recue" : c.LivreeLe != null ? "livree" : c.FabricationLe != null ? "fabrication" : e.Validee ? "validee" : "attente";

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/logistique").WithTags("Logistique").RequireAuthorization(Policies.Logistique);

        // Une commande apparaît ici dès qu'elle est soumise à validation.
        g.MapGet("/commandes", async (ProjetsDbContext db, string? etat) =>
        {
            var rows = await (
                from c in db.CommandesUsine.AsNoTracking()
                join e in db.Etapes.AsNoTracking() on new { c.DalleId, Numero = Etapes.CommandeUsine } equals new { e.DalleId, e.Numero }
                join d in db.Dalles.AsNoTracking() on c.DalleId equals d.Id
                join p in db.Projets.AsNoTracking() on d.ProjetId equals p.Id
                where e.FaitLe != null && !p.Supprime
                orderby e.FaitLe descending
                select new { c, e, d, p }).ToListAsync();
            var liste = rows.Select(r => new
            {
                dalleId = r.d.Id, projetId = r.p.Id, reference = $"{r.p.Code}-{r.d.Suffixe}", projet = r.p.Libelle, entite = r.p.Entite, demandee = r.e.FaitLe,
                contenu = string.Join(" · ", r.c.Lignes.GroupBy(l => l.Torons).OrderBy(x => x.Key)
                    .Select(x => $"S{x.Key} · {x.Sum(l => l.Nombre)} câbles · {x.Sum(l => l.Gaine):0.##} m")),
                validations = r.e.Validee ? "Deux validations"
                    : r.e.ValidationTechniqueLe == null && r.e.ValidationAdministrativeLe == null ? "Aucune validation"
                    : $"Technique : {(r.e.ValidationTechniqueLe != null ? "oui" : "non")} · administrative : {(r.e.ValidationAdministrativeLe != null ? "oui" : "non")}",
                etat = Etat(r.c, r.e),
            }).ToList();
            return Results.Ok(new
            {
                compteurs = Etats.Select(x => new { etat = x.Code, label = x.Label, nb = liste.Count(l => l.etat == x.Code) }),
                commandes = liste.Where(l => string.IsNullOrEmpty(etat) || l.etat == etat)
                    .Select(l => new { l.dalleId, l.projetId, l.reference, l.projet, l.entite, l.demandee, l.contenu, l.validations, l.etat, etatLabel = Etats.First(x => x.Code == l.etat).Label }),
            });
        });

        // État de la commande après validation : validée, en fabrication, livrée, reçue.
        g.MapPost("/commandes/{dalleId:guid}/etat", async (Guid dalleId, EtatCommandeInput input, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            var c = await db.CommandesUsine.FirstOrDefaultAsync(x => x.DalleId == dalleId);
            var e = await db.Etapes.AsNoTracking().FirstOrDefaultAsync(x => x.DalleId == dalleId && x.Numero == Etapes.CommandeUsine);
            if (c is null || e is null) return Results.NotFound();
            if (Etat(c, e) == "attente") return ProjetsEndpoints.Conflit("La commande attend encore ses deux validations.");
            // L'état se règle librement, en avant comme en arrière ; le stock suit, puisqu'il se calcule sur les commandes livrées.
            var rang = input.Etat switch { "validee" => 0, "fabrication" => 1, "livree" => 2, "recue" => 3, _ => -1 };
            if (rang < 0) return ProjetsEndpoints.Conflit("État de commande inconnu.");
            var maintenant = DateTime.UtcNow;
            c.FabricationLe = rang >= 1 ? c.FabricationLe ?? maintenant : null;
            c.LivreeLe = rang >= 2 ? c.LivreeLe ?? maintenant : null;
            c.RecueLe = rang >= 3 ? c.RecueLe ?? maintenant : null;
            c.SuiviPar = user.UserName();
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Synthèse annuelle : matière livrée par mois et par entité. Le tonnage de torons se calcule à 1,18 kg par mètre (toron T15,7).
        g.MapGet("/synthese", async (int? annee, ProjetsDbContext db) =>
        {
            var an = annee ?? DateTime.UtcNow.Year;
            var debut = new DateTime(an, 1, 1, 0, 0, 0, DateTimeKind.Utc); var fin = debut.AddYears(1);
            var rows = await (
                from c in db.CommandesUsine.AsNoTracking()
                join d in db.Dalles.AsNoTracking() on c.DalleId equals d.Id
                join p in db.Projets.AsNoTracking() on d.ProjetId equals p.Id
                where c.LivreeLe >= debut && c.LivreeLe < fin
                select new { c, p.Entite, d.Surface }).ToListAsync();
            const decimal KgParMetre = 1.18m;
            object Ligne(string libelle, IEnumerable<dynamic> x)
            {
                var l = x.ToList();
                var metres = l.Sum(r => (decimal)((CommandeUsine)r.c).Lignes.Sum(v => v.MetresTorons));
                return new
                {
                    libelle, commandes = l.Count, surface = l.Sum(r => (decimal)r.Surface), ancrages = l.Sum(r => ((CommandeUsine)r.c).Lignes.Sum(v => v.Ancrages)),
                    gaine = l.Sum(r => (decimal)((CommandeUsine)r.c).Lignes.Sum(v => v.Gaine)), torons = metres, tonnes = Math.Round(metres * KgParMetre / 1000m, 3),
                };
            }
            var noms = System.Globalization.CultureInfo.GetCultureInfo("fr-FR").DateTimeFormat.MonthNames;
            return Results.Ok(new
            {
                annee = an, kgParMetre = KgParMetre,
                total = Ligne("Année", rows),
                entites = Entites.Labels.Select(e => Ligne(e.Key, rows.Where(r => r.Entite == e.Key))),
                mois = Enumerable.Range(1, 12).Select(m => Ligne(noms[m - 1], rows.Where(r => r.c.LivreeLe!.Value.Month == m))),
            });
        });

        g.MapGet("/stock", async (string entite, ProjetsDbContext db) =>
        {
            if (!Entites.Labels.ContainsKey(entite)) return Results.NotFound();
            return Results.Ok(await Stock(db, entite));
        });

        g.MapPut("/stock/articles", async (ArticleInput input, ProjetsDbContext db) =>
        {
            if (!Entites.Labels.ContainsKey(input.Entite) || !CodeValide(input.Code)) return Results.NotFound();
            if (input.StockInitial < 0 || input.Seuil < 0) return ProjetsEndpoints.Conflit("Le stock initial et le seuil ne peuvent pas être négatifs.");
            var a = await db.ArticlesStock.FirstOrDefaultAsync(x => x.Entite == input.Entite && x.Code == input.Code);
            if (a is null) db.ArticlesStock.Add(a = new ArticleStock { Entite = input.Entite, Code = input.Code });
            a.StockInitial = input.StockInitial;
            a.Seuil = input.Seuil;
            a.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(await Stock(db, input.Entite));
        });

        g.MapPost("/stock/mouvements", async (MouvementInput input, ProjetsDbContext db, ClaimsPrincipal user) =>
        {
            if (!Entites.Labels.ContainsKey(input.Entite) || !CodeValide(input.Code)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(input.Motif)) return ProjetsEndpoints.Conflit("Le motif est obligatoire.");
            if (input.Quantite == 0) return ProjetsEndpoints.Conflit("La quantité ne peut pas être nulle.");
            decimal quantite;
            switch (input.Sens)
            {
                case "entree": if (input.Quantite < 0) return ProjetsEndpoints.Conflit("Une entrée se saisit avec une quantité positive."); quantite = input.Quantite; break;
                case "sortie": if (input.Quantite < 0) return ProjetsEndpoints.Conflit("Une sortie se saisit avec une quantité positive."); quantite = -input.Quantite; break;
                case "correction": quantite = input.Quantite; break; // écart d'inventaire, positif ou négatif
                default: return ProjetsEndpoints.Conflit("Sens de mouvement inconnu.");
            }
            db.MouvementsStock.Add(new MouvementStock
            {
                Entite = input.Entite, Code = input.Code, Sens = input.Sens, Quantite = quantite, Motif = input.Motif.Trim(), Par = user.UserName(),
            });
            await db.SaveChangesAsync();
            return Results.Ok(await Stock(db, input.Entite));
        });
    }

    private static bool CodeValide(string code)
    {
        var p = code.Split(':');
        if (p.Length == 2 && p[0] == "accessoire") return Guid.TryParseExact(p[1], "N", out _);
        return code == "toron" || (p.Length == 2 && p[0] is "ancrage" or "gaine" && int.TryParse(p[1], out var t) && t is >= 1 and <= 99);
    }

    /// <summary>Restant = stock initial + mouvements manuels − consommé. Le consommé vient des commandes livrées de l'entité.</summary>
    private static async Task<object> Stock(ProjetsDbContext db, string entite)
    {
        var livrees = await (
            from c in db.CommandesUsine.AsNoTracking()
            join d in db.Dalles.AsNoTracking() on c.DalleId equals d.Id
            join p in db.Projets.AsNoTracking() on d.ProjetId equals p.Id
            where c.LivreeLe != null && p.Entite == entite
            select c).ToListAsync();
        var conso = new Dictionary<string, decimal>();
        void Ajouter(string code, decimal q) => conso[code] = conso.GetValueOrDefault(code) + q;
        foreach (var l in livrees.SelectMany(c => c.Lignes))
        {
            Ajouter($"ancrage:{l.Torons}", l.Ancrages);
            Ajouter($"gaine:{l.Torons}", l.Gaine);
            Ajouter("toron", l.MetresTorons);
        }
        // Les accessoires sont suivis en stock comme les autres articles, dans l'unité du Paramétrage.
        var accessoires = await db.Accessoires.AsNoTracking().OrderBy(a => a.Ordre).ToListAsync();
        foreach (var a in livrees.SelectMany(c => c.Accessoires))
            if ((a.AccessoireId ?? accessoires.FirstOrDefault(x => x.Libelle == a.Libelle)?.Id) is Guid aid) Ajouter($"accessoire:{aid:N}", a.Quantite);
        string Libelle(string code)
        {
            if (!code.StartsWith("accessoire:")) return ArticleStock.Libelle(code);
            var acc = accessoires.FirstOrDefault(x => code == $"accessoire:{x.Id:N}");
            return acc is null ? "Accessoire supprimé" : string.IsNullOrWhiteSpace(acc.Unite) ? acc.Libelle : $"{acc.Libelle}, {acc.Unite}";
        }
        int Rang(string code) => code == "toron" ? 2 : code.StartsWith("ancrage") ? 0 : code.StartsWith("gaine") ? 1 : 3;
        var articles = await db.ArticlesStock.AsNoTracking().Where(a => a.Entite == entite).ToListAsync();
        var mouvements = await db.MouvementsStock.AsNoTracking().Where(m => m.Entite == entite).OrderByDescending(m => m.CreatedAt).ToListAsync();
        var codes = articles.Select(a => a.Code).Concat(conso.Keys).Concat(mouvements.Select(m => m.Code)).Append("toron")
            .Concat(accessoires.Where(a => a.Actif).Select(a => $"accessoire:{a.Id:N}")).Distinct()
            .OrderBy(Rang).ThenBy(c => Rang(c) < 2 ? int.Parse(c.Split(':')[1]) : 0)
            .ThenBy(c => accessoires.FindIndex(a => c == $"accessoire:{a.Id:N}"));
        return new
        {
            entite,
            articles = codes.Select(code =>
            {
                var a = articles.FirstOrDefault(x => x.Code == code);
                var mvt = mouvements.Where(m => m.Code == code).Sum(m => m.Quantite);
                var consomme = conso.GetValueOrDefault(code);
                var restant = (a?.StockInitial ?? 0) + mvt - consomme;
                return new
                {
                    code, article = Libelle(code), stockInitial = a?.StockInitial ?? 0, mouvements = mvt, consomme, restant,
                    seuil = a?.Seuil ?? 0, bas = restant < (a?.Seuil ?? 0),
                };
            }),
            mouvements = mouvements.Take(30).Select(m => new { date = m.CreatedAt, article = Libelle(m.Code), sens = m.Sens, quantite = m.Quantite, motif = m.Motif, par = m.Par }),
        };
    }
}
