using Progix.SharedKernel;

namespace Progix.Modules.Fichiers.Domain;

/// <summary>Fichier déposé. Le contenu est dans le stockage S3, seule la description est en base.</summary>
public class Fichier : Entity
{
    public string Contexte { get; set; } = "";
    public Guid CibleId { get; set; }
    public string? Categorie { get; set; }
    public string Nom { get; set; } = "";
    public long Taille { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public string Cle { get; set; } = "";
    public Guid AjouteParId { get; set; }
    public string AjoutePar { get; set; } = "";
    /// <summary>Envoi en plusieurs morceaux : le fichier n'est visible qu'une fois complet.</summary>
    public bool Complet { get; set; }
    public string? EnvoiId { get; set; }

    public bool Supprime { get; set; }
    public string? SupprimePar { get; set; }
    public DateTime? SupprimeLe { get; set; }

    /// <summary>Date à laquelle le contenu a été effacé du stockage, un mois après la suppression.</summary>
    public DateTime? PurgeLe { get; set; }
}
