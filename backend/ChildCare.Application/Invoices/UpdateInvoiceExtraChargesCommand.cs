using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Invoices;

// Feature 014 — spec.md FR-006. Replaces the whole extra-charges array (mirrors 013j's
// MenuVariantSettingsForm-sends-the-full-desired-list precedent) — draft-only.
public record UpdateInvoiceExtraChargesCommand(Guid InvoiceId, IReadOnlyList<InvoiceExtraCharge> ExtraCharges) : IRequest<UpdateInvoiceExtraChargesResult>;

public enum UpdateInvoiceExtraChargesFailure { NotFound, NotDraft }

public class UpdateInvoiceExtraChargesResult
{
    public InvoiceResponse? Response { get; private init; }
    public UpdateInvoiceExtraChargesFailure? Failure { get; private init; }
    public bool Succeeded => Failure is null;

    public static UpdateInvoiceExtraChargesResult Success(InvoiceResponse response) => new() { Response = response };
    public static UpdateInvoiceExtraChargesResult Fail(UpdateInvoiceExtraChargesFailure failure) => new() { Failure = failure };
}

public class UpdateInvoiceExtraChargesCommandValidator : AbstractValidator<UpdateInvoiceExtraChargesCommand>
{
    public UpdateInvoiceExtraChargesCommandValidator()
    {
        RuleForEach(x => x.ExtraCharges).ChildRules(charge =>
        {
            charge.RuleFor(c => c.Label).NotEmpty().MaximumLength(200);
            // FR-006/checklist CHK003/CHK004 — additive-only, never zero or negative.
            charge.RuleFor(c => c.AmountCents).GreaterThan(0);
        });
    }
}

public class UpdateInvoiceExtraChargesCommandHandler(ITenantDbContext db)
    : IRequestHandler<UpdateInvoiceExtraChargesCommand, UpdateInvoiceExtraChargesResult>
{
    public async Task<UpdateInvoiceExtraChargesResult> Handle(UpdateInvoiceExtraChargesCommand request, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);
        if (invoice is null)
            return UpdateInvoiceExtraChargesResult.Fail(UpdateInvoiceExtraChargesFailure.NotFound);
        if (invoice.Status != InvoiceStatus.Draft)
            return UpdateInvoiceExtraChargesResult.Fail(UpdateInvoiceExtraChargesFailure.NotDraft);

        var current = InvoiceLineItems.FromJson(invoice.LineItems);
        var updated = current with { ExtraCharges = request.ExtraCharges };
        invoice.LineItems = updated.ToJson();
        invoice.TotalCents = updated.TotalCents;
        invoice.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var child = await db.Children.FirstAsync(c => c.Id == invoice.ChildId, cancellationToken);
        var location = await db.Locations.FirstAsync(l => l.Id == invoice.LocationId, cancellationToken);
        return UpdateInvoiceExtraChargesResult.Success(InvoiceMapper.ToResponse(invoice, $"{child.FirstName} {child.LastName}", location.Name));
    }
}
