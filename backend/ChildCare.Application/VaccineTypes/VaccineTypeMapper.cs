using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.VaccineTypes;

internal static class VaccineTypeMapper
{
    public static VaccineTypeResponse ToResponse(VaccineType v) => new(
        v.Id,
        v.Name,
        v.Category?.ToWireString(),
        v.SortOrder);
}
