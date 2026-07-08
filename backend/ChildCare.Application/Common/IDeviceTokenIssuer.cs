namespace ChildCare.Application.Common;

/// <summary>
/// Port for minting device tokens (feature 008a, kiosk mode), mirroring IAccessTokenIssuer's
/// existing adapter pattern — implemented in ChildCare.Api by wrapping DeviceTokenService, so
/// Application/Domain never take a dependency on the concrete JWT-signing stack.
/// </summary>
public interface IDeviceTokenIssuer
{
    string IssueDeviceToken(Guid tenantId, Guid deviceId, Guid locationId, Guid groupId, int tokenVersion);
}
