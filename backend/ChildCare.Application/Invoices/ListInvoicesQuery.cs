using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Invoices;

// Feature 014 — contracts/014-invoicing/invoicing-api.md. status: draft/sent/paid/overdue
// ("overdue" filters Sent rows whose DueDate has passed, research.md R4).
public record ListInvoicesQuery(Guid LocationId, int? Year, int? Month, string? Status) : IRequest<IReadOnlyList<InvoiceResponse>>;

public class ListInvoicesQueryHandler(ITenantDbContext db) : IRequestHandler<ListInvoicesQuery, IReadOnlyList<InvoiceResponse>>
{
    public async Task<IReadOnlyList<InvoiceResponse>> Handle(ListInvoicesQuery request, CancellationToken cancellationToken)
    {
        var query = db.Invoices.Where(i => i.LocationId == request.LocationId);

        if (request.Year is not null && request.Month is not null)
            query = query.Where(i => i.PeriodMonth == new DateOnly(request.Year.Value, request.Month.Value, 1));

        var invoices = await query.ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.Status is not null)
        {
            invoices = request.Status switch
            {
                "overdue" => invoices.Where(i => i.Status == InvoiceStatus.Sent && i.DueDate < today).ToList(),
                "sent" => invoices.Where(i => i.Status == InvoiceStatus.Sent && !(i.DueDate < today)).ToList(),
                "draft" => invoices.Where(i => i.Status == InvoiceStatus.Draft).ToList(),
                "paid" => invoices.Where(i => i.Status == InvoiceStatus.Paid).ToList(),
                _ => invoices,
            };
        }

        if (invoices.Count == 0)
            return [];

        var location = await db.Locations.FirstAsync(l => l.Id == request.LocationId, cancellationToken);
        var childIds = invoices.Select(i => i.ChildId).Distinct().ToList();
        var children = await db.Children.Where(c => childIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);

        return invoices
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => InvoiceMapper.ToResponse(i, $"{children[i.ChildId].FirstName} {children[i.ChildId].LastName}", location.Name))
            .ToList();
    }
}
