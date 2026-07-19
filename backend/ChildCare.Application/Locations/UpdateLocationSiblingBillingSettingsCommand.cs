using ChildCare.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Locations;

// Feature 030 — contracts/family-siblings-api.md. Mirrors UpdateLocationInvoiceSettingsCommand's
// shape exactly (spec.md FR-004/FR-007) — both fields default to no-op (0%/disabled) so a
// location that never configures this sees zero invoice-generation behavior change (SC-005).
public record UpdateLocationSiblingBillingSettingsCommand(
    Guid LocationId,
    decimal SiblingDiscountPct,
    bool FamilyInvoiceBundlingEnabled) : IRequest<LocationResult>;

public class UpdateLocationSiblingBillingSettingsCommandValidator : AbstractValidator<UpdateLocationSiblingBillingSettingsCommand>
{
    public UpdateLocationSiblingBillingSettingsCommandValidator()
    {
        RuleFor(x => x.SiblingDiscountPct).InclusiveBetween(0, 100);
    }
}

public class UpdateLocationSiblingBillingSettingsCommandHandler(ITenantDbContext db)
    : IRequestHandler<UpdateLocationSiblingBillingSettingsCommand, LocationResult>
{
    public async Task<LocationResult> Handle(UpdateLocationSiblingBillingSettingsCommand request, CancellationToken cancellationToken)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == request.LocationId, cancellationToken);
        if (location is null)
            return LocationResult.Fail(LocationFailure.NotFound);

        location.SiblingDiscountPct = request.SiblingDiscountPct;
        location.FamilyInvoiceBundlingEnabled = request.FamilyInvoiceBundlingEnabled;
        location.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return LocationResult.Success(LocationMapper.ToResponse(location));
    }
}
