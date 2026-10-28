using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Facturation.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Infrastructure;

/// <summary>Factures, avoirs et reçus retrouvés par le jeton de leur QR code.</summary>
public class VerificationFacturation(FacturationDbContext db) : IVerificationDocuments
{
    private static readonly CultureInfo Fr = new("fr-FR");
    private static string DH(decimal v) => v.ToString("N2", Fr) + " DH";
    private static string Date(DateOnly d) => d.ToString("dd/MM/yyyy");
    private static KeyValuePair<string, string> L(string label, string valeur) => new(label, valeur);

    public async Task<DocumentVerifie?> TrouverAsync(string jeton, CancellationToken ct = default)
    {
        async Task<string> Entite(string code) => (await db.Entites.AsNoTracking().FirstOrDefaultAsync(e => e.Code == code, ct))?.RaisonSociale ?? code;

        if (await db.Factures.AsNoTracking().Include(f => f.Avoirs).FirstOrDefaultAsync(f => f.Jeton == jeton, ct) is { } f)
            return new DocumentVerifie(f.Type == TypesFacture.Acompte ? "Facture d'acompte" : "Facture", f.Numero, await Entite(f.Entite),
                [L("Client", f.ClientNom), L("Date d'émission", Date(f.DateFacture)), L("Projet", f.ProjetCode), L("Total hors taxes", DH(f.NetHT)), L("Total TTC", DH(f.TotalTTC))],
                f.SignePar ?? "", f.SigneLe, f.Annulee ? "Cette facture a été annulée par un avoir total : elle n'a plus de valeur." : null);

        if (await db.Avoirs.AsNoTracking().FirstOrDefaultAsync(a => a.Jeton == jeton, ct) is { } a)
        {
            var fa = await db.Factures.AsNoTracking().FirstAsync(x => x.Id == a.FactureId, ct);
            return new DocumentVerifie("Avoir", a.Numero, await Entite(a.Entite),
                [L("Client", fa.ClientNom), L("Sur la facture", fa.Numero), L("Date d'émission", Date(a.DateAvoir)), L("Total TTC de l'avoir", DH(a.TotalTTC))],
                a.SignePar ?? "", a.SigneLe, a.Annule ? "Cet avoir a été annulé après son émission : il n'a plus de valeur." : null);
        }

        if (await db.Contrats.AsNoTracking().FirstOrDefaultAsync(c => c.Jeton == jeton, ct) is { } ctr)
            return new DocumentVerifie("Contrat", ctr.Numero, await Entite(ctr.Entite),
                [L("Date du contrat", Date(ctr.DateContrat)), L("Fait à", ctr.Lieu), L("Sur le devis", ctr.DevisNumero)],
                ctr.SignePar ?? "", ctr.SigneLe, ctr.Annule ? "Ce contrat a été annulé : il n'a plus de valeur." : null);

        if (await db.Courriers.AsNoTracking().FirstOrDefaultAsync(c => c.Jeton == jeton, ct) is { } co)
            return new DocumentVerifie(TypesCourrier.Labels.GetValueOrDefault(co.Type, "Courrier"), co.Numero, await Entite(co.Entite),
                [L("Date", Date(co.DateCourrier)), L("Objet", co.Objet), L(co.Type == TypesCourrier.Attestation ? "Dalles attestées" : "Plans transmis", co.Lignes.Count.ToString())],
                co.SignePar ?? "", co.SigneLe, co.Annule ? "Ce courrier a été annulé : il n'a plus de valeur." : null);

        if (await db.SignaturesFiches.AsNoTracking().FirstOrDefaultAsync(x => x.Jeton == jeton, ct) is { } fiche)
            return new DocumentVerifie("Fiche de chantier", fiche.Reference, "",
                [L("Fiche", System.Globalization.CultureInfo.GetCultureInfo("fr-FR").TextInfo.ToTitleCase(fiche.Titre.ToLowerInvariant())), L("Dalle", fiche.Reference)],
                fiche.SignePar, fiche.SigneLe, null);

        if (await db.BonsLivraison.AsNoTracking().Include(b => b.Lignes).FirstOrDefaultAsync(b => b.Jeton == jeton, ct) is { } bl)
            return new DocumentVerifie("Bon de livraison", bl.Numero, await Entite(bl.Entite),
                [L("Date de livraison", Date(bl.DateLivraison)), L("Dalles livrées", string.Join(", ", bl.Lignes.OrderBy(l => l.Ordre).Select(l => l.Reference))), L("Surface livrée", bl.Surface.ToString("N2", Fr) + " m²")],
                bl.SignePar ?? "", bl.SigneLe, null);

        if (await db.Encaissements.AsNoTracking().FirstOrDefaultAsync(x => x.Jeton == jeton, ct) is { } x)
        {
            return new DocumentVerifie(x.Remboursement ? "Reçu de remboursement" : "Reçu de paiement", x.NumeroRecu ?? "", await Entite(x.Entite),
                [L("Client", x.ClientNom), L("Projet", x.ProjetCode), L("Dalles", string.Join(", ", x.Dalles.Select(d => d.Reference))),
                 L(x.Remboursement ? "Date du remboursement" : "Date du paiement", Date(x.DatePaiement)),
                 L("Mode", ModesPaiement.Libelles.GetValueOrDefault(x.Mode, x.Mode)), L(x.Remboursement ? "Montant remboursé au client" : "Montant reçu", DH(x.Montant))],
                x.SaisiPar, x.SigneLe, x.Annule ? "Cette opération a été annulée après l'émission du reçu : le reçu n'a plus de valeur." : null);
        }
        return null;
    }
}
