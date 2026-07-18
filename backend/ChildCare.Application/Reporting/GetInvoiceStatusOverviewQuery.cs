using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Reporting;

/// <summary>
/// FR-009/FR-010: current-month invoice status overview, reusing the existing `Invoice.cs`
/// convention exactly — "overdue" is `Status == Sent && DueDate &lt; today`, never a separately
/// stored value (research.md R6).
/// </summary>
public record GetInvoiceStatusOverviewQuery(Guid? LocationId) : IRequest<InvoiceStatusOverviewResponse>;

public class GetInvoiceStatusOverviewQueryHandler(ITenantDbContext db)
    : IRequestHandler<GetInvoiceStatusOverviewQuery, InvoiceStatusOverviewResponse>
{
    public async Task<InvoiceStatusOverviewResponse> Handle(GetInvoiceStatusOverviewQuery request, CancellationToken cancellationToken)
    {
        var today = BelgianCalendarDay.Today();
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var locationsQuery = db.Locations.Where(l => l.DeactivatedAt == null);
        if (request.LocationId is not null)
            locationsQuery = locationsQuery.Where(l => l.Id == request.LocationId);
        var locationIds = await locationsQuery.Select(l => l.Id).ToListAsync(cancellationToken);

        var invoices = await db.Invoices
            .Where(i => locationIds.Contains(i.LocationId) && i.PeriodMonth == monthStart)
            .ToListAsync(cancellationToken);

        var paid = invoices.Where(i => i.Status == InvoiceStatus.Paid).ToList();
        var overdue = invoices.Where(i => i.Status == InvoiceStatus.Sent && i.DueDate is not null && i.DueDate < today).ToList();
        var outstanding = invoices.Where(i => i.Status == InvoiceStatus.Sent && !overdue.Contains(i)).ToList();

        var childIds = overdue.Select(i => i.ChildId).Distinct().ToList();
        var children = childIds.Count == 0
            ? []
            : await db.Children.Where(c => childIds.Contains(c.Id)).ToListAsync(cancellationToken);
        var childNames = children.ToDictionary(c => c.Id, c => $"{c.FirstName} {c.LastName}");

        var overdueInvoices = overdue.Select(i => new OverdueInvoiceResponse(
            i.Id,
            childNames.GetValueOrDefault(i.ChildId, string.Empty),
            i.DueDate!.Value,
            ReportingMapper.ComputeDaysOverdue(i.DueDate.Value, today),
            i.TotalCents)).ToList();

        return new InvoiceStatusOverviewResponse(
            monthStart,
            paid.Count,
            paid.Sum(i => i.TotalCents),
            outstanding.Count,
            outstanding.Sum(i => i.TotalCents),
            overdue.Count,
            overdue.Sum(i => i.TotalCents),
            paid.Sum(i => i.TotalCents) + outstanding.Sum(i => i.TotalCents) + overdue.Sum(i => i.TotalCents),
            overdueInvoices);
    }
}
