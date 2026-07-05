namespace ChildCare.Contracts.Responses;

public record AuthSessionResponse(string AccessToken, string RefreshToken, AuthenticatedUser User);

public record AuthenticatedUser(Guid Id, string Email, bool EmailVerified, string Role);
