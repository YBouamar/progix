using System.Net;
using Hangfire;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using Progix.Modules.Identity.Domain;
using Progix.SharedKernel;

namespace Progix.Api.Notifications;

/// <summary>Réglages du serveur d'envoi (section « Smtp »). Sans hôte, aucun courriel ne part et rien n'échoue.</summary>
public class SmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? Password { get; set; }
    /// <summary>Adresse d'expédition, par exemple « PROGIX &lt;progix@votre-domaine.ma&gt; ».</summary>
    public string? From { get; set; }
    /// <summary>« starttls » (port 587, par défaut), « ssl » (port 465) ou « aucun ».</summary>
    public string Securite { get; set; } = "starttls";
}

/// <summary>Prépare le courriel et le confie à la file des tâches : il part même si l'action est déjà terminée, avec reprise en cas d'échec.</summary>
public class NotificationsDirection(UserManager<AppUser> users, IBackgroundJobClient jobs, IConfiguration config) : INotificationsDirection
{
    public async Task SignalerAsync(string sujet, string titre, IReadOnlyList<KeyValuePair<string, string>> lignes, string? lien, CancellationToken ct = default)
    {
        var directeurs = (await users.GetUsersInRoleAsync(Roles.Directeur))
            .Where(u => u.IsActive && !string.IsNullOrWhiteSpace(u.Email)).Select(u => u.Email!).Distinct().ToList();
        if (directeurs.Count == 0) return;
        var url = string.IsNullOrWhiteSpace(lien) ? null : (config["App:PublicUrl"] ?? "").TrimEnd('/') + lien;
        jobs.Enqueue<EnvoiCourriel>(e => e.EnvoyerAsync(directeurs, "PROGIX · " + sujet, Html(titre, lignes, url), Texte(titre, lignes, url)));
    }

    private static string E(string s) => WebUtility.HtmlEncode(s);

    private static string Html(string titre, IReadOnlyList<KeyValuePair<string, string>> lignes, string? url) =>
        $"""
        <div style="font-family:Arial,sans-serif;font-size:14px;color:#161a2e;max-width:560px">
          <div style="font-size:12px;font-weight:bold;letter-spacing:.08em;color:#1b2a6b">PROGIX</div>
          <h2 style="font-size:18px;margin:8px 0 14px">{E(titre)}</h2>
          <table style="border-collapse:collapse;width:100%">
            {string.Join("", lignes.Select(l => $"<tr><td style=\"padding:6px 12px 6px 0;color:#5b6178;white-space:nowrap;vertical-align:top\">{E(l.Key)}</td><td style=\"padding:6px 0;font-weight:bold\">{E(l.Value)}</td></tr>"))}
          </table>
          {(url is null ? "" : $"<p style=\"margin:18px 0\"><a href=\"{E(url)}\" style=\"background:#1b2a6b;color:#fff;text-decoration:none;padding:10px 16px;border-radius:8px;display:inline-block\">Ouvrir dans PROGIX</a></p>")}
          <p style="font-size:12px;color:#8a90a6">Message automatique envoyé aux directeurs. Merci de ne pas y répondre.</p>
        </div>
        """;

    private static string Texte(string titre, IReadOnlyList<KeyValuePair<string, string>> lignes, string? url) =>
        titre + "\n\n" + string.Join("\n", lignes.Select(l => $"{l.Key} : {l.Value}")) + (url is null ? "" : $"\n\nOuvrir dans PROGIX : {url}") + "\n";
}

/// <summary>Envoi effectif, exécuté par la file des tâches.</summary>
public class EnvoiCourriel(IConfiguration config, ILogger<EnvoiCourriel> logger)
{
    [AutomaticRetry(Attempts = 5)]
    public async Task EnvoyerAsync(List<string> destinataires, string sujet, string html, string texte)
    {
        var o = config.GetSection("Smtp").Get<SmtpOptions>() ?? new SmtpOptions();
        if (string.IsNullOrWhiteSpace(o.Host) || string.IsNullOrWhiteSpace(o.From))
        {
            logger.LogInformation("Courriel « {Sujet} » non envoyé : la messagerie (Smtp) n'est pas réglée.", sujet);
            return;
        }
        var message = new MimeMessage { Subject = sujet };
        message.From.Add(MailboxAddress.Parse(o.From));
        foreach (var d in destinataires) message.To.Add(MailboxAddress.Parse(d));
        message.Body = new BodyBuilder { HtmlBody = html, TextBody = texte }.ToMessageBody();

        using var client = new SmtpClient();
        var securite = o.Securite.ToLowerInvariant() switch { "ssl" => SecureSocketOptions.SslOnConnect, "aucun" => SecureSocketOptions.None, _ => SecureSocketOptions.StartTls };
        await client.ConnectAsync(o.Host, o.Port, securite);
        if (!string.IsNullOrWhiteSpace(o.User)) await client.AuthenticateAsync(o.User, o.Password ?? "");
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
        logger.LogInformation("Courriel « {Sujet} » envoyé à {Nombre} directeur(s).", sujet, destinataires.Count);
    }
}
