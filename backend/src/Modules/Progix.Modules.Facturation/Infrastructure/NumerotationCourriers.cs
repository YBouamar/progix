using Microsoft.EntityFrameworkCore;
using Progix.Modules.Facturation.Domain;

namespace Progix.Modules.Facturation.Infrastructure;

/// <summary>
/// Numéros des courriers. Bordereaux : « 1/26 » (dépôt) ou « 01/26 » (envoi), par bureau destinataire (bureau d'études ou bureau de contrôle) et par an, communs à toutes les entités ;
/// le numéro d'un bordereau annulé redevient disponible. Attestation : ATT-XXXXX-AAAA, par entité et par an.
/// </summary>
public static class NumerotationCourriers
{
    /// <summary>Reprise des bordereaux numérotés avec un ancien format : ils reçoivent un numéro au format en vigueur, dans l'ordre de leur date.</summary>
    public static async Task ReprendreAsync(FacturationDbContext db, Progix.SharedKernel.IProjetsLookup projets, CancellationToken ct)
    {
        var bordereaux = await db.Courriers.Where(c => c.Type != TypesCourrier.Attestation).OrderBy(c => c.DateCourrier).ThenBy(c => c.CreatedAt).ToListAsync(ct);
        bool AuFormat(Courrier c) => System.Text.RegularExpressions.Regex.IsMatch(c.Numero, c.Type == TypesCourrier.BordereauBct ? @"^\d{2,}/\d{2}$" : @"^[1-9]\d*/\d{2}$");
        var aReprendre = bordereaux.Where(c => !AuFormat(c) || c.DestinataireId is null).ToList();
        if (aReprendre.Count == 0) return;
        // Les anciens numéros sont d'abord neutralisés, pour ne pas occuper un numéro du nouveau format.
        foreach (var c in aReprendre) { c.AncienNumero ??= c.Numero; c.Numero = $"~{c.Id:N}"[..20]; }
        await db.SaveChangesAsync(ct);
        foreach (var c in aReprendre)
        {
            if (c.DestinataireId is null)
            {
                var role = c.Type == TypesCourrier.BordereauBct ? "bct" : "bet";
                c.DestinataireId = (await projets.IntervenantsAsync(c.ProjetId, ct)).FirstOrDefault(i => i.Role == role)?.PartenaireId;
            }
            // Un bordereau annulé garde une trace de son ancien numéro mais n'occupe plus de numéro.
            c.Numero = c.Annule ? $"annulé {c.AncienNumero}"[..Math.Min(20, $"annulé {c.AncienNumero}".Length)] : await SuivantAsync(db, c, ct);
            await db.SaveChangesAsync(ct);
        }
    }

    public static async Task<string> SuivantAsync(FacturationDbContext db, Courrier c, CancellationToken ct = default)
    {
        var annee = c.DateCourrier.Year;
        if (c.Type == TypesCourrier.Attestation) return $"ATT-{await db.NumeroSuivantAsync($"att:{c.Entite}:{annee}", ct):00000}-{annee}";
        var suffixe = $"/{annee % 100:00}";
        var pris = (await db.Courriers.AsNoTracking()
                .Where(x => x.Id != c.Id && !x.Annule && x.Type == c.Type && x.DestinataireId == c.DestinataireId && x.Numero.EndsWith(suffixe))
                .Select(x => x.Numero).ToListAsync(ct))
            .Select(n => int.TryParse(n[..^suffixe.Length], out var v) ? v : 0).ToHashSet();
        // Premier numéro libre : un numéro annulé est repris avant d'en ouvrir un nouveau.
        var numero = 1;
        while (pris.Contains(numero)) numero++;
        // Envoi au bureau de contrôle : deux chiffres (« 03/26 ») ; dépôt au bureau d'études : « 3/26 ».
        return c.Type == TypesCourrier.BordereauBct ? $"{numero:00}{suffixe}" : $"{numero}{suffixe}";
    }
}
