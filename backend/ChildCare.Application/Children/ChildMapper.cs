using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;

namespace ChildCare.Application.Children;

internal static class ChildMapper
{
    public static ChildResponse ToResponse(Child c, string? photoDownloadUrl) => new(
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
        c.GpName,
        c.GpPhone,
        c.PediatricianName,
        c.PediatricianPhone,
        c.HealthInsuranceNumber,
        c.Kindcode,
        c.DeactivatedAt,
        c.CreatedAt,
        c.UpdatedAt);
}
