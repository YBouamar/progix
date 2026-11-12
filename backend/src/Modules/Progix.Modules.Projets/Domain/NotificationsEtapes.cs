using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

/// <summary>
/// Notifications de changement d'étape : chacun est prévenu quand c'est son tour.
/// Une fonction est concernée par l'étape en cours d'une dalle si elle la réalise, ou si l'étape attend sa validation.
/// Le directeur et le chef de projet, qui peuvent tout réaliser, ne sont prévenus que des validations qui leur reviennent.
/// </summary>
public static class NotificationsEtapes
{
    public const string ATour = "a_tour";
    public const string AValider = "a_valider";

    /// <summary>Fonctions qui ne suivent que les projets ou les dalles où elles sont affectées ; les autres suivent tous les projets.</summary>
    public static readonly string[] RolesAffectes = [Roles.Ingenieur, Roles.Dessinateur, Roles.ChefChantier];

    /// <param name="Le">Moment où l'étape est devenue l'affaire de la personne.</param>
    /// <param name="Par">Personne dont l'action a déclenché la notification, si elle est connue.</param>
    public record Avis(string Genre, int Numero, DateTime Le, string? Par, string Texte);

    /// <summary>Dernière action d'une étape : sa réalisation ou la plus récente de ses validations.</summary>
    public static (DateTime Le, string? Par)? DerniereAction(EtapeDalle? e)
    {
        if (e is null) return null;
        var actions = new (DateTime? Le, string? Par)[]
        {
            (e.FaitLe, e.FaitPar), (e.ValidationTechniqueLe, e.ValidationTechniquePar),
            (e.ValidationAdministrativeLe, e.ValidationAdministrativePar), (e.ValidationChantierLe, e.ValidationChantierPar),
        };
        var derniere = actions.Where(a => a.Le is not null).OrderByDescending(a => a.Le).FirstOrDefault();
        return derniere.Le is DateTime le ? (le, derniere.Par) : null;
    }

    /// <summary>Validations de l'étape qui reviennent à la personne et ne sont pas encore données.</summary>
    public static IReadOnlyList<string> ValidationsAttendues(Etapes.Definition def, EtapeDalle? e, Func<string, bool> aRole)
    {
        if (!def.DoubleValidation) return [];
        var l = new List<string>();
        var directeur = aRole(Roles.Directeur);
        if (e?.ValidationTechniqueLe is null && (directeur || aRole(Roles.ChefProjet))) l.Add("technique");
        if (e?.ValidationAdministrativeLe is null && (directeur || aRole(Roles.ResponsableAdministratif))) l.Add("administrative");
        if (def.ValidationChantier && e?.ValidationChantierLe is null && (directeur || aRole(Roles.ResponsableChantier))) l.Add("du responsable des chantiers");
        return l;
    }

    /// <summary>
    /// Avis que l'étape en cours d'une dalle vaut à la personne, ou rien.
    /// </summary>
    /// <param name="numero">Étape en cours de la dalle.</param>
    /// <param name="etape">Sa réalisation, si elle est commencée.</param>
    /// <param name="debut">Moment où elle est devenue l'étape en cours (fin de la précédente, création de la dalle, ou déverrouillage) et son auteur.</param>
    public static IEnumerable<Avis> Pour(int numero, EtapeDalle? etape, (DateTime Le, string? Par) debut, bool rouverte, Func<string, bool> aRole)
    {
        var def = Etapes.Get(numero);
        var realise = def.Roles.Any(aRole);
        var action = DerniereAction(etape);

        // Validations : dès que l'étape est en cours si elles précèdent la saisie, sinon dès qu'elle est réalisée.
        var attendues = ValidationsAttendues(def, etape, aRole);
        if (attendues.Count > 0)
        {
            var quoi = $"{def.Label} attend votre validation {string.Join(" et ", attendues)}.";
            if (def.ValidationAvant) yield return new(AValider, numero, debut.Le, debut.Par, quoi);
            else if (etape?.FaitLe is DateTime fait) yield return new(AValider, numero, fait, etape.FaitPar, quoi);
        }

        // Réalisation : l'étape est à faire par la fonction ; avec validations préalables, seulement une fois toutes données.
        if (!realise || etape?.FaitLe is not null) yield break;
        if (def.ValidationAvant)
        {
            if (etape?.Validee == true && action is { } a) yield return new(ATour, numero, a.Le, a.Par, $"{def.Label} est validée : à vous de la réaliser.");
        }
        else yield return new(ATour, numero, debut.Le, debut.Par, rouverte ? $"{def.Label} a été rouverte : à vous de la reprendre." : $"{def.Label} : c'est à vous.");
    }
}
