namespace ChildCare.Contracts.Responses;

public record AuthSessionResponse(string AccessToken, string RefreshToken, AuthenticatedUser User);

// IsPlatformAdmin (feature 013h): the web app never decodes the JWT client-side (every existing
// screen gates purely on session presence, since the entire director-web app is already
// DirectorOnly-enforced backend-side) — this field is the one place platform-admin status is
// exposed to the frontend, mirroring how Role already works, so the sidebar can conditionally
// show the platform-admin nav entry without adding client-side token parsing.
public record AuthenticatedUser(Guid Id, string Email, bool EmailVerified, string Role, string Name, bool IsPlatformAdmin);
