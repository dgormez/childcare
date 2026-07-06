namespace ChildCare.Contracts.Requests;

public record LoginRequest(string OrganisationSlug, string Email, string Password);

public record RefreshRequest(string OrganisationSlug, string RefreshToken);

public record LogoutRequest(string RefreshToken);

public record GoogleAuthRequest(string OrganisationSlug, string IdToken);

public record AppleAuthRequest(string OrganisationSlug, string IdentityToken, string? Email);

public record VerifyEmailRequest(string OrganisationSlug, string Token);

public record ForgotPasswordRequest(string OrganisationSlug, string Email);

public record ResetPasswordRequest(string OrganisationSlug, string Token, string NewPassword);
