using System.Collections.Concurrent;
using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

/// <summary>
/// Niveau proposé pour une dalle, paramétrable : libellé libre (2ème sous-sol, Rez-de-chaussée, Mezzanine, Terrasse...) et rang d'affichage.
/// La dalle garde la valeur du niveau, qui ne change jamais ; un niveau utilisé ne se supprime pas, il se désactive.
/// </summary>
public class NiveauDalle : Entity
{
    /// <summary>Code stable porté par les dalles. Les niveaux d'origine gardent leur nombre (−1 = 1er sous-sol, 0 = rez-de-chaussée).</summary>
    public int Valeur { get; set; }
    public string Libelle { get; set; } = "";
    public int Ordre { get; set; }
    public bool Actif { get; set; } = true;

    /// <summary>Niveaux créés au premier démarrage : ceux proposés jusqu'ici, du 5ème sous-sol au 40ème étage.</summary>
    public const int DefautMin = -5;
    public const int DefautMax = 40;
}

/// <summary>
/// Libellés des niveaux en mémoire, pour les nombreux affichages d'emplacement (fiches, logistique, tableaux de bord).
/// Chargés au démarrage du module et rechargés à chaque enregistrement du paramétrage : l'API tourne en un seul processus.
/// </summary>
public static class NiveauxDalle
{
    private static readonly ConcurrentDictionary<int, string> Libelles = new();

    public static string Libelle(int valeur) => Libelles.TryGetValue(valeur, out var l) ? l : Dalle.NiveauParDefaut(valeur);

    public static void Charger(IEnumerable<NiveauDalle> niveaux)
    {
        var liste = niveaux.ToList();
        foreach (var n in liste) Libelles[n.Valeur] = n.Libelle;
        foreach (var v in Libelles.Keys.Except(liste.Select(n => n.Valeur))) Libelles.TryRemove(v, out _);
    }
}
