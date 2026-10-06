using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

public static class Fonctions
{
    public const string Ingenieur = "ingenieur";
    public const string ChefChantier = "chef_chantier";
}

/// <summary>Affectation d'une personne à un projet. Le retrait renseigne la date de fin et garde l'historique.</summary>
public class Affectation : Entity
{
    public Guid ProjetId { get; set; }
    public string Fonction { get; set; } = "";
    public Guid UserId { get; set; }
    public string UserNom { get; set; } = "";
    public DateOnly Debut { get; set; }
    public DateOnly? Fin { get; set; }
}
