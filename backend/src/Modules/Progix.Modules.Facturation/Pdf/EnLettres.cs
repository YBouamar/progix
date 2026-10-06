namespace Progix.Modules.Facturation.Pdf;

/// <summary>Montant en toutes lettres, en français : « cent vingt mille dirhams et cinquante centimes ».</summary>
public static class EnLettres
{
    private static readonly string[] Unites =
    [
        "zéro", "un", "deux", "trois", "quatre", "cinq", "six", "sept", "huit", "neuf", "dix", "onze", "douze", "treize", "quatorze", "quinze", "seize",
        "dix-sept", "dix-huit", "dix-neuf",
    ];
    private static readonly string[] Dizaines = ["", "dix", "vingt", "trente", "quarante", "cinquante", "soixante", "soixante", "quatre-vingt", "quatre-vingt"];

    public static string Montant(decimal montant)
    {
        montant = Math.Round(Math.Abs(montant), 2);
        var entier = (long)Math.Truncate(montant);
        var centimes = (int)((montant - entier) * 100);
        var texte = $"{Nombre(entier)} dirham{(entier > 1 ? "s" : "")}";
        // « un million de dirhams » : de s'intercale quand le nombre finit par million(s) ou milliard(s).
        if (entier >= 1_000_000 && entier % 1_000_000 == 0) texte = $"{Nombre(entier)} de dirhams";
        if (centimes > 0) texte += $" et {Nombre(centimes)} centime{(centimes > 1 ? "s" : "")}";
        return char.ToUpper(texte[0]) + texte[1..];
    }

    public static string Nombre(long n)
    {
        if (n == 0) return Unites[0];
        var parties = new List<string>();
        void Tranche(long valeur, string nom, bool invariable)
        {
            if (valeur == 0) return;
            if (invariable) parties.Add(valeur == 1 ? nom : $"{Centaines((int)valeur, false)} {nom}");
            else parties.Add($"{Centaines((int)valeur, false)} {nom}{(valeur > 1 ? "s" : "")}");
        }
        Tranche(n / 1_000_000_000, "milliard", false);
        Tranche(n / 1_000_000 % 1000, "million", false);
        Tranche(n / 1000 % 1000, "mille", true);
        var reste = (int)(n % 1000);
        if (reste > 0) parties.Add(Centaines(reste, true));
        return string.Join(" ", parties);
    }

    /// <param name="final">Vrai en fin de nombre : « cents » et « quatre-vingts » ne prennent leur s que là.</param>
    private static string Centaines(int n, bool final)
    {
        var c = n / 100; var r = n % 100;
        var texte = c == 0 ? "" : c == 1 ? "cent" : $"{Unites[c]} cent{(r == 0 && final ? "s" : "")}";
        if (r == 0) return texte;
        return (texte.Length > 0 ? texte + " " : "") + Moins100(r, final);
    }

    private static string Moins100(int n, bool final)
    {
        if (n < 20) return Unites[n];
        var d = n / 10; var u = n % 10;
        if (d is 7 or 9)
        {
            // soixante-dix, soixante et onze, quatre-vingt-dix, quatre-vingt-onze
            var suite = Unites[10 + u];
            return d == 7 && u == 1 ? "soixante et onze" : $"{Dizaines[d]}-{suite}";
        }
        if (u == 0) return d == 8 ? (final ? "quatre-vingts" : "quatre-vingt") : Dizaines[d];
        if (u == 1 && d != 8) return $"{Dizaines[d]} et un";
        return $"{Dizaines[d]}-{Unites[u]}";
    }
}
