using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.MonthlyMenus;

// Director authoring read (contracts/monthly-menu-api.md; Variant added feature 013j). Returns
// an exists:false shell when no menu row exists yet for this location/year/month/variant, so the
// web form can render blank inputs without a 404. Draft or published — the director always sees
// the menu regardless of state. Variant: null = base menu (unchanged 013e behavior). Unlike the
// write commands, a GET is not rejected for a disabled variant — a director may still want to
// view retained content before deciding whether to re-enable it (FR-007).
public record GetMonthlyMenuQuery(Guid LocationId, int Year, int Month, DietaryType? Variant = null) : IRequest<MonthlyMenuResponse>;

public class GetMonthlyMenuQueryHandler(ITenantDbContext db) : IRequestHandler<GetMonthlyMenuQuery, MonthlyMenuResponse>
{
    public async Task<MonthlyMenuResponse> Handle(GetMonthlyMenuQuery request, CancellationToken cancellationToken)
    {
        var variantWire = MonthlyMenuVariantHelper.ToStorageWire(request.Variant);
        var menu = await db.MonthlyMenus
            .Include(m => m.Days)
            .FirstOrDefaultAsync(
                m => m.LocationId == request.LocationId && m.Year == request.Year && m.Month == request.Month && m.Variant == variantWire,
                cancellationToken);

        return menu is null
            ? MonthlyMenuMapper.EmptyShell(request.Variant)
            : MonthlyMenuMapper.ToResponse(menu);
    }
}
