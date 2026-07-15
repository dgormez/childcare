using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.MonthlyMenus;

// Clears PublishedAt back to null, reverting to a director-only draft for corrections (FR-004).
// NotFound (→ 404) if no menu row exists (contracts/monthly-menu-api.md). Variant added feature
// 013j: null = the base menu (unchanged); a real value is rejected unless currently enabled for
// the location (FR-006), same as Upsert/Publish.
public record UnpublishMonthlyMenuCommand(Guid LocationId, int Year, int Month, DietaryType? Variant = null) : IRequest<MonthlyMenuPublishResult>;

public class UnpublishMonthlyMenuCommandHandler(ITenantDbContext db) : IRequestHandler<UnpublishMonthlyMenuCommand, MonthlyMenuPublishResult>
{
    public async Task<MonthlyMenuPublishResult> Handle(UnpublishMonthlyMenuCommand request, CancellationToken cancellationToken)
    {
        if (request.Variant is not null && !await MonthlyMenuVariantHelper.IsEnabledAsync(db, request.LocationId, request.Variant.Value, cancellationToken))
            return MonthlyMenuPublishResult.Fail(MonthlyMenuFailure.VariantNotEnabled);

        var variantWire = MonthlyMenuVariantHelper.ToStorageWire(request.Variant);
        var menu = await db.MonthlyMenus.FirstOrDefaultAsync(
            m => m.LocationId == request.LocationId && m.Year == request.Year && m.Month == request.Month && m.Variant == variantWire,
            cancellationToken);
        if (menu is null)
            return MonthlyMenuPublishResult.Fail(MonthlyMenuFailure.NotFound);

        menu.PublishedAt = null;
        await db.SaveChangesAsync(cancellationToken);

        return MonthlyMenuPublishResult.Success(new MonthlyMenuPublishStateResponse(false, null));
    }
}
