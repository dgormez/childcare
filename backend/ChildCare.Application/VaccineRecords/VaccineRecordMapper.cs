using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;

namespace ChildCare.Application.VaccineRecords;

internal static class VaccineRecordMapper
{
    public static VaccineRecordResponse ToResponse(VaccineRecord v) => new(
        v.Id,
        v.ChildId,
        v.VaccineName,
        v.DoseNumber,
        v.AdministeredOn,
        v.NextDueDate,
        v.AdministeredBy,
        v.Notes,
        v.RecordedBy,
        v.CreatedAt,
        v.UpdatedAt);
}
