using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.Children;

internal static class ChildMapper
{
    // includeIdentityVerification (spec.md FR-015, research.md R8): false for Staff/device-token
    // callers reading the shared DeviceOrStaffOrDirector routes — verification/NRN fields are
    // compliance-audit data no caregiver workflow needs, so they're nulled out entirely rather
    // than exposed. Defaults true so every DirectorOnly-group call site (create, update,
    // deactivate, reactivate, verify, set-nrn) needs no change.
    public static ChildResponse ToResponse(Child c, string? photoDownloadUrl, bool includeIdentityVerification = true) => new(
        c.Id,
        c.FirstName,
        c.LastName,
        c.DateOfBirth,
        photoDownloadUrl,
        c.Gender?.ToString(),
        c.Nationality,
        c.AllergiesDescription,
        c.AllergySeverity?.ToString(),
        c.MedicalConditions,
        c.DietaryRestrictions,
        c.PediatricianName,
        c.PediatricianPhone,
        c.HealthInsuranceNumber,
        c.Kindcode,
        c.DeactivatedAt,
        c.CreatedAt,
        c.UpdatedAt,
        includeIdentityVerification ? c.IdVerifiedAt : null,
        includeIdentityVerification ? c.IdVerifiedByEmail : null,
        includeIdentityVerification ? c.IdDocumentType?.ToWireString() : null,
        includeIdentityVerification ? c.IdDocumentNote : null,
        includeIdentityVerification ? c.FirstIdVerifiedAt : null,
        includeIdentityVerification ? c.FirstIdVerifiedByEmail : null,
        includeIdentityVerification ? c.NrnLast4 : null);
}
