namespace Progix.Modules.Identity.Application;

public record LoginRequest(string Login, string Password);

/// <param name="Role">Fonction de référence : l'une des douze fonctions d'origine, celle qui porte les validations et les signatures.</param>
/// <param name="RoleLabel">Nom de la fonction de la personne, tel qu'il s'affiche.</param>
/// <param name="Fonction">Code de la fonction de la personne : une fonction créée, ou l'une des douze d'origine.</param>
/// <param name="Droits">Niveau d'accès par module : 0 aucun, 1 lecture, 2 écriture.</param>
public record UserDto(Guid Id, string FullName, string Email, string Role, string RoleLabel, bool VoitMontants, bool IsActive, string? PhoneNumber, DateTime? LastLoginAt, string Login = "",
    string Fonction = "", Dictionary<string, int>? Droits = null);

public record CreateUserRequest(string Prenom, string Nom, string Email, string Role, string Password);

public record UpdateUserRequest(string FullName, string Email, string Role, bool IsActive);

public record ResetPasswordRequest(string NewPassword);

public record UpdateProfileRequest(string FullName, string? PhoneNumber);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record RoleDto(string Code, string Label, bool VoitMontants, string Reference, bool Origine);
