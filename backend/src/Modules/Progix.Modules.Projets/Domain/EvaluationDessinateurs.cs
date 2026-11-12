namespace Progix.Modules.Projets.Domain;

/// <summary>Réglages de l'évaluation des dessinateurs (feuille « Évaluation » du classeur d'origine). Une seule ligne.</summary>
public class ParametresEvaluation
{
    public int Id { get; set; } = 1;

    /// <summary>Jours travaillés par mois : les jours travaillés sont les mois de présence multipliés par ce nombre.</summary>
    public decimal JoursParMois { get; set; } = 23;

    /// <summary>Jours par mois de référence : le score de la médiane vaut 1,00 quand les jours par mois égalent cette valeur.</summary>
    public decimal JoursReference { get; set; } = 23;
    public decimal SeuilExcellent { get; set; } = 1.25m;
    public decimal SeuilBon { get; set; } = 0.9m;
    public decimal SeuilMoyen { get; set; } = 0.6m;

    /// <summary>Mois de présence en dessous desquels une personne n'est ni classée ni notée (« Trop récente »).</summary>
    public decimal MoisMinimum { get; set; } = 2;
    public decimal PoidsSurface { get; set; } = 0.8m;
    public decimal PoidsDossiers { get; set; } = 0.2m;

    /// <summary>Objectif normal par personne et par jour : surface en m² et nombre de dossiers.</summary>
    public decimal ObjectifSurface { get; set; } = 400;
    public decimal ObjectifDossiers { get; set; } = 1;
    public string? ModifiePar { get; set; }
    public DateTime? ModifieLe { get; set; }

    /// <summary>Motif de refus des réglages saisis, ou null.</summary>
    public string? Refus() =>
        JoursParMois is <= 0 or > 31 || JoursReference is <= 0 or > 31 ? "Les jours par mois sont compris entre 1 et 31."
        : !(SeuilExcellent > SeuilBon && SeuilBon > SeuilMoyen && SeuilMoyen > 0) ? "Les seuils se suivent : Excellent au-dessus de Bon, Bon au-dessus de Moyen, Moyen au-dessus de 0."
        : MoisMinimum < 0 ? "Le nombre de mois minimum ne peut pas être négatif."
        : PoidsSurface < 0 || PoidsDossiers < 0 || PoidsSurface + PoidsDossiers != 1 ? "Les deux poids sont positifs et leur somme fait 1."
        : ObjectifSurface < 0 || ObjectifDossiers < 0 ? "Les objectifs ne peuvent pas être négatifs."
        : null;
}

/// <summary>
/// Évaluation des dessinateurs par leur rendement par jour travaillé, comme dans le classeur d'origine :
/// dossiers EXE et MODIF terminés et leurs surfaces, rapportés aux jours travaillés (mois de présence × jours par mois),
/// puis un score comparé à la médiane des personnes en cours, un rang et un niveau.
/// </summary>
public static class EvaluationDessinateurs
{
    public const string Excellent = "Excellent";
    public const string Bon = "Bon";
    public const string Moyen = "Moyen";
    public const string ARenforcer = "À renforcer";
    public const string TropRecente = "Trop récente";
    public static readonly string[] Niveaux = [Excellent, Bon, Moyen, ARenforcer, TropRecente];

    /// <param name="Mois">Mois de présence sur l'année, ou null si la date d'arrivée n'est pas renseignée.</param>
    public record Personne(Guid Id, string Nom, bool Partie, DateOnly? Arrivee, DateOnly? Depart, decimal? Mois,
        decimal NbModif, decimal SurfaceModif, decimal NbExe, decimal SurfaceExe);

    public record Ligne(Personne P, decimal? Jours, decimal? ModifParJour, decimal? ExeParJour, decimal? SurfaceModifParJour, decimal? SurfaceExeParJour,
        decimal? DossiersParJour, decimal? SurfaceParJour, decimal? SurfaceParDossier, decimal? Score, int? Rang, string? Niveau);

    public record Resultat(decimal? MedianeDossiers, decimal? MedianeSurface, IReadOnlyList<Ligne> EnCours, IReadOnlyList<Ligne> Parties);

