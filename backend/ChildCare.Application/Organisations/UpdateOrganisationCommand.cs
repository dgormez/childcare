using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Organisations;

// Feature 014 — contracts/014-invoicing/invoicing-api.md. Org-wide, not per-location (KboNumber
// lives on Tenant, public schema — see GetCurrentOrganisationQuery's existing read).
public record UpdateOrganisationCommand(string? KboNumber) : IRequest<OrganisationResponse>;

public class UpdateOrganisationCommandValidator : AbstractValidator<UpdateOrganisationCommand>
{
    public UpdateOrganisationCommandValidator()
    {
        RuleFor(x => x.KboNumber).MaximumLength(20);
    }
}

public class UpdateOrganisationCommandHandler(IPublicDbContext publicDb, ICurrentTenantService currentTenant)
    : IRequestHandler<UpdateOrganisationCommand, OrganisationResponse>
{
    public async Task<OrganisationResponse> Handle(UpdateOrganisationCommand request, CancellationToken cancellationToken)
    {
        var tenant = await publicDb.Tenants.FirstAsync(t => t.Id == currentTenant.TenantId, cancellationToken);
        tenant.KboNumber = request.KboNumber;
        await publicDb.SaveChangesAsync(cancellationToken);

        return new OrganisationResponse(tenant.Name, tenant.KboNumber);
    }
}
