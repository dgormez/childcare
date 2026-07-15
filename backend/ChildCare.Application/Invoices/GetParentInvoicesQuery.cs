using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Invoices;

// Feature 014 — spec.md FR-008/FR-010/FR-015. Every sent/paid invoice (draft never included)
// for every child the requesting contact is linked to — mirrors GetParentMonthlyMenuQuery's
// per-contact-then-per-child resolution (013j) and its "every linked contact sees the same
// children's data" precedent (spec.md Assumptions).
public record GetParentInvoicesQuery(Guid TenantUserId) : IRequest<GetParentInvoicesResult>;

public class GetParentInvoicesResult
{
    public bool Authorized { get; private init; }
    public List<InvoiceResponse>? Invoices { get; private init; }

    public static GetParentInvoicesResult Ok(List<InvoiceResponse> invoices) => new() { Authorized = true, Invoices = invoices };
    public static GetParentInvoicesResult Forbidden() => new() { Authorized = false };
}

public class GetParentInvoicesQueryHandler(ITenantDbContext db, ICurrentParentContactResolver contactResolver)
    : IRequestHandler<GetParentInvoicesQuery, GetParentInvoicesResult>
{
    public async Task<GetParentInvoicesResult> Handle(GetParentInvoicesQuery request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return GetParentInvoicesResult.Forbidden();

        var childIds = await db.ChildContacts
            .Where(cc => cc.ContactId == contact.Id)
            .Select(cc => cc.ChildId)
            .ToListAsync(cancellationToken);

        if (childIds.Count == 0)
            return GetParentInvoicesResult.Ok([]);

        var invoices = await db.Invoices
            .Where(i => childIds.Contains(i.ChildId) && i.Status != InvoiceStatus.Draft)
            .ToListAsync(cancellationToken);

        if (invoices.Count == 0)
            return GetParentInvoicesResult.Ok([]);

        var children = await db.Children.Where(c => childIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);
        var locationIds = invoices.Select(i => i.LocationId).Distinct().ToList();
        var locations = await db.Locations.Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, cancellationToken);

        var responses = invoices
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => InvoiceMapper.ToResponse(i, $"{children[i.ChildId].FirstName} {children[i.ChildId].LastName}", locations[i.LocationId].Name))
            .ToList();

        return GetParentInvoicesResult.Ok(responses);
    }
}
