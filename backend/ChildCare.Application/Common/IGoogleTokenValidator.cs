namespace ChildCare.Application.Common;

/// <summary>
/// Port over Google ID token validation (research.md R7) — an external service client,
/// living behind this port so Application-layer commands never take a direct HTTP dependency.
/// </summary>
public interface IGoogleTokenValidator
{
    /// <summary>Returns null when the token is invalid, unverified, or its audience is not allow-listed.</summary>
    Task<GoogleIdentity?> ValidateAsync(string idToken);
}

public record GoogleIdentity(string Sub, string Email);
