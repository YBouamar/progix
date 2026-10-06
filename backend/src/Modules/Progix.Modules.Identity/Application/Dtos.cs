namespace Progix.Modules.Identity.Application;

public record LoginRequest(string Login, string Password);

public record UserDto(Guid Id, string FullName, string Email, string Role, string RoleLabel, bool VoitMontants, bool IsActive, string? PhoneNumber, DateTime? LastLoginAt, string Login = "");

public record CreateUserRequest(string Prenom, string Nom, string Email, string Role, string Password);

public record UpdateUserRequest(string FullName, string Email, string Role, bool IsActive);

public record ResetPasswordRequest(string NewPassword);

public record UpdateProfileRequest(string FullName, string? PhoneNumber);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record RoleDto(string Code, string Label, bool VoitMontants);
