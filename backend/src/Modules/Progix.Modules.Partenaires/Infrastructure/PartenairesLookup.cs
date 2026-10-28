using Microsoft.EntityFrameworkCore;
using Progix.Modules.Partenaires.Infrastructure;
using Progix.SharedKernel;

namespace Progix.Modules.Partenaires;

public class PartenairesLookup(PartenairesDbContext db) : IPartenairesLookup
{
    public async Task<IReadOnlyDictionary<Guid, string>> NomsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var liste = ids.Distinct().ToList();
        if (liste.Count == 0) return new Dictionary<Guid, string>();
        var rows = await db.Partenaires.AsNoTracking().Where(p => liste.Contains(p.Id)).ToListAsync(ct);
        return rows.ToDictionary(p => p.Id, p => p.NomAffiche);
    }

    public async Task<IReadOnlyDictionary<Guid, PartenaireFiche>> FichesAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var liste = ids.Distinct().ToList();
        if (liste.Count == 0) return new Dictionary<Guid, PartenaireFiche>();
        var rows = await db.Partenaires.AsNoTracking().Include(p => p.Contacts).Where(p => liste.Contains(p.Id)).ToListAsync(ct);
        return rows.ToDictionary(p => p.Id, p =>
        {
            var c = p.Contacts.OrderBy(x => x.CreatedAt).FirstOrDefault();
            var contact = c is null ? p.Telephone : string.Join(" · ", new[] { c.Nom, c.Telephone ?? p.Telephone }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return new PartenaireFiche(p.Id, p.NomAffiche, p.Type, string.IsNullOrWhiteSpace(contact) ? null : contact, Exonere(p));
        });
    }

    public async Task<IReadOnlyDictionary<Guid, PartenaireIdentite>> IdentitesAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var liste = ids.Distinct().ToList();
        if (liste.Count == 0) return new Dictionary<Guid, PartenaireIdentite>();
        var rows = await db.Partenaires.AsNoTracking().Where(p => liste.Contains(p.Id)).ToListAsync(ct);
        return rows.ToDictionary(p => p.Id, p => new PartenaireIdentite(p.Id, p.NomAffiche, p.Nature, p.Adresse, p.Ville, p.Telephone, p.Rc, p.Ice,
            p.Civilite, p.Nationalite, p.DateNaissance, p.LieuNaissance, p.Cin));
    }

    public async Task<PartenaireLegal?> LegalAsync(Guid id, CancellationToken ct = default)
    {
        var p = await db.Partenaires.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        var manques = new List<string>();
        var manquesContrat = new List<string>();
        string? identifiants;
        if (p.Nature == Domain.Natures.Morale)
        {
            // Personne morale : ICE et registre de commerce exigés pour facturer et pour le contrat ;
            // identifiant fiscal et patente exigés pour le contrat seulement (pas pour la facture).
            if (string.IsNullOrWhiteSpace(p.Ice)) manques.Add("ICE");
            if (string.IsNullOrWhiteSpace(p.Rc)) manques.Add("registre de commerce");
            manquesContrat.AddRange(manques);
            if (string.IsNullOrWhiteSpace(p.IdentifiantFiscal)) manquesContrat.Add("identifiant fiscal");
            if (string.IsNullOrWhiteSpace(p.Patente)) manquesContrat.Add("patente");
            identifiants = string.Join(" · ", new[] { ("ICE", p.Ice), ("RC", p.Rc), ("IF", p.IdentifiantFiscal), ("Patente", p.Patente) }
                .Where(x => !string.IsNullOrWhiteSpace(x.Item2)).Select(x => $"{x.Item1} {x.Item2}"));
        }
        else identifiants = string.IsNullOrWhiteSpace(p.Cin) ? null : $"CIN {p.Cin}";
        var adresse = string.Join(", ", new[] { p.Adresse, p.Ville }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return new PartenaireLegal(p.Id, p.NomAffiche, p.Nature, string.IsNullOrWhiteSpace(identifiants) ? null : identifiants,
            adresse.Length == 0 ? null : adresse, Exonere(p), manques, manquesContrat);
    }

    /// <summary>Une attestation d'exonération expirée remet d'office le client à la TVA normale.</summary>
    private static bool Exonere(Domain.Partenaire p) =>
        p.ExonereTva && (p.AttestationExpiration is null || p.AttestationExpiration >= DateOnly.FromDateTime(DateTime.UtcNow));

    public async Task<IReadOnlyList<Guid>> RechercherAsync(string texte, CancellationToken ct = default)
    {
        var like = $"%{texte.Trim()}%";
        return await db.Partenaires.AsNoTracking()
            .Where(p => EF.Functions.ILike(p.RaisonSociale ?? "", like) || EF.Functions.ILike(p.Nom ?? "", like) || EF.Functions.ILike(p.Prenom ?? "", like))
            .Select(p => p.Id).Take(200).ToListAsync(ct);
    }
}
