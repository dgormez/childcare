using MediatR;

namespace ChildCare.Application.ParentInvitations;

public record AcceptParentInvitationCommand(string OrganisationSlug, string Token, string Password) : IRequest<AcceptParentInvitationResult>;
