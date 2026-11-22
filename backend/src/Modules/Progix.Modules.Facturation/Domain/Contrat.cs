using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Domain;

/// <summary>Contrat établi sur un devis validé. Numéro CTR-XXXXX-AAAA, par entité et par an. Il ne se modifie pas ; il s'annule avec un motif.</summary>
public class Contrat : Entity
{
    public string Numero { get; set; } = "";
    public string Entite { get; set; } = "";
    public string? AncienNumero { get; set; }
    public Guid ProjetId { get; set; }
    public Guid DevisId { get; set; }
    public string DevisNumero { get; set; } = "";
    public DateOnly DateContrat { get; set; }
    public string Lieu { get; set; } = "Casablanca";
    public string EmisPar { get; set; } = "";

    // Signature électronique par mot de passe : obligatoire avant d'imprimer ou d'envoyer le contrat.
    public string? SignePar { get; set; }
    public DateTime? SigneLe { get; set; }
    public string? Jeton { get; set; }

    /// <summary>Suivi du contrat auprès du client : en attente, envoyé, validé par le client (voir <see cref="StatutsContrat"/>).</summary>
    public string Statut { get; set; } = StatutsContrat.EnAttente;
    public DateTime? EnvoyeLe { get; set; }
    public string? EnvoyePar { get; set; }
    public DateTime? ValideClientLe { get; set; }
    public string? ValideClientPar { get; set; }

    public bool Annule { get; set; }
    public string? MotifAnnulation { get; set; }
    public string? AnnulePar { get; set; }
    public DateTime? AnnuleLe { get; set; }
}

/// <summary>Statuts de suivi d'un contrat établi : il part au client, puis revient validé (signé et cacheté par lui).</summary>
public static class StatutsContrat
{
    public const string EnAttente = "en_attente";
    public const string Envoye = "envoye";
    public const string ValideClient = "valide_client";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [EnAttente] = "En attente", [Envoye] = "Envoyé", [ValideClient] = "Validé par le client",
    };

    private static readonly string[] Ordre = [EnAttente, Envoye, ValideClient];

    /// <summary>
    /// Passe le contrat au statut demandé, d'un cran à la fois. Avancer exige un contrat signé électroniquement (il ne part pas sans signature) ;
    /// reculer d'un cran corrige une erreur et revient aux fonctions qui annulent. Renvoie un motif de refus, ou null.
    /// </summary>
    public static string? Passer(Contrat c, string cible, bool peutReculer, string par, DateTime le)
    {
        if (!Ordre.Contains(cible)) return "Statut inconnu.";
        if (c.Annule) return "Ce contrat est annulé.";
        var de = Array.IndexOf(Ordre, c.Statut);
        var vers = Array.IndexOf(Ordre, cible);
        if (vers == de) return $"Le contrat est déjà « {Labels[cible].ToLowerInvariant()} ».";
        if (vers > de)
        {
            if (vers != de + 1) return "Le contrat passe d'abord par « envoyé » avant d'être validé par le client.";
            if (c.Jeton is null) return "Signez le contrat avant de le marquer envoyé : il ne s'imprime ni ne s'envoie sans signature.";
            if (cible == Envoye) { c.EnvoyeLe = le; c.EnvoyePar = par; }
            else { c.ValideClientLe = le; c.ValideClientPar = par; }
        }
        else
        {
            if (!peutReculer) return "Revenir à un statut précédent revient au responsable administratif, au chef de projet ou au directeur.";
            if (vers != de - 1) return "Le contrat revient d'un seul statut à la fois.";
            if (de == 2) { c.ValideClientLe = null; c.ValideClientPar = null; }
            else { c.EnvoyeLe = null; c.EnvoyePar = null; }
        }
        c.Statut = cible;
        return null;
    }
}
