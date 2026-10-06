using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Devis.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Devis.Infrastructure;

/// <summary>Contenu d'un devis pour son PDF, et vérification d'un devis signé par le jeton de son QR code.</summary>
public class DevisDocuments(DevisDbContext db) : IDevisDocuments, IVerificationDocuments
{
    public async Task<DevisDocument?> DocumentAsync(Guid id, CancellationToken ct = default)
    {
        var d = await db.Devis.AsNoTracking().Include(x => x.Lignes).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return null;
        return new DevisDocument(d.Id, d.Numero, d.Version, d.Entite, d.ProjetId, d.Etat, EtatsDevis.Labels.GetValueOrDefault(d.Etat, d.Etat), d.DateDevis, d.Echeance, d.CommercialNom,
            d.Lignes.OrderBy(l => l.Ordre).Select(l => new DevisDocumentLigne(l.Reference, l.Emplacement, l.Surface, l.RatioActif, l.RatioPassif, l.PrixM2, l.MontantHT)).ToList(),
            d.SurfaceTotale, d.TotalHT, d.RemiseM2, d.RemiseTotale, d.NetHT, d.ExonereTva, d.Tva, d.TotalTTC,
            d.Echeances.Select(e => new KeyValuePair<string, decimal>(e.Label, e.Pourcentage)).ToList(), d.ModalitesContrat, d.SignePar, d.SigneLe, d.Jeton);
    }

    public async Task<DocumentVerifie?> TrouverAsync(string jeton, CancellationToken ct = default)
    {
        var d = await db.Devis.AsNoTracking().FirstOrDefaultAsync(x => x.Jeton == jeton, ct);
        if (d is null) return null;
        var fr = new CultureInfo("fr-FR");
        return new DocumentVerifie("Devis", $"{d.Numero}, version {d.Version}", Entites.Labels.GetValueOrDefault(d.Entite, d.Entite),
            [new("Date du devis", d.DateDevis.ToString("dd/MM/yyyy")), new("Valable jusqu'au", d.Echeance.ToString("dd/MM/yyyy")),
             new("Surface totale", d.SurfaceTotale.ToString("N2", fr) + " m²"), new("Total TTC", d.TotalTTC.ToString("N2", fr) + " DH"),
             new("État", EtatsDevis.Labels.GetValueOrDefault(d.Etat, d.Etat))],
            d.SignePar ?? "", d.SigneLe,
            d.Etat switch
            {
                EtatsDevis.Annule => "Ce devis a été annulé : il n'a plus de valeur.",
                EtatsDevis.Remplace => "Ce devis a été remplacé par une version plus récente : il n'a plus de valeur.",
                EtatsDevis.Expire => "Ce devis a expiré : il n'engage plus l'entreprise.",
                _ => null,
            });
    }
}
