namespace Progix.SharedKernel;

/// <summary>Les douze rôles de PROGIX et leurs droits transverses.</summary>
public static class Roles
{
    public const string Directeur = "directeur";
    public const string ChefProjet = "chef_projet";
    public const string ResponsableAdministratif = "responsable_administratif";
    public const string Administrateur = "administrateur";
    public const string AgentCommercial = "agent_commercial";
    public const string IngenieurChef = "ingenieur_chef";
    public const string Ingenieur = "ingenieur";
    public const string DessinateurChef = "dessinateur_chef";
    public const string Dessinateur = "dessinateur";
    public const string ResponsableLogistique = "responsable_logistique";
    public const string ResponsableChantier = "responsable_chantier";
    public const string ChefChantier = "chef_chantier";

    public sealed record Definition(string Code, string Label, bool VoitMontants);

    public static readonly IReadOnlyList<Definition> All =
    [
        new(Directeur, "Directeur", true),
        new(ChefProjet, "Chef de projet", true),
        new(ResponsableAdministratif, "Responsable administratif", true),
        new(Administrateur, "Administrateur", true),
        new(AgentCommercial, "Agent commercial", false),
        new(IngenieurChef, "Ingénieur en chef", true),
        new(Ingenieur, "Ingénieur", true),
        new(DessinateurChef, "Dessinateur en chef", false),
        new(Dessinateur, "Dessinateur", false),
        new(ResponsableLogistique, "Responsable logistique", false),
        new(ResponsableChantier, "Responsable chantier", false),
        new(ChefChantier, "Chef de chantier", false),
    ];

    public static Definition? Find(string code) => All.FirstOrDefault(r => r.Code == code);
}

/// <summary>Noms des politiques d'autorisation partagées entre modules.</summary>
public static class Policies
{
    /// <summary>Gestion des utilisateurs : directeur seul.</summary>
    public const string GestionUtilisateurs = "gestion-utilisateurs";

    /// <summary>Partie commerciale : clients, partenaires, projets, paiements, dépenses.</summary>
    public const string Commercial = "commercial";

    /// <summary>Devis : rôles qui voient les montants.</summary>
    public const string Devis = "devis";

    public static readonly string[] RolesDevis = Roles.All.Where(r => r.VoitMontants).Select(r => r.Code).ToArray();

    /// <summary>Logistique : commandes usine et stock.</summary>
    public const string Logistique = "logistique";

    public static readonly string[] RolesLogistique = [Roles.Directeur, Roles.ChefProjet, Roles.ResponsableLogistique];

    /// <summary>Suppression et restauration d'un projet : directeur ou chef de projet.</summary>
    public const string Direction = "direction";

    public static readonly string[] RolesDirection = [Roles.Directeur, Roles.ChefProjet];

    public static readonly string[] RolesCommercial =
        [Roles.Directeur, Roles.ChefProjet, Roles.ResponsableAdministratif, Roles.Administrateur];
}
