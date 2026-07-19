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
    // Feature 030 (US3) — each entry is either an InvoiceResponse (ungrouped, FamilyGroupId
    // null) or a FamilyInvoiceResponse (siblings sharing a FamilyGroupId collapsed into one
    // entry, contracts/family-siblings-api.md) — no OpenAPI response schema is declared for this
    // route (see apiClient.ts's doc comment), so the mixed shape is a frontend-only concern.
    public List<object>? Invoices { get; private init; }

    public static GetParentInvoicesResult Ok(List<object> invoices) => new() { Authorized = true, Invoices = invoices };
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

        // Feature 030 (US3) — invoices sharing a FamilyGroupId collapse into one
        // FamilyInvoiceResponse entry; every other invoice stays its own InvoiceResponse entry,
        // identical to pre-030 behavior (contracts/family-siblings-api.md).
        var ungrouped = invoices.Where(i => i.FamilyGroupId is null);
        var grouped = invoices.Where(i => i.FamilyGroupId is not null).GroupBy(i => i.FamilyGroupId!.Value);

        var responses = new List<(object Entry, DateTime SortKey)>();

        foreach (var invoice in ungrouped)
        {
            responses.Add((
                InvoiceMapper.ToResponse(invoice, $"{children[invoice.ChildId].FirstName} {children[invoice.ChildId].LastName}", locations[invoice.LocationId].Name),
                invoice.CreatedAt));
        }

        foreach (var group in grouped)
        {
            var groupInvoices = group.ToList();
            var isOverdue = groupInvoices.Any(i => i.Status == InvoiceStatus.Sent && i.DueDate is { } due && due < DateOnly.FromDateTime(DateTime.UtcNow));
            responses.Add((
                new FamilyInvoiceResponse(
                    group.Key,
                    groupInvoices.Select(i => new FamilyInvoiceChildLineResponse(i.Id, i.ChildId, $"{children[i.ChildId].FirstName} {children[i.ChildId].LastName}", i.SubtotalCents)).ToList(),
                    groupInvoices.Sum(i => i.TotalCents),
                    groupInvoices[0].Status.ToString().ToLowerInvariant(),
                    isOverdue,
                    groupInvoices[0].DueDate,
                    groupInvoices.Min(i => i.CreatedAt)),
                groupInvoices.Max(i => i.CreatedAt)));
        }

        return GetParentInvoicesResult.Ok(responses.OrderByDescending(r => r.SortKey).Select(r => r.Entry).ToList());
    }
}
