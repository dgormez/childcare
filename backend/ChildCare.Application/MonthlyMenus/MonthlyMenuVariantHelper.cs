using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.MonthlyMenus;

/// <summary>
/// Shared between GetLocationByIdQuery (settings-read display) and
/// UpdateLocationMenuVariantSettingsCommand (FR-014's removal-warning check) — both need the
/// same "which enabled variants currently have a published menu for the current or a future
/// month" answer, computed the same way so the two can never disagree.
/// </summary>
public static class MonthlyMenuVariantHelper
{
    public static async Task<IReadOnlyList<string>> GetVariantsWithPublishedContentAsync(
        ITenantDbContext db, Guid locationId, IReadOnlyList<DietaryType> enabledVariants, CancellationToken cancellationToken)
    {
        if (enabledVariants.Count == 0)
            return [];

        var today = BelgianCalendarDay.Today();

        var published = await db.MonthlyMenus
            .Where(m => m.LocationId == locationId
                && m.Variant != null
                && enabledVariants.Contains(m.Variant.Value)
                && m.PublishedAt != null
                && (m.Year > today.Year || (m.Year == today.Year && m.Month >= today.Month)))
            .Select(m => m.Variant!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        return published.Select(v => v.ToWireString()).ToList();
    }

    // FR-006 — shared by Upsert/Publish/Unpublish's rejection of a variant not currently
    // enabled for the location, including via a direct API call after later disabling it.
    public static async Task<bool> IsEnabledAsync(ITenantDbContext db, Guid locationId, DietaryType variant, CancellationToken cancellationToken)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == locationId, cancellationToken);
        return location is not null && location.MenuVariantPriorityOrder.Contains(variant);
    }
}
