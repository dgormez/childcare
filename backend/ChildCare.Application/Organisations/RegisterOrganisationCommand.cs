using ChildCare.Contracts.Responses;
using MediatR;

namespace ChildCare.Application.Organisations;

public record RegisterOrganisationCommand(
    string InvitationToken,
    string OrganisationName,
    string DirectorName,
    string Email,
    string Password) : IRequest<RegisterOrganisationResult>;
