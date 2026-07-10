using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.ClosureCalendar;

public record ClosureParentRecipient(Guid ContactId, string Locale, string? PushToken);

public class ClosureParentRecipientResolver(ITenantDbContext db)
{
    public async Task<IReadOnlyList<ClosureParentRecipient>> ResolveAsync(
        Guid locationId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var childIds = await db.Contracts
            .Where(c => c.LocationId == locationId
                     && c.Status == ContractStatus.Active
                     && c.StartDate <= date
                     && (c.EndDate == null || c.EndDate >= date))
            .ToListAsync(cancellationToken);

        var contractedChildIds = childIds
            .Where(c => c.ContractedDays.Any(d => d.Weekday == date.DayOfWeek))
            .Select(c => c.ChildId)
            .Distinct()
            .ToList();

        var rows = await db.ChildContacts
            .Where(cc => contractedChildIds.Contains(cc.ChildId)
                      && (cc.Relationship == ContactRelationship.Mother
                          || cc.Relationship == ContactRelationship.Father
                          || cc.Relationship == ContactRelationship.Guardian))
            .Join(db.Contacts, cc => cc.ContactId, c => c.Id, (cc, c) => new ClosureParentRecipient(c.Id, c.Locale, c.PushToken))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.ContactId)
            .Select(g => g.First())
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> ResolveChildIdsAsync(
        Guid locationId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var contracts = await db.Contracts
            .Where(c => c.LocationId == locationId
                     && c.Status == ContractStatus.Active
                     && c.StartDate <= date
                     && (c.EndDate == null || c.EndDate >= date))
            .ToListAsync(cancellationToken);

        return contracts
            .Where(c => c.ContractedDays.Any(d => d.Weekday == date.DayOfWeek))
            .Select(c => c.ChildId)
            .Distinct()
            .ToList();
    }
}
