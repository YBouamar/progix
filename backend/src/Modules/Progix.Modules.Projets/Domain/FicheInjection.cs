using Progix.SharedKernel;

namespace Progix.Modules.Projets.Domain;

/// <summary>Rapport d'injection d'une dalle, enregistrement ENR-PT-04.</summary>
public class FicheInjection : Entity
{
    public Guid DalleId { get; set; }
    public decimal? AirAmbiant { get; set; }
    public string? Malaxeur { get; set; }
    public string? Pompe { get; set; }

    // Composition du coulis pour une gâchée
    public string? CimentType { get; set; } = "CPJ 45";
    public decimal? CimentKg { get; set; } = 50;
    public decimal? CimentTemperature { get; set; } = 20;
    public string? Eau { get; set; } = "Potable";
    public decimal? EauLitres { get; set; } = 16.5m;
    public string? AdjuvantType { get; set; } = "Krono 400 M";
    public decimal? AdjuvantGrammes { get; set; } = 450;

    public List<CableInjection> Cables { get; set; } = [];

    // Signatures : le client signe au doigt sur la tablette, l'exécutant avec son mot de passe à la validation.
    public string? ClientNom { get; set; }
    public string? ClientSignature { get; set; }
    public string? SignePar { get; set; }
    public DateTime? SigneLe { get; set; }
}

/// <summary>Composition du coulis proposée sur une nouvelle fiche d'injection, réglable dans le Paramétrage. Une seule ligne.</summary>
public class ParametresCoulis
{
    public int Id { get; set; } = 1;
    public string? CimentType { get; set; } = "CPJ 45";
    public decimal? CimentKg { get; set; } = 50;
    public decimal? CimentTemperature { get; set; } = 20;
    public string? Eau { get; set; } = "Potable";
    public decimal? EauLitres { get; set; } = 16.5m;
    public string? AdjuvantType { get; set; } = "Krono 400 M";
    public decimal? AdjuvantGrammes { get; set; } = 450;
    public string? ModifiePar { get; set; }
    public DateTime? ModifieLe { get; set; }
}

public class CableInjection
{
    public string Cable { get; set; } = "";
    public bool TestAir { get; set; }
    public decimal? Fluidite { get; set; }
    public decimal? Temperature { get; set; }
    public decimal? Pression { get; set; }
    public string? Depart { get; set; }
    public string? Fin { get; set; }

    public bool Complet => TestAir && Fluidite != null && Temperature != null && Pression != null
                           && !string.IsNullOrWhiteSpace(Depart) && !string.IsNullOrWhiteSpace(Fin);
}
