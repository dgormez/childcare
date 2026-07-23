using ChildCare.Domain.Enums;
using MediatR;

namespace ChildCare.Application.Staff;

public record UpdateStaffProfileCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string Phone,
    QualificationLevel? QualificationLevel,
    // Feature 027 (FR-002) — null leaves ContractedDays unchanged.
    IReadOnlyList<DayOfWeek>? ContractedDays = null) : IRequest<StaffResult>;
