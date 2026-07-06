using ChildCare.Domain.Enums;
using MediatR;

namespace ChildCare.Application.Staff;

public record CreateStaffProfileCommand(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    QualificationLevel? QualificationLevel,
    UserRole Role,
    Guid? ExistingTenantUserId) : IRequest<StaffResult>;
