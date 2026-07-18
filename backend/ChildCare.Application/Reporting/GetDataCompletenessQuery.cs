using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Reporting;

/// <summary>
/// FR-011: four data-completeness checks, each reading a field that already exists and already
/// has a clear meaning (research.md R7) — no speculative "critical data" definition invented for
/// this feature. Staff document/dossier gaps are out of scope (feature 028 doesn't exist yet;
/// spec.md Assumptions).
/// </summary>
public record GetDataCompletenessQuery(Guid? LocationId) : IRequest<DataCompletenessResponse>;

public class GetDataCompletenessQueryHandler(ITenantDbContext db)
    : IRequestHandler<GetDataCompletenessQuery, DataCompletenessResponse>
{
    public async Task<DataCompletenessResponse> Handle(GetDataCompletenessQuery request, CancellationToken cancellationToken)
    {
        var today = BelgianCalendarDay.Today();

        var locationsQuery = db.Locations.Where(l => l.DeactivatedAt == null);
        if (request.LocationId is not null)
            locationsQuery = locationsQuery.Where(l => l.Id == request.LocationId);
        var locationIds = await locationsQuery.Select(l => l.Id).ToListAsync(cancellationToken);

        // Children currently attending at least one of the scoped locations (via any attendance
        // record ever, not just today — a data-completeness gap doesn't stop mattering overnight).
        var childIds = await db.AttendanceRecords
            .Where(r => locationIds.Contains(r.LocationId))
            .Select(r => r.ChildId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var children = childIds.Count == 0
            ? []
            : await db.Children.Where(c => childIds.Contains(c.Id) && c.DeactivatedAt == null).ToListAsync(cancellationToken);

        var childContacts = childIds.Count == 0
            ? []
            : await db.ChildContacts.Where(cc => childIds.Contains(cc.ChildId) && cc.CanPickup).ToListAsync(cancellationToken);
        var childIdsWithPickup = childContacts.Select(cc => cc.ChildId).ToHashSet();

        var flags = new List<DataCompletenessFlagResponse>();

        foreach (var child in children.Where(c => !childIdsWithPickup.Contains(c.Id)))
        {
            flags.Add(new DataCompletenessFlagResponse(
                "missing_pickup_contact", "child", child.Id, $"{child.FirstName} {child.LastName}", null));
        }

        var vaccineRecords = childIds.Count == 0
            ? []
            : await db.VaccineRecords
                .Where(v => childIds.Contains(v.ChildId) && v.DeletedAt == null && v.NextDueDate != null)
                .ToListAsync(cancellationToken);

        var overdueByChild = vaccineRecords
            .Where(v => v.NextDueDate < today)
            .GroupBy(v => v.ChildId)
            .Select(g => g.OrderByDescending(v => v.AdministeredOn).First());

        foreach (var record in overdueByChild)
        {
            // Skip if a newer record for the same vaccine already has a later/no due date.
            var hasNewerRecord = vaccineRecords.Any(v =>
                v.ChildId == record.ChildId
                && v.Id != record.Id
                && ((v.VaccineTypeId is not null && v.VaccineTypeId == record.VaccineTypeId)
                    || (v.CustomVaccineEntryId is not null && v.CustomVaccineEntryId == record.CustomVaccineEntryId))
                && v.AdministeredOn > record.AdministeredOn
                && (v.NextDueDate is null || v.NextDueDate >= today));
            if (hasNewerRecord)
                continue;

            var child = children.FirstOrDefault(c => c.Id == record.ChildId);
            if (child is null)
                continue;

            flags.Add(new DataCompletenessFlagResponse(
                "overdue_vaccine", "child", child.Id, $"{child.FirstName} {child.LastName}",
                $"{record.VaccineName} (due {record.NextDueDate:yyyy-MM-dd})"));
        }

        var staffProfiles = await db.StaffProfiles
            .Where(sp => sp.DeactivatedAt == null)
            .Join(db.Users, sp => sp.TenantUserId, u => u.Id, (sp, u) => new { Staff = sp, u.Role })
            .Where(x => db.StaffLocationEligibility.Any(e => e.StaffProfileId == x.Staff.Id && locationIds.Contains(e.LocationId)))
            .ToListAsync(cancellationToken);

        foreach (var entry in staffProfiles.Where(x => x.Role == UserRole.Staff && x.Staff.QualificationLevel is null))
        {
            flags.Add(new DataCompletenessFlagResponse(
                "missing_qualification", "staff", entry.Staff.Id, $"{entry.Staff.FirstName} {entry.Staff.LastName}", null));
        }

        foreach (var entry in staffProfiles.Where(x => x.Staff.PinHash is null))
        {
            flags.Add(new DataCompletenessFlagResponse(
                "missing_pin", "staff", entry.Staff.Id, $"{entry.Staff.FirstName} {entry.Staff.LastName}", null));
        }

        return new DataCompletenessResponse(flags);
    }
}
