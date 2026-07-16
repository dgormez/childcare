using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.FiscalAttestations;

// Feature 015 — spec.md FR-001/FR-003/FR-005/FR-009/FR-010. Bulk-generates an attestation per
// (child, location) with at least one Paid invoice in the tax year, skipping pairs that already
// have a row (FR-009 — only the explicit regenerate command, RegenerateFiscalAttestationCommand,
// overwrites an existing one) and isolating a single pair's failure from the rest of the batch
// (FR-010) — mirrors GenerateInvoicesCommand's per-item-loop shape (014), scaled from
// "one location/month" to "one organisation/tax year".
public record GenerateFiscalAttestationsCommand(int TaxYear) : IRequest<GenerateFiscalAttestationsResponse>;

public class GenerateFiscalAttestationsCommandValidator : AbstractValidator<GenerateFiscalAttestationsCommand>
{
    public GenerateFiscalAttestationsCommandValidator()
    {
        RuleFor(x => x.TaxYear).InclusiveBetween(2000, 2100);
    }
}

public class GenerateFiscalAttestationsCommandHandler(
    ITenantDbContext db,
    FiscalAttestationWriter writer,
    FiscalAttestationNotificationService notificationService,
    ILogger<GenerateFiscalAttestationsCommandHandler> logger)
    : IRequestHandler<GenerateFiscalAttestationsCommand, GenerateFiscalAttestationsResponse>
{
    public async Task<GenerateFiscalAttestationsResponse> Handle(GenerateFiscalAttestationsCommand request, CancellationToken cancellationToken)
    {
        var yearStart = new DateOnly(request.TaxYear, 1, 1);
        var yearEnd = new DateOnly(request.TaxYear, 12, 31);

        var eligiblePairs = await db.Invoices
            .Where(i => i.Status == InvoiceStatus.Paid && i.PeriodMonth >= yearStart && i.PeriodMonth <= yearEnd)
            .Select(i => new { i.ChildId, i.LocationId })
            .Distinct()
            .ToListAsync(cancellationToken);

        var existingPairs = (await db.FiscalAttestations
            .Where(fa => fa.TaxYear == request.TaxYear)
            .Select(fa => new { fa.ChildId, fa.LocationId })
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var results = new List<GenerateFiscalAttestationsResultItem>();

        foreach (var pair in eligiblePairs)
        {
            if (existingPairs.Contains(new { pair.ChildId, pair.LocationId }))
            {
                results.Add(new GenerateFiscalAttestationsResultItem(pair.ChildId, pair.LocationId, "alreadyExists"));
                continue;
            }

            try
            {
                var writeResult = await writer.WriteAsync(pair.ChildId, pair.LocationId, request.TaxYear, cancellationToken);
                if (!writeResult.Succeeded)
                {
                    // Shouldn't happen — this pair came from a Paid-invoice query — but the
                    // writer's own no-paid-invoices guard is still authoritative.
                    results.Add(new GenerateFiscalAttestationsResultItem(pair.ChildId, pair.LocationId, "noPaidInvoices"));
                    continue;
                }

                await notificationService.NotifyAsync(writeResult.Attestation!, cancellationToken);
                results.Add(new GenerateFiscalAttestationsResultItem(pair.ChildId, pair.LocationId, "generated"));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fiscal attestation generation failed for child {ChildId}, location {LocationId}, tax year {TaxYear}.",
                    pair.ChildId, pair.LocationId, request.TaxYear);
                results.Add(new GenerateFiscalAttestationsResultItem(pair.ChildId, pair.LocationId, "failed"));
            }
        }

        return new GenerateFiscalAttestationsResponse(request.TaxYear, results);
    }
}
