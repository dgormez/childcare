using ChildCare.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Locations;

// Feature 014 — contracts/014-invoicing/invoicing-api.md. Mirrors
// UpdateLocationReservationSettingsCommand's shape, minus its warning-confirm flow (no
// analogous "pending requests" concern for these fields).
public record UpdateLocationInvoiceSettingsCommand(
    Guid LocationId,
    string? Erkenningsnummer,
    string? BankAccountNumber,
    int InvoiceDueDays) : IRequest<LocationResult>;

public class UpdateLocationInvoiceSettingsCommandValidator : AbstractValidator<UpdateLocationInvoiceSettingsCommand>
{
    public UpdateLocationInvoiceSettingsCommandValidator()
    {
        RuleFor(x => x.InvoiceDueDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Erkenningsnummer).MaximumLength(50);
        RuleFor(x => x.BankAccountNumber).MaximumLength(50);
    }
}

public class UpdateLocationInvoiceSettingsCommandHandler(ITenantDbContext db)
    : IRequestHandler<UpdateLocationInvoiceSettingsCommand, LocationResult>
{
    public async Task<LocationResult> Handle(UpdateLocationInvoiceSettingsCommand request, CancellationToken cancellationToken)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == request.LocationId, cancellationToken);
        if (location is null)
            return LocationResult.Fail(LocationFailure.NotFound);

        location.Erkenningsnummer = request.Erkenningsnummer;
        location.BankAccountNumber = request.BankAccountNumber;
        location.InvoiceDueDays = request.InvoiceDueDays;
        location.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return LocationResult.Success(LocationMapper.ToResponse(location));
    }
}
