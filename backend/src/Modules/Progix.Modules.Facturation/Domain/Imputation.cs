namespace Progix.Modules.Facturation.Domain;

/// <summary>Montant TTC d'une dalle au devis validé le plus récent qui la contient : la base de son taux de paiement.</summary>
public sealed record DalleDue(Guid DalleId, string Reference, string Emplacement, decimal MontantTTC);

/// <summary>Où en est le paiement d'une dalle.</summary>
public sealed record SituationDalle(Guid DalleId, string Reference, string Emplacement, decimal MontantTTC, decimal Avoirs, decimal Paye, decimal EffetsEnAttente)
{
    /// <summary>Dû après avoirs.</summary>
    public decimal Du => MontantTTC - Avoirs;
    public decimal Reste => Du - Paye;

    /// <summary>Ce qu'un nouveau paiement peut encore couvrir, effets en attente déduits.</summary>
    public decimal ACouvrir => Math.Max(0, Reste - EffetsEnAttente);

    /// <summary>Payé au-delà du dû, à rembourser au client.</summary>
    public decimal TropPercu => Math.Max(0, Paye - Du);

    /// <summary>Payé rapporté au montant TTC du devis, en pourcentage.</summary>
    public decimal? Taux => MontantTTC > 0 ? Math.Round(Paye / MontantTTC * 100m, 1) : null;
}

/// <summary>
/// Les paiements suivent les dalles ; une facture est réglée par imputation des paiements de ses dalles,
/// sur les factures les plus anciennes d'abord. Le surplus d'une dalle reste à son crédit (avance).
/// </summary>
public static class Imputation
{
    /// <summary>Répartit un montant au prorata des poids, au centime ; l'écart d'arrondi va à la dernière part.</summary>
    public static List<(Guid Cle, decimal Montant)> Repartir(decimal montant, IReadOnlyList<(Guid Cle, decimal Poids)> poids)
    {
        var total = poids.Sum(p => p.Poids);
        if (poids.Count == 0 || total <= 0) return [];
        var parts = poids.Select(p => (p.Cle, Montant: Math.Round(montant * p.Poids / total, 2))).ToList();
        var ecart = montant - parts.Sum(p => p.Montant);
        parts[^1] = (parts[^1].Cle, parts[^1].Montant + ecart);
        return parts;
    }

    /// <summary>Parts TTC d'une facture de livraison : son total TTC réparti au prorata du montant de chaque dalle.</summary>
    public static List<PartFacture> PartsLivraison(Facture f)
    {
        var lignes = f.Lignes.Where(l => l.DalleId is not null).OrderBy(l => l.Ordre).ToList();
        var parts = Repartir(f.TotalTTC, lignes.Select(l => (l.DalleId!.Value, l.MontantHT)).ToList());
        return parts.Select(p => new PartFacture
        {
            DalleId = p.Cle, MontantTTC = p.Montant,
            Reference = lignes.First(l => l.DalleId == p.Cle).Libelle.Split(" · ")[0],
        }).ToList();
    }

    /// <summary>Part d'une dalle encore due sur la facture, après ses avoirs (répartis au prorata).</summary>
    public static decimal PartDue(Facture f, PartFacture p) => f.TotalTTC <= 0 ? 0 : Math.Round(p.MontantTTC * f.Du / f.TotalTTC, 2);

    private static decimal Signe(Encaissement e, decimal m) => e.Remboursement ? -m : m;

    /// <summary>Renseigne le réglé et les effets en attente de chaque facture à partir des paiements des dalles.</summary>
    public static void Imputer(IReadOnlyCollection<Facture> factures, IEnumerable<Encaissement> encaissements, DateOnly jour)
    {
        foreach (var f in factures) { f.Regle = 0; f.EffetsEnAttente = 0; }
        var valides = encaissements.Where(e => !e.Annule).ToList();
        var parId = factures.ToDictionary(f => f.Id);

        // Règlement d'avant le suivi par dalle, sans ventilation : il reste imputé à sa facture.
        foreach (var e in valides.Where(e => e.Dalles.Count == 0 && e.FactureId is Guid id && parId.ContainsKey(id)))
        {
            var f = parId[e.FactureId!.Value];
            if (e.EnAttente(jour)) f.EffetsEnAttente += e.Montant; else f.Regle += Signe(e, e.Montant);
        }

        var paye = new Dictionary<Guid, decimal>();
        var attente = new Dictionary<Guid, decimal>();
        foreach (var e in valides)
            foreach (var d in e.Dalles)
            {
                var cible = e.EnAttente(jour) ? attente : paye;
                cible[d.DalleId] = cible.GetValueOrDefault(d.DalleId) + Signe(e, d.Montant);
            }

        var cibles = factures.Where(f => !f.Annulee).OrderBy(f => f.DateFacture).ThenBy(f => f.Numero)
            .SelectMany(f => f.Parts.Select(p => (Facture: f, p.DalleId, Due: PartDue(f, p))))
            .GroupBy(x => x.DalleId);
        foreach (var g in cibles)
        {
            var dispo = Math.Max(0, paye.GetValueOrDefault(g.Key));
            var enAttente = Math.Max(0, attente.GetValueOrDefault(g.Key));
            foreach (var (f, _, due) in g)
            {
                var regle = Math.Min(dispo, due);
                f.Regle += regle; dispo -= regle;
                var effet = Math.Min(enAttente, due - regle);
                f.EffetsEnAttente += effet; enAttente -= effet;
            }
        }
    }

    /// <summary>
    /// Situation de paiement de chaque dalle : montant du devis, avoirs, payé, effets en attente.
    /// Seuls les avoirs partiels réduisent le dû : un avoir total annule la facture et rend ses bons de livraison
    /// facturables à nouveau, la dalle reste donc due.
    /// </summary>
    public static List<SituationDalle> Situation(IEnumerable<DalleDue> dues, IReadOnlyCollection<Facture> factures, IEnumerable<Encaissement> encaissements, DateOnly jour)
    {
        var valides = encaissements.Where(e => !e.Annule).ToList();
        return dues.Select(d =>
        {
            var avoirs = factures.Where(f => !f.Annulee)
                .SelectMany(f => f.Parts.Where(p => p.DalleId == d.DalleId).Select(p => p.MontantTTC - PartDue(f, p))).Sum();
            var parts = valides.SelectMany(e => e.Dalles.Where(x => x.DalleId == d.DalleId).Select(x => (e, x.Montant))).ToList();
            var paye = parts.Where(x => !x.e.EnAttente(jour)).Sum(x => Signe(x.e, x.Montant));
            var attente = parts.Where(x => x.e.EnAttente(jour)).Sum(x => x.Montant);
            return new SituationDalle(d.DalleId, d.Reference, d.Emplacement, d.MontantTTC, avoirs, paye, attente);
        }).ToList();
    }
}
