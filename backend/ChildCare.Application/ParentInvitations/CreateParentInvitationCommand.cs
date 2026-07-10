using MediatR;

namespace ChildCare.Application.ParentInvitations;

public record CreateParentInvitationCommand(Guid ContactId) : IRequest<ParentInvitationResult>;
