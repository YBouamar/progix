using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Fichiers.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Fichiers.Infrastructure;

/// <summary>Lecture et dépôt de contenus pour les autres modules (génération du fond de plan d'une étude).</summary>
public class FichiersContenu(FichiersDbContext db, Stockage stockage) : IFichiersContenu
{
    private static FichierResume Resume(Fichier f) => new(f.Id, f.CibleId, f.Categorie, f.Nom, f.Taille, f.CreatedAt);

    public async Task<IReadOnlyList<FichierResume>> ListerAsync(string contexte, Guid cibleId, string? categorie, CancellationToken ct = default) =>
        (await db.Fichiers.AsNoTracking().Where(f => f.Contexte == contexte && f.CibleId == cibleId && f.Complet && !f.Supprime && (categorie == null || f.Categorie == categorie))
            .OrderByDescending(f => f.CreatedAt).ToListAsync(ct)).Select(Resume).ToList();

    public async Task<(FichierResume Fichier, MemoryStream Contenu)?> LireAsync(string contexte, Guid id, CancellationToken ct = default)
    {
        var f = await db.Fichiers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Contexte == contexte && x.Complet && !x.Supprime, ct);
        if (f is null) return null;
        var tampon = new MemoryStream();
        await using (var flux = await stockage.LireAsync(f.Cle, ct)) await flux.CopyToAsync(tampon, ct);
        tampon.Position = 0;
        return (Resume(f), tampon);
    }

    public async Task<FichierResume> DeposerAsync(string contexte, Guid cibleId, string? categorie, string nom, string contentType, MemoryStream contenu, ClaimsPrincipal par, CancellationToken ct = default)
    {
        var f = new Fichier
        {
            Contexte = contexte, CibleId = cibleId, Categorie = categorie, Nom = nom.Length > 255 ? nom[^255..] : nom, Taille = contenu.Length,
            ContentType = contentType, AjouteParId = par.UserId(), AjoutePar = par.UserName(), Complet = true,
        };
        f.Cle = $"{f.Contexte}/{f.CibleId:N}/{f.Id:N}";
        contenu.Position = 0;
        await stockage.DeposerAsync(f.Cle, contenu, contentType, ct);
        db.Fichiers.Add(f);
        await db.SaveChangesAsync(ct);
        return Resume(f);
    }
}
