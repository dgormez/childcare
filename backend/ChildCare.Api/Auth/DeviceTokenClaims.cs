namespace ChildCare.Api.Auth;

/// <summary>
/// Shared claim-name constants for the device-token scheme (feature 008a, research.md R1).
/// A device token carries the same <c>tenant_id</c> claim name as the user-JWT scheme so
/// TenantMiddleware needs zero code changes regardless of which scheme authenticated the
/// request — see Program.cs's policy-scheme forwarder.
/// </summary>
public static class DeviceTokenClaims
{
    public const string TenantId = "tenant_id";
    public const string DeviceId = "device_id";
    public const string LocationId = "location_id";
    public const string GroupId = "group_id";
    public const string TokenVersion = "token_version";
}
