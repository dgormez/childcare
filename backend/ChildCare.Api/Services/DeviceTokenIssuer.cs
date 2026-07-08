using ChildCare.Application.Common;

namespace ChildCare.Api.Services;

/// <summary>Adapts DeviceTokenService to the IDeviceTokenIssuer port (mirrors JwtAccessTokenIssuer).</summary>
public class DeviceTokenIssuer(DeviceTokenService deviceTokenService) : IDeviceTokenIssuer
{
    public string IssueDeviceToken(Guid tenantId, Guid deviceId, Guid locationId, Guid groupId, int tokenVersion)
        => deviceTokenService.GenerateDeviceToken(tenantId, deviceId, locationId, groupId, tokenVersion);
}
