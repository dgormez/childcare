using ChildCare.Domain.Enums;

namespace ChildCare.Application.Auth;

/// <summary>
/// FR-017's per-role sign-in method table, enforced server-side regardless of which client
/// made the request: web admin (Director) is password + Google; caregiver app (Staff) is
/// password only; parent app (Parent) is password + Google + Apple. Password itself is
/// universal, so only the OAuth methods need a role check.
/// </summary>
internal static class AuthMethodPolicy
{
    public static bool GoogleAllowedFor(UserRole role) => role is UserRole.Director or UserRole.Parent;

    public static bool AppleAllowedFor(UserRole role) => role is UserRole.Parent;
}
