using ChildCare.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Locations;

// Feature 014a — contracts/014a-invoice-payments-plus/payments-api.md. Mirrors
// UpdateLocationInvoiceSettingsCommand's exact shape (014).
public record UpdateLocationPaymentReminderSettingsCommand(
    Guid LocationId,
    bool Enabled,
    int DelayDays,
    int CadenceDays) : IRequest<LocationResult>;

public class UpdateLocationPaymentReminderSettingsCommandValidator : AbstractValidator<UpdateLocationPaymentReminderSettingsCommand>
{
    public UpdateLocationPaymentReminderSettingsCommandValidator()
    {
        RuleFor(x => x.DelayDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CadenceDays).GreaterThanOrEqualTo(1);
    }
}

public class UpdateLocationPaymentReminderSettingsCommandHandler(ITenantDbContext db)
    : IRequestHandler<UpdateLocationPaymentReminderSettingsCommand, LocationResult>
{
    public async Task<LocationResult> Handle(UpdateLocationPaymentReminderSettingsCommand request, CancellationToken cancellationToken)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == request.LocationId, cancellationToken);
        if (location is null)
            return LocationResult.Fail(LocationFailure.NotFound);

        location.PaymentRemindersEnabled = request.Enabled;
        location.PaymentReminderDelayDays = request.DelayDays;
        location.PaymentReminderCadenceDays = request.CadenceDays;
        location.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return LocationResult.Success(LocationMapper.ToResponse(location));
    }
}
