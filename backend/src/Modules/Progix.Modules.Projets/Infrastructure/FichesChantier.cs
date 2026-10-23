using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Projets.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Infrastructure;

/// <summary>Contenu des fiches de chantier pour leur impression.</summary>
public class FichesChantier(ProjetsDbContext db) : IFichesChantier
{
    private static readonly CultureInfo Fr = new("fr-FR");
    private static string N(decimal? v, string format = "0.##") => v is null ? "" : v.Value.ToString(format, Fr);
    private static KeyValuePair<string, string> L(string k, string? v) => new(k, v ?? "");

    public async Task<FicheChantier?> FicheAsync(Guid dalleId, string type, CancellationToken ct = default)
    {
        var d = await db.Dalles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dalleId, ct);
        if (d is null) return null;
        var p = await db.Projets.AsNoTracking().FirstAsync(x => x.Id == d.ProjetId, ct);
        var numero = type switch { "auto-controle" => 6, "tension" => Etapes.MiseEnTension, "injection" => Etapes.Injection, _ => 0 };
        if (numero == 0) return null;
        var e = await db.Etapes.AsNoTracking().FirstOrDefaultAsync(x => x.DalleId == dalleId && x.Numero == numero, ct);
        var reference = $"{p.Code}-{d.Suffixe}";
        var emplacement = string.Join(" · ", new[] { d.Bloc, d.Immeuble, Dalle.NiveauLabel(d.Niveau) }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var roles = Etapes.Get(numero).Roles.Concat([Roles.Directeur, Roles.ChefProjet]).Distinct().ToList();
        FicheChantier Fiche(string titre, List<KeyValuePair<string, string>> entete, string[] colonnes, List<IReadOnlyList<string>> lignes, List<KeyValuePair<string, string>> synthese,
            string? clientNom = null, string? clientSignature = null) =>
            new(type, titre, d.Id, p.Id, reference, emplacement, numero, d.EtapesFaites >= numero, e?.FaitPar, e?.FaitLe, e?.Remarque, entete, colonnes, lignes, synthese, clientNom, clientSignature, roles);

        if (type == "auto-controle")
        {
            var f = await db.FichesAutoControle.AsNoTracking().FirstOrDefaultAsync(x => x.DalleId == dalleId, ct);
            var lignes = new List<IReadOnlyList<string>>();
            string section = "", rubrique = "";
            foreach (var m in FicheAutoControle.Modele)
            {
                if (m.Section != section) { lignes.Add([m.Section.ToUpperInvariant()]); section = m.Section; rubrique = ""; }
                if (m.Rubrique.Length > 0 && m.Rubrique != rubrique) { lignes.Add([m.Rubrique]); rubrique = m.Rubrique; }
                var pt = f?.Points.FirstOrDefault(x => x.Code == m.Code);
                lignes.Add([m.Libelle, pt?.Resultat == "c" ? "X" : "", pt?.Resultat == "nc" ? "X" : "", pt?.Resultat == "na" ? "X" : "", pt?.Observation ?? ""]);
            }
            var nc = f?.Points.Count(x => x.Resultat == FicheAutoControle.NonConforme) ?? 0;
            return Fiche("FICHE D'AUTO-CONTRÔLE DU PLANCHER", [L("Niveau", emplacement), L("Saisie par", f?.SaisiPar)],
                ["Points critiques", "C", "NC", "NA", "Observations"], lignes,
                [L("Points non conformes", nc.ToString()), L("Légende", "C : conforme · NC : non conforme · NA : non applicable")]);
        }

        if (type == "tension")
        {
            var mesures = await db.MesuresTension.AsNoTracking().Where(x => x.DalleId == dalleId).OrderBy(x => x.Ordre).ToListAsync(ct);
            var prm = await db.ParametresTension.AsNoTracking().FirstOrDefaultAsync(ct) ?? new ParametresTension();
            var theo = mesures.Sum(m => m.Theorique); var mes = mesures.Sum(m => m.Mesure ?? 0);
            decimal? ecart = theo > 0 ? Math.Round((mes - theo) / theo * 100, 1) : null;
            var hors = mesures.Count(m => m.Conforme == false);
            var entete = await db.FichesTension.AsNoTracking().FirstOrDefaultAsync(x => x.DalleId == dalleId, ct);
            return Fiche("FICHE DE MISE EN TENSION", [L("PH", emplacement), L("Force", $"{N(entete?.ForceKn ?? prm.ForceKn)} kN"), L("CR", entete?.Cr), L("CMP", entete?.Cmp), L("Nombre de câbles", mesures.Count.ToString())],
                ["Tendon", "Ordre", "A 25 %", "A 100 %", "Δ A", "B 25 %", "B 100 %", "Δ B", "Total", "Moyenne", $"{N(MesureTension.BorneBasse)} Ao", "Ao", $"{N(MesureTension.BorneHaute)} Ao", "Conf."],
                mesures.Select(m => (IReadOnlyList<string>)[m.Cable, (m.Ordre + 1).ToString(), N(m.A25), N(m.A100), N(m.DeltaA), N(m.B25), N(m.B100), N(m.DeltaB), N(m.Mesure), N(m.Moyenne), N(m.Min), N(m.Theorique), N(m.Max),
                    m.Conforme is null ? "" : m.Conforme.Value ? "Oui" : "Non"]).ToList(),
                [L("Allongements", "en millimètres ; Δ = lecture à 100 % moins lecture à 25 % ; total = Δ A + Δ B ; moyenne = moyenne des deux côtés"),
                 L("Groupe", $"{N(mes)} mm mesurés pour {N(theo)} mm théoriques{(ecart is null ? "" : $", écart {(ecart > 0 ? "+" : "")}{N(ecart, "0.0")} %")}"),
                 L("Règle", $"EN 13670 : câble seul entre {N(MesureTension.BorneBasse)} et {N(MesureTension.BorneHaute)} fois Ao, groupe entre {N(MesureTension.GroupeBas)} et {N(MesureTension.GroupeHaut)} fois la somme des Ao"),
                 L("Câbles hors tolérance", hors.ToString())]);
        }

        var inj = await db.FichesInjection.AsNoTracking().FirstOrDefaultAsync(x => x.DalleId == dalleId, ct) ?? new FicheInjection();
        return Fiche("RAPPORT D'INJECTION",
            [L("Localisation, niveau", emplacement), L("Air ambiant", inj.AirAmbiant is null ? "" : $"{N(inj.AirAmbiant)} °C"), L("Malaxeur", inj.Malaxeur), L("Pompe", inj.Pompe),
             L("Ciment", $"{inj.CimentType}, {N(inj.CimentKg)} kg, {N(inj.CimentTemperature)} °C"), L("Eau", $"{inj.Eau}, {N(inj.EauLitres)} L"),
             L("Adjuvant", $"{inj.AdjuvantType}, {N(inj.AdjuvantGrammes)} g")],
            ["N° du câble", "Test à l'air", "Fluidité (s)", "Température du coulis (°C)", "Pression du coulis (bar)", "Départ", "Fin"],
            inj.Cables.Select(c => (IReadOnlyList<string>)[c.Cable, c.TestAir ? "Oui" : "Non", N(c.Fluidite), N(c.Temperature), N(c.Pression), c.Depart ?? "", c.Fin ?? ""]).ToList(),
            [L("Composition", "pour une gâchée"), L("Câbles injectés", inj.Cables.Count.ToString()), L("Exécution signée par", inj.SignePar)], inj.ClientNom, inj.ClientSignature);
    }
}
