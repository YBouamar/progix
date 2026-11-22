using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Negoce.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Negoce.Infrastructure;

/// <summary>Devis, bons, factures et avoirs de vente directe retrouvés par le jeton de leur QR code.</summary>
public class VerificationNegoce(NegoceDbContext db) : IVerificationDocuments
{
    private static readonly CultureInfo Fr = new("fr-FR");
    private static string DH(decimal v) => v.ToString("N2", Fr) + " DH";
    private static KeyValuePair<string, string> L(string label, string valeur) => new(label, valeur);

    public async Task<DocumentVerifie?> TrouverAsync(string jeton, CancellationToken ct = default)
    {
        var d = await db.Documents.AsNoTracking().Include(x => x.Lignes).FirstOrDefaultAsync(x => x.Jeton == jeton, ct);
        if (d is null) return null;
        var type = d.Type switch { TypesVente.Devis => "Devis", TypesVente.Livraison => "Bon de livraison", TypesVente.Avoir => "Avoir", _ => "Facture" };
        var lignes = new List<KeyValuePair<string, string>> { L("Client", d.ClientNom), L("Date", d.DateDocument.ToString("dd/MM/yyyy")) };
        if (d.Type == TypesVente.Livraison) lignes.Add(L("Articles livrés", d.Lignes.Count.ToString()));
        else { lignes.Add(L("Total hors taxes", DH(d.TotalHT))); lignes.Add(L("Total TTC", DH(d.TotalTTC))); }
        return new DocumentVerifie(type, d.Numero, d.Entite, lignes, d.SignePar ?? "", d.SigneLe, d.Annule ? "Ce document a été annulé après sa signature : il n'a plus de valeur." : null);
    }
}

/// <summary>Espèces encaissées en vente directe, pour le plafond mensuel par client.</summary>
public class EspecesNegoce(NegoceDbContext db) : IEspecesClient
{
    public const string Nom = "negoce";
    public string Source => Nom;

    public async Task<decimal> DuMoisAsync(Guid clientId, DateOnly date, CancellationToken ct = default)
    {
        var debut = new DateOnly(date.Year, date.Month, 1);
        var fin = debut.AddMonths(1);
        return await db.PaiementsClient.Where(p => p.ClientId == clientId && !p.Annule && p.Mode == ModesPaiementNegoce.Especes && p.DatePaiement >= debut && p.DatePaiement < fin)
            .SumAsync(p => (decimal?)p.Montant, ct) ?? 0;
    }
}
