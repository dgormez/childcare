using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.MonthlyMenus;

/// <summary>
/// Shared between GetLocationByIdQuery (settings-read display) and
/// UpdateLocationMenuVariantSettingsCommand (FR-014's removal-warning check) — both need the
/// same "which enabled variants currently have a published menu for the current or a future
/// month" answer, computed the same way so the two can never disagree. Also owns the
/// DietaryType? <-> MonthlyMenu.Variant "base"-sentinel-string conversion (research.md) so every
/// command/query does it identically.
/// </summary>
public static class MonthlyMenuVariantHelper
{
    public const string BaseSentinel = "base";

    public static string ToStorageWire(DietaryType? variant) => variant?.ToWireString() ?? BaseSentinel;

    public static DietaryType? FromStorageWire(string wire) =>
        wire != BaseSentinel && DietaryTypeExtensions.TryParseWireString(wire, out var parsed) ? parsed : null;

    public static async Task<IReadOnlyList<string>> GetVariantsWithPublishedContentAsync(
        ITenantDbContext db, Guid locationId, IReadOnlyList<string> enabledVariants, CancellationToken cancellationToken)
    {
        if (enabledVariants.Count == 0)
            return [];

        var today = BelgianCalendarDay.Today();

        // enabledVariants (Location.MenuVariantPriorityOrder) and MonthlyMenu.Variant are both
        // plain wire strings now — a direct string comparison, no DietaryType parsing needed.
        return await db.MonthlyMenus
            .Where(m => m.LocationId == locationId
                && m.Variant != BaseSentinel
                && enabledVariants.Contains(m.Variant)
                && m.PublishedAt != null
                && (m.Year > today.Year || (m.Year == today.Year && m.Month >= today.Month)))
            .Select(m => m.Variant)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    // FR-006 — shared by Upsert/Publish/Unpublish's rejection of a variant not currently
    // enabled for the location, including via a direct API call after later disabling it.
    public static async Task<bool> IsEnabledAsync(ITenantDbContext db, Guid locationId, DietaryType variant, CancellationToken cancellationToken)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == locationId, cancellationToken);
        return location is not null && location.MenuVariantPriorityOrder.Contains(variant.ToWireString());
    }
}
