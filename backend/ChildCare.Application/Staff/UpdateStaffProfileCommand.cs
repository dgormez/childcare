using ChildCare.Domain.Enums;
using MediatR;

namespace ChildCare.Application.Staff;

public record UpdateStaffProfileCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string Phone,
    QualificationLevel? QualificationLevel) : IRequest<StaffResult>;
