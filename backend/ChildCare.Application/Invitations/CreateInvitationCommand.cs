using ChildCare.Contracts.Responses;
using MediatR;

namespace ChildCare.Application.Invitations;

public record CreateInvitationCommand(string Email) : IRequest<CreateInvitationResponse>;
