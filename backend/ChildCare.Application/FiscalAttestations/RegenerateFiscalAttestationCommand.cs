using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.FiscalAttestations;

// Feature 015 — spec.md FR-008/FR-016. Re-aggregates and replaces the existing attestation in
// place (or creates it, if none existed yet — "also serves as generate one", spec.md FR-008) —
// unlike GenerateFiscalAttestationsCommand's FR-009 skip-existing rule, this always overwrites.
public record RegenerateFiscalAttestationCommand(Guid ChildId, Guid LocationId, int TaxYear) : IRequest<RegenerateFiscalAttestationResult>;

public enum RegenerateFiscalAttestationFailure { NoPaidInvoices }

public class RegenerateFiscalAttestationResult
{
    public FiscalAttestationResponse? Response { get; private init; }
    public RegenerateFiscalAttestationFailure? Failure { get; private init; }
    public bool Succeeded => Failure is null;

    public static RegenerateFiscalAttestationResult Success(FiscalAttestationResponse response) => new() { Response = response };
    public static RegenerateFiscalAttestationResult Fail(RegenerateFiscalAttestationFailure failure) => new() { Failure = failure };
}

public class RegenerateFiscalAttestationCommandValidator : AbstractValidator<RegenerateFiscalAttestationCommand>
{
    public RegenerateFiscalAttestationCommandValidator()
    {
        RuleFor(x => x.TaxYear).InclusiveBetween(2000, 2100);
    }
}

public class RegenerateFiscalAttestationCommandHandler(
    ITenantDbContext db,
    FiscalAttestationWriter writer,
    FiscalAttestationNotificationService notificationService)
    : IRequestHandler<RegenerateFiscalAttestationCommand, RegenerateFiscalAttestationResult>
{
    public async Task<RegenerateFiscalAttestationResult> Handle(RegenerateFiscalAttestationCommand request, CancellationToken cancellationToken)
    {
        var writeResult = await writer.WriteAsync(request.ChildId, request.LocationId, request.TaxYear, cancellationToken);
        if (!writeResult.Succeeded)
            return RegenerateFiscalAttestationResult.Fail(RegenerateFiscalAttestationFailure.NoPaidInvoices);

        await notificationService.NotifyAsync(writeResult.Attestation!, cancellationToken);

        var child = await db.Children.FirstAsync(c => c.Id == request.ChildId, cancellationToken);
        var location = await db.Locations.FirstAsync(l => l.Id == request.LocationId, cancellationToken);
        return RegenerateFiscalAttestationResult.Success(
            FiscalAttestationMapper.ToResponse(writeResult.Attestation!, $"{child.FirstName} {child.LastName}", location.Name));
    }
}
