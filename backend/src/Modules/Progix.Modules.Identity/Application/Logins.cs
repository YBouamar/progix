using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Progix.Modules.Identity.Domain;

namespace Progix.Modules.Identity.Application;

/// <summary>Identifiant de connexion : prénom.nom, en minuscules et sans accent. Il ne change plus après la création du compte.</summary>
public static class Logins
{
    public static string Partie(string? texte)
    {
        var sb = new StringBuilder();
        foreach (var c in (texte ?? "").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && c is (>= 'a' and <= 'z') or (>= '0' and <= '9')) sb.Append(c);
        return sb.ToString();
    }

    public static string Base(string? prenom, string? nom) => $"{Partie(prenom)}.{Partie(nom)}";

    /// <summary>Premier identifiant libre : prénom.nom, puis prénom.nom2, prénom.nom3 en cas d'homonyme.</summary>
    public static async Task<string> LibreAsync(UserManager<AppUser> users, string racine)
    {
        var login = racine;
        for (var n = 2; await users.FindByNameAsync(login) is not null; n++) login = $"{racine}{n}";
        return login;
    }
}
