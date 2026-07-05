namespace ChildCare.Application.Common;

/// <summary>
/// Port over Apple identity token (JWKS) validation (research.md R7) — an external service
/// client, living behind this port so Application-layer commands never take a direct
/// HTTP/JWKS dependency.
/// </summary>
public interface IAppleTokenValidator
{
    /// <summary>Returns null when the token's signature, issuer, audience, or lifetime is invalid.</summary>
    Task<AppleIdentity?> ValidateAsync(string identityToken, string bundleId);
}

/// <summary>Email is only present on Apple's first sign-in for a given user.</summary>
public record AppleIdentity(string Sub, string? Email);
