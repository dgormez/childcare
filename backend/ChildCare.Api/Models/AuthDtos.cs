namespace ChildCare.Api.Models;

public record AuthResponse(string AccessToken, string RefreshToken, UserDto User);

public record UserDto(Guid Id, string Email, bool EmailVerified);
