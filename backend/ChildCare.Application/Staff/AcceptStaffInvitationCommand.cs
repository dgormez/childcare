using MediatR;

namespace ChildCare.Application.Staff;

public record AcceptStaffInvitationCommand(
    string OrganisationSlug,
    string Token,
    string Password) : IRequest<StaffResult>;
