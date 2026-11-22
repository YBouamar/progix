using Progix.SharedKernel;

namespace Progix.Modules.Devis.Infrastructure;

/// <summary>Série DEV de chaque entité, partagée avec les devis de vente directe du module Négoce.</summary>
public class NumerotationDevisPartagee(DevisDbContext db) : INumerotationDevis
{
    public async Task<string> ProchainAsync(string entite, int annee, CancellationToken ct = default) =>
        $"DEV-{await db.NumeroSuivantAsync($"devis:{entite}:{annee}", ct):00000}-{annee}";
}
