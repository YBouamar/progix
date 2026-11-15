using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Progix.Modules.Identity.Application;
using Progix.Modules.Identity.Domain;
using Progix.SharedKernel;

namespace Progix.Modules.Identity.Endpoints;

public record CommercialInput(string? Prenom, string? Nom, string? Email);
public record CommercialMajInput(string? Nom, bool Actif);

/// <summary>
/// Commerciaux proposés sur la fiche d'un projet : les utilisateurs de fonction « agent commercial ».
/// Écrire demande le niveau « écriture » du module Commerciaux (contrôlé pour toute la route par les habilitations),
/// sans ouvrir la gestion complète des utilisateurs. Un commercial ne se supprime pas : il se désactive, et les projets gardent son nom.
/// </summary>
public static class CommerciauxEndpoints
{
    private static IResult Conflit(string message) => Results.Conflict(new { message });

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/commerciaux").WithTags("Commerciaux").RequireAuthorization();

        g.MapGet("/", async (UserManager<AppUser> users) => Results.Ok(await Liste(users)));

        // Le nouveau commercial n'a pas de mot de passe connu : il ne peut se connecter qu'après qu'un responsable des accès lui en donne un.
        g.MapPost("/", async (CommercialInput input, UserManager<AppUser> users) =>
        {
            var prenom = input.Prenom?.Trim() ?? "";
            var nom = input.Nom?.Trim() ?? "";
            var email = input.Email?.Trim() ?? "";
            var e = new Dictionary<string, string[]>();
            if (Logins.Partie(prenom).Length == 0 || Logins.Partie(nom).Length == 0) e["nom"] = ["Le prénom et le nom sont obligatoires."];
            if (email.Length == 0 || !email.Contains('@')) e["email"] = ["Adresse email invalide."];
            if (e.Count > 0) return Results.ValidationProblem(e);
            if (await users.FindByEmailAsync(email) is not null) return Conflit("Un utilisateur porte déjà cette adresse email.");
            var login = await Logins.LibreAsync(users, Logins.Base(prenom, nom));
            var u = new AppUser { UserName = login, Email = email, FullName = $"{prenom} {nom}", EmailConfirmed = true };
            var cree = await users.CreateAsync(u, MotDePasseInconnu());
            if (!cree.Succeeded) return Conflit("Création impossible : " + string.Join(" ", cree.Errors.Select(x => x.Description)));
            await users.AddToRoleAsync(u, Roles.AgentCommercial);
            return Results.Ok(await Liste(users));
        });

        g.MapPut("/{id:guid}", async (Guid id, CommercialMajInput input, UserManager<AppUser> users) =>
        {
            var u = await users.FindByIdAsync(id.ToString());
            if (u is null || !await users.IsInRoleAsync(u, Roles.AgentCommercial)) return Results.NotFound();
            var nom = input.Nom?.Trim() ?? "";
            if (nom.Length < 2 || nom.Length > 100) return Conflit("Le nom compte entre 2 et 100 caractères.");
            u.FullName = nom;
            u.IsActive = input.Actif;
            await users.UpdateAsync(u);
            return Results.Ok(await Liste(users));
        });
    }

    private static async Task<object> Liste(UserManager<AppUser> users)
    {
        var agents = await users.GetUsersInRoleAsync(Roles.AgentCommercial);
        return new { commerciaux = agents.OrderByDescending(u => u.IsActive).ThenBy(u => u.FullName).Select(u => new { id = u.Id, nom = u.FullName, actif = u.IsActive }) };
    }

    /// <summary>Mot de passe aléatoire jamais communiqué : le compte reste sans connexion possible jusqu'à une réinitialisation.</summary>
    private static string MotDePasseInconnu() => "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
}
