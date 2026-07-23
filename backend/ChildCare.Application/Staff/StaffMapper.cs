using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;

namespace ChildCare.Application.Staff;

internal static class StaffMapper
{
    public static StaffResponse ToResponse(
        StaffProfile profile,
        TenantUser user,
        IReadOnlyList<Guid> eligibleLocationIds,
        string? photoDownloadUrl) => new(
        profile.Id,
        profile.TenantUserId,
        profile.FirstName,
        profile.LastName,
        user.Email,
        profile.Phone,
        user.Role.ToString(),
        profile.QualificationLevel?.ToString(),
        photoDownloadUrl,
        eligibleLocationIds,
        profile.DeactivatedAt,
        profile.CreatedAt,
        profile.UpdatedAt,
        profile.ContractedDays.Select(d => d.ToString()).ToList());
}
