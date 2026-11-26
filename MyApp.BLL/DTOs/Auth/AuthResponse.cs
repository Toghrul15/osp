namespace MyApp.BLL.DTOs.Auth;

public record AuthResponse(string AccessToken, DateTime ExpiresAt, UserDto User);

public record UserDto(string Id, string Email, string FullName, IReadOnlyList<string> Roles);