    /// <summary>
    /// Mois de présence dans l'année, au dixième : de l'arrivée (ou du 1er janvier) au départ (ou à aujourd'hui, ou au 31 décembre), jours extrêmes compris.
    /// Les mois entiers comptent pour 1, le reste au prorata des jours du mois entamé. Sans date d'arrivée, pas de calcul.
    /// </summary>
    public static decimal? Mois(DateOnly? arrivee, DateOnly? depart, int annee, DateOnly aujourdhui)
    {
        if (arrivee is not DateOnly a) return null;
        var debut = a > new DateOnly(annee, 1, 1) ? a : new DateOnly(annee, 1, 1);
        var fin = new DateOnly(annee, 12, 31);
        if (aujourdhui < fin) fin = aujourdhui;
        if (depart is DateOnly d && d < fin) fin = d;
        if (fin < debut) return 0;
        var apres = fin.AddDays(1);
        var entiers = 0;
        while (debut.AddMonths(entiers + 1) <= apres) entiers++;
        var entame = debut.AddMonths(entiers);
        var reste = (decimal)(apres.DayNumber - entame.DayNumber) / DateTime.DaysInMonth(entame.Year, entame.Month);
        return Math.Round(entiers + reste, 1, MidpointRounding.AwayFromZero);
    }

    public static decimal? Mediane(IEnumerable<decimal?> valeurs)
    {
        var v = valeurs.Where(x => x is not null).Select(x => x!.Value).OrderBy(x => x).ToList();
        return v.Count == 0 ? null : v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2;
    }

    public static string Niveau(decimal score, decimal mois, ParametresEvaluation p) =>
        mois < p.MoisMinimum ? TropRecente : score >= p.SeuilExcellent ? Excellent : score >= p.SeuilBon ? Bon : score >= p.SeuilMoyen ? Moyen : ARenforcer;

    /// <summary>
    /// Score = (poids surface × m² par jour ÷ médiane + poids dossiers × dossiers par jour ÷ médiane) × jours de référence ÷ jours par mois.
    /// Les médianes sont celles des personnes en cours ; les personnes parties leur sont comparées, et classées entre elles.
    /// Rang : 1 pour le meilleur score, parmi les personnes présentes depuis le minimum de mois.
    /// </summary>
    public static Resultat Calculer(IEnumerable<Personne> personnes, ParametresEvaluation p)
    {
        Ligne Rendement(Personne x)
        {
            if (x.Mois is not > 0) return new(x, null, null, null, null, null, null, null, SurfaceParDossier(x), null, null, null);
            var jours = x.Mois.Value * p.JoursParMois;
            return new(x, jours, x.NbModif / jours, x.NbExe / jours, x.SurfaceModif / jours, x.SurfaceExe / jours,
                (x.NbModif + x.NbExe) / jours, (x.SurfaceModif + x.SurfaceExe) / jours, SurfaceParDossier(x), null, null, null);
        }
        var toutes = personnes.Select(Rendement).ToList();
        var enCours = toutes.Where(l => !l.P.Partie).ToList();
        var medDossiers = Mediane(enCours.Select(l => l.DossiersParJour));
        var medSurface = Mediane(enCours.Select(l => l.SurfaceParJour));

        List<Ligne> Classer(List<Ligne> groupe)
        {
            var notees = groupe.Select(l => l.DossiersParJour is null || medDossiers is not > 0 || medSurface is not > 0 ? l : l with
            {
                Score = (p.PoidsSurface * l.SurfaceParJour!.Value / medSurface.Value + p.PoidsDossiers * l.DossiersParJour.Value / medDossiers.Value) * p.JoursReference / p.JoursParMois,
            }).ToList();
            bool Classee(Ligne l) => l.Score is not null && l.P.Mois >= p.MoisMinimum;
            return notees.Select(l => l.Score is null ? l : l with
            {
                Rang = Classee(l) ? 1 + notees.Count(o => Classee(o) && o.Score > l.Score) : null,
                Niveau = Niveau(l.Score.Value, l.P.Mois!.Value, p),
            })
            // Classement : les personnes classées par score, puis les trop récentes, puis celles qu'on ne peut pas calculer.
            .OrderBy(l => l.Score is null ? 2 : l.Rang is null ? 1 : 0).ThenByDescending(l => l.Score).ThenBy(l => l.P.Nom).ToList();
        }
        return new(medDossiers, medSurface, Classer(enCours), Classer(toutes.Where(l => l.P.Partie).ToList()));
    }

    private static decimal? SurfaceParDossier(Personne x) => x.NbModif + x.NbExe == 0 ? null : (x.SurfaceModif + x.SurfaceExe) / (x.NbModif + x.NbExe);
}
