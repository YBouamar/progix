using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Partenaires.Application;
using Progix.Modules.Partenaires.Domain;
using Progix.Modules.Partenaires.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Partenaires.Endpoints;

public static class PartenairesEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/partenaires").WithTags("Clients et partenaires").RequireAuthorization();

        g.MapGet("/", async (PartenairesDbContext db, string? type, string? q, bool? archives, int page = 1, int pageSize = 20) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var query = db.Partenaires.AsNoTracking().Where(p => p.Archive == (archives ?? false));
            if (!string.IsNullOrWhiteSpace(type)) query = query.Where(p => p.Type == type);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var like = $"%{q.Trim()}%";
                query = query.Where(p =>
                    EF.Functions.ILike(p.RaisonSociale ?? "", like) || EF.Functions.ILike(p.Nom ?? "", like) ||
                    EF.Functions.ILike(p.Prenom ?? "", like) || EF.Functions.ILike(p.Ice ?? "", like) ||
                    EF.Functions.ILike(p.Cin ?? "", like) || EF.Functions.ILike(p.Ville ?? "", like));
            }
            var total = await query.CountAsync();
            var rows = await query
                .OrderBy(p => p.RaisonSociale ?? p.Nom)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(p => new { P = p, Nb = p.Contacts.Count })
                .ToListAsync();
            var items = rows.Select(r => new PartenaireListItem(r.P.Id, r.P.Type,
                TypesPartenaire.Labels.GetValueOrDefault(r.P.Type, r.P.Type), r.P.Nature, r.P.NomAffiche,
                r.P.Nature == Natures.Physique ? r.P.Cin : r.P.Ice, r.P.Ville, r.P.Telephone, r.P.Email,
                r.P.ExonereTva, r.P.Archive, r.Nb)).ToList();
            return Results.Ok(new PagedResult<PartenaireListItem>(items, total, page, pageSize));
        });

        g.MapGet("/compteurs", async (PartenairesDbContext db) =>
        {
            var parType = await db.Partenaires.Where(p => !p.Archive)
                .GroupBy(p => p.Type).Select(x => new { Type = x.Key, Nb = x.Count() }).ToListAsync();
            return Results.Ok(TypesPartenaire.Labels.Select(t => new
            {
                type = t.Key, label = t.Value, nb = parType.FirstOrDefault(x => x.Type == t.Key)?.Nb ?? 0,
            }));
        });

        g.MapGet("/{id:guid}", async (Guid id, PartenairesDbContext db) =>
        {
            var p = await db.Partenaires.AsNoTracking().Include(x => x.Contacts).FirstOrDefaultAsync(x => x.Id == id);
            return p is null ? Results.NotFound() : Results.Ok(ToDetail(p));
        });

        // Vérification de doublon pendant la saisie (ICE ou CIN).
        g.MapGet("/doublon", async (PartenairesDbContext db, string? ice, string? cin, Guid? sauf) =>
        {
            var existant = await TrouverDoublon(db, Clean(ice), Clean(cin), sauf);
            return Results.Ok(new { doublon = existant is not null, id = existant?.Id, nom = existant?.NomAffiche });
        });

        g.MapPost("/", async (PartenaireInput input, PartenairesDbContext db) =>
        {
            var erreurs = Valider(input);
            if (erreurs.Count > 0) return Results.ValidationProblem(erreurs);
            var doublon = await TrouverDoublon(db, Clean(input.Ice), Clean(input.Cin), null);
            if (doublon is not null) return Conflit(doublon);

            var p = new Partenaire();
            Appliquer(p, input);
            p.Contacts = (input.Contacts ?? []).Where(c => !string.IsNullOrWhiteSpace(c.Nom))
                .Select(c => new Contact { Nom = c.Nom.Trim(), Fonction = Clean(c.Fonction), Telephone = Clean(c.Telephone), Email = Clean(c.Email) })
                .ToList();
            db.Partenaires.Add(p);
            await db.SaveChangesAsync();
            return Results.Created($"/api/partenaires/{p.Id}", ToDetail(p));
        }).RequireAuthorization(Policies.Commercial);

        g.MapPut("/{id:guid}", async (Guid id, PartenaireInput input, PartenairesDbContext db) =>
        {
            var p = await db.Partenaires.Include(x => x.Contacts).FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            var erreurs = Valider(input);
            if (erreurs.Count > 0) return Results.ValidationProblem(erreurs);
            var doublon = await TrouverDoublon(db, Clean(input.Ice), Clean(input.Cin), id);
            if (doublon is not null) return Conflit(doublon);

            Appliquer(p, input);
            p.UpdatedAt = DateTime.UtcNow;
            db.Contacts.RemoveRange(p.Contacts);
            foreach (var c in (input.Contacts ?? []).Where(c => !string.IsNullOrWhiteSpace(c.Nom)))
                db.Contacts.Add(new Contact { PartenaireId = p.Id, Nom = c.Nom.Trim(), Fonction = Clean(c.Fonction), Telephone = Clean(c.Telephone), Email = Clean(c.Email) });
            await db.SaveChangesAsync();
            var frais = await db.Partenaires.AsNoTracking().Include(x => x.Contacts).FirstAsync(x => x.Id == id);
            return Results.Ok(ToDetail(frais));
        }).RequireAuthorization(Policies.Commercial);

        g.MapPost("/{id:guid}/archive", async (Guid id, bool archive, PartenairesDbContext db) =>
        {
            var p = await db.Partenaires.FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            p.Archive = archive;
            p.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(Policies.Commercial);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static IResult Conflit(Partenaire d) => Results.Conflict(new
    {
        message = $"Un client ou partenaire existe déjà avec cet identifiant : {d.NomAffiche}.",
        id = d.Id,
    });

    private static async Task<Partenaire?> TrouverDoublon(PartenairesDbContext db, string? ice, string? cin, Guid? sauf)
    {
        if (ice is null && cin is null) return null;
        return await db.Partenaires.AsNoTracking()
            .Where(p => p.Id != sauf)
            .FirstOrDefaultAsync(p => (ice != null && p.Ice == ice) || (cin != null && p.Cin == cin));
    }

    private static Dictionary<string, string[]> Valider(PartenaireInput i)
    {
        var e = new Dictionary<string, string[]>();
        if (!TypesPartenaire.Labels.ContainsKey(i.Type)) e["type"] = ["Type inconnu."];
        if (i.Nature == Natures.Morale)
        {
            if (string.IsNullOrWhiteSpace(i.RaisonSociale)) e["raisonSociale"] = ["La raison sociale est obligatoire."];
            var ice = Clean(i.Ice);
            if (ice is not null && (ice.Length != 15 || !ice.All(char.IsDigit))) e["ice"] = ["L'ICE comporte 15 chiffres."];
        }
        else if (i.Nature == Natures.Physique)
        {
            if (string.IsNullOrWhiteSpace(i.NomFamille)) e["nomFamille"] = ["Le nom est obligatoire."];
            if (string.IsNullOrWhiteSpace(i.Prenom)) e["prenom"] = ["Le prénom est obligatoire."];
        }
        else if (i.Nature == Natures.Autre)
        {
            if (string.IsNullOrWhiteSpace(i.RaisonSociale)) e["raisonSociale"] = ["Le nom est obligatoire."];
        }
        else e["nature"] = ["Nature inconnue."];
        var email = Clean(i.Email);
        if (email is not null && !email.Contains('@')) e["email"] = ["Adresse e-mail invalide."];
        return e;
    }

    private static void Appliquer(Partenaire p, PartenaireInput i)
    {
        p.Type = i.Type;
        p.Nature = i.Nature;
        var autre = i.Nature == Natures.Autre;
        var morale = i.Nature == Natures.Morale;
        var physique = i.Nature == Natures.Physique;
        p.RaisonSociale = morale || autre ? Clean(i.RaisonSociale) : null;
        p.Ice = morale ? Clean(i.Ice) : null;
        p.Rc = morale ? Clean(i.Rc) : null;
        p.IdentifiantFiscal = morale ? Clean(i.IdentifiantFiscal) : null;
        p.Patente = morale ? Clean(i.Patente) : null;
        p.Civilite = physique ? Clean(i.Civilite) : null;
        p.Nom = physique ? Clean(i.NomFamille) : null;
        p.Prenom = physique ? Clean(i.Prenom) : null;
        p.Cin = physique ? Clean(i.Cin)?.ToUpperInvariant() : null;
        p.Nationalite = physique ? Clean(i.Nationalite) : null;
        p.DateNaissance = physique ? i.DateNaissance : null;
        p.LieuNaissance = physique ? Clean(i.LieuNaissance) : null;
        p.Adresse = Clean(i.Adresse);
        p.Ville = Clean(i.Ville);
        p.Telephone = Clean(i.Telephone);
        p.Email = Clean(i.Email);
        p.ExonereTva = i.ExonereTva;
        // L'attestation n'a de sens que pour un client exonéré.
        p.AttestationNumero = i.ExonereTva ? Clean(i.AttestationNumero) : null;
        p.AttestationExpiration = i.ExonereTva ? i.AttestationExpiration : null;
    }

    private static PartenaireDetail ToDetail(Partenaire p) => new(p.Id, p.Type, p.Nature, p.NomAffiche,
        p.RaisonSociale, p.Ice, p.Rc, p.IdentifiantFiscal, p.Patente,
        p.Civilite, p.Nom, p.Prenom, p.Cin, p.Nationalite, p.DateNaissance, p.LieuNaissance,
        p.Adresse, p.Ville, p.Telephone, p.Email, p.ExonereTva, p.Archive, p.CreatedAt, p.UpdatedAt,
        p.Contacts.OrderBy(c => c.Nom).Select(c => new ContactDto(c.Id, c.Nom, c.Fonction, c.Telephone, c.Email)).ToList(),
        p.AttestationNumero, p.AttestationExpiration);
}
