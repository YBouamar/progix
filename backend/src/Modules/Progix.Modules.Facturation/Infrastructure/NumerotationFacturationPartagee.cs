using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Infrastructure;

/// <summary>Séries BL, FAC et AV de chaque entité, partagées avec les ventes directes du module Négoce.</summary>
public class NumerotationFacturationPartagee(FacturationDbContext db) : INumerotationFacturation
{
    public async Task<string> ProchainAsync(string type, string entite, int annee, CancellationToken ct = default)
    {
        var prefixe = type switch
        {
            TypesNumero.BonLivraison => "BL",
            TypesNumero.Facture => "FAC",
            TypesNumero.Avoir => "AV",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Type de numéro inconnu."),
        };
        return $"{prefixe}-{await db.NumeroSuivantAsync($"{type}:{entite}:{annee}", ct):00000}-{annee}";
    }
}
