using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.MealPreferenceRequests;

// Director review queue (FR-013). status defaults to "pending" at the endpoint layer.
public record ListMealPreferenceChangeRequestsQuery(string? Status) : IRequest<List<MealPreferenceChangeRequestResponse>>;

public class ListMealPreferenceChangeRequestsQueryHandler(ITenantDbContext db) : IRequestHandler<ListMealPreferenceChangeRequestsQuery, List<MealPreferenceChangeRequestResponse>>
{
    public async Task<List<MealPreferenceChangeRequestResponse>> Handle(ListMealPreferenceChangeRequestsQuery request, CancellationToken cancellationToken)
    {
        var query = db.MealPreferenceChangeRequests.AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<MealPreferenceChangeRequestStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(r => r.Status == status);
        }

        var requests = await query.OrderByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);
        if (requests.Count == 0)
            return [];

        var childIds = requests.Select(r => r.ChildId).Distinct().ToList();
        var children = await db.Children.Where(c => childIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);

        var today = BelgianCalendarDay.Today();
        var activeHealthRecordsByChild = await db.HealthRecords
            .Where(h => childIds.Contains(h.ChildId) && h.DeletedAt == null
                && (h.ValidFrom == null || h.ValidFrom <= today) && (h.ValidUntil == null || h.ValidUntil >= today))
            .GroupBy(h => h.ChildId)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.Select(h => new MealPreferenceRequestHealthRecordEntry(h.Id, h.RecordType.ToWireString(), h.Title, h.ValidFrom, h.ValidUntil)).ToList(),
                cancellationToken);

        var requestedByTenantUserIds = requests.Select(r => r.RequestedBy).Distinct().ToList();
        var contactsByTenantUserId = await db.Contacts
            .Where(c => c.TenantUserId != null && requestedByTenantUserIds.Contains(c.TenantUserId.Value))
            .ToDictionaryAsync(c => c.TenantUserId!.Value, cancellationToken);

        return requests.Select(r =>
        {
            var child = children.GetValueOrDefault(r.ChildId);
            var childName = child is null ? string.Empty : $"{child.FirstName} {child.LastName}";
            var contact = contactsByTenantUserId.GetValueOrDefault(r.RequestedBy);
            var requestedByName = contact is null ? string.Empty : $"{contact.FirstName} {contact.LastName}";
            var activeHealthRecords = activeHealthRecordsByChild.GetValueOrDefault(r.ChildId, []);
            return MealPreferenceRequestMapper.ToResponse(r, childName, requestedByName, activeHealthRecords);
        }).ToList();
    }
}
