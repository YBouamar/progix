using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Progix.Modules.Fichiers.Infrastructure;

/// <summary>
/// Un fichier supprimé garde son contenu un mois, le temps des sauvegardes, puis il est effacé du stockage.
/// Les envois abandonnés depuis plus de deux jours sont nettoyés au passage.
/// </summary>
public class PurgeFichiers(FichiersDbContext db, Stockage stockage, ILogger<PurgeFichiers> logger)
{
    public const int JoursDeConservation = 30;

    public async Task<int> ExecuterAsync()
    {
        var limite = DateTime.UtcNow.AddDays(-JoursDeConservation);
        var aPurger = await db.Fichiers.Where(f => f.Complet && f.Supprime && f.PurgeLe == null && f.SupprimeLe < limite).Take(500).ToListAsync();
        foreach (var f in aPurger)
        {
            await stockage.SupprimerAsync(f.Cle, CancellationToken.None);
            f.PurgeLe = DateTime.UtcNow;
        }

        var abandon = DateTime.UtcNow.AddDays(-2);
        var inacheves = await db.Fichiers.Where(f => !f.Complet && f.CreatedAt < abandon).Take(500).ToListAsync();
        foreach (var f in inacheves)
        {
            try { if (f.EnvoiId is not null) await stockage.AbandonnerEnvoiAsync(f.Cle, f.EnvoiId, CancellationToken.None); }
            catch (Exception ex) { logger.LogWarning("Envoi {Id} déjà absent du stockage : {Message}", f.Id, ex.Message); }
            db.Fichiers.Remove(f);
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Purge des fichiers : {Purges} contenus effacés, {Inacheves} envois abandonnés nettoyés.", aPurger.Count, inacheves.Count);
        return aPurger.Count;
    }
}
