using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.Invoices;

internal static class InvoiceMapper
{
    public static InvoiceResponse ToResponse(Invoice invoice, string childName, string locationName)
    {
        var lineItems = InvoiceLineItems.FromJson(invoice.LineItems);
        var isOverdue = invoice.Status == InvoiceStatus.Sent
            && invoice.DueDate is { } dueDate
            && dueDate < DateOnly.FromDateTime(DateTime.UtcNow);

        return new InvoiceResponse(
            invoice.Id,
            invoice.ChildId,
            childName,
            invoice.ContractId,
            invoice.LocationId,
            locationName,
            invoice.PeriodMonth,
            invoice.Status.ToString().ToLowerInvariant(),
            isOverdue,
            invoice.SubtotalCents,
            invoice.TotalCents,
            new InvoiceLineItemsResponse(
                lineItems.PresentDays,
                lineItems.UnjustifiedAbsentDays,
                lineItems.DailyRateCents,
                lineItems.ClosureDaysExcluded,
                lineItems.DaysMin5u,
                lineItems.DaysMin11u,
                lineItems.ExtraCharges.Select(c => new InvoiceExtraChargeResponse(c.Label, c.AmountCents)).ToList()),
            invoice.OgmReference,
            invoice.DueDate,
            invoice.SentAt,
            invoice.PaidAt,
            invoice.CreatedAt,
            invoice.UpdatedAt);
    }
}
