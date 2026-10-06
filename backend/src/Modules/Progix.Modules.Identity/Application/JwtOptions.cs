namespace Progix.Modules.Identity.Application;

public class JwtOptions
{
    public const string Section = "Jwt";
    public const string CookieName = "progix_token";

    public string Issuer { get; set; } = "progix";
    public string Audience { get; set; } = "progix";
    public string Key { get; set; } = string.Empty;
    public int ExpirationHours { get; set; } = 12;
}
