using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Invoices;

public record GetInvoiceByIdQuery(Guid Id) : IRequest<InvoiceResponse?>;

public class GetInvoiceByIdQueryHandler(ITenantDbContext db) : IRequestHandler<GetInvoiceByIdQuery, InvoiceResponse?>
{
    public async Task<InvoiceResponse?> Handle(GetInvoiceByIdQuery request, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);
        if (invoice is null)
            return null;

        var child = await db.Children.FirstAsync(c => c.Id == invoice.ChildId, cancellationToken);
        var location = await db.Locations.FirstAsync(l => l.Id == invoice.LocationId, cancellationToken);

        return InvoiceMapper.ToResponse(invoice, $"{child.FirstName} {child.LastName}", location.Name);
    }
}
