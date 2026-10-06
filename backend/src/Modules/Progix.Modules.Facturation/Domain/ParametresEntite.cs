using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Domain;

/// <summary>Identité légale d'une entité émettrice, reprise sur ses documents. Une ligne par entité.</summary>
public class ParametresEntite : Entity
{
    public string Code { get; set; } = "";
    public string RaisonSociale { get; set; } = "";
    public string? FormeJuridique { get; set; }
    public decimal? Capital { get; set; }
    public string? Siege { get; set; }
    public string? Ville { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public string? Ice { get; set; }
    public string? IdentifiantFiscal { get; set; }
    public string? Rc { get; set; }
    public string? TribunalRc { get; set; }
    public string? Patente { get; set; }
    public string? Cnss { get; set; }
    public string? Banque { get; set; }
    public string? Rib { get; set; }

    // Logo : absent tant qu'il n'a pas été déposé depuis le paramétrage.
    public byte[]? Logo { get; set; }
    public string? LogoType { get; set; }

    public string? ModifiePar { get; set; }

    /// <summary>Valeurs de test, à remplacer dans le paramétrage.</summary>
    public static ParametresEntite[] Defauts() =>
    [
        new()
        {
            Code = Entites.Apt, RaisonSociale = "AFRICA PT", FormeJuridique = "SARL", Capital = 1_000_000m, Siege = "123, boulevard de test, quartier Maârif", Ville = "Casablanca",
            Telephone = "05 22 00 00 01", Email = "contact@africapt.test", Ice = "000000000000001", IdentifiantFiscal = "10000001", Rc = "100001", TribunalRc = "Casablanca",
            Patente = "30000001", Cnss = "1000001", Banque = "Banque de test", Rib = "000000000000000000000001",
        },
        new()
        {
            Code = Entites.Bbr, RaisonSociale = "BECOMAR", FormeJuridique = "SARL", Capital = 500_000m, Siege = "45, zone industrielle de test", Ville = "Casablanca",
            Telephone = "05 22 00 00 02", Email = "contact@becomar.test", Ice = "000000000000002", IdentifiantFiscal = "10000002", Rc = "100002", TribunalRc = "Casablanca",
            Patente = "30000002", Cnss = "1000002", Banque = "Banque de test", Rib = "000000000000000000000002",
        },
    ];
}
