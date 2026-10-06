using Progix.SharedKernel;

namespace Progix.Modules.Facturation.Domain;

/// <summary>Signature électronique d'une fiche de chantier (auto-contrôle, mise en tension, injection) : une par dalle et par type de fiche.</summary>
public class SignatureFiche : Entity
{
    public Guid DalleId { get; set; }
    public string Type { get; set; } = "";
    public Guid ProjetId { get; set; }
    public string Reference { get; set; } = "";
    public string Titre { get; set; } = "";
    public string SignePar { get; set; } = "";
    public DateTime SigneLe { get; set; }
    public string Jeton { get; set; } = "";
}
