using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.MonthlyMenus;

// Director authoring read (contracts/monthly-menu-api.md). Returns an exists:false shell when no
// menu row exists yet for this location/year/month, so the web form can render blank inputs
// without a 404. Draft or published — the director always sees the menu regardless of state.
public record GetMonthlyMenuQuery(Guid LocationId, int Year, int Month) : IRequest<MonthlyMenuResponse>;

public class GetMonthlyMenuQueryHandler(ITenantDbContext db) : IRequestHandler<GetMonthlyMenuQuery, MonthlyMenuResponse>
{
    public async Task<MonthlyMenuResponse> Handle(GetMonthlyMenuQuery request, CancellationToken cancellationToken)
    {
        var menu = await db.MonthlyMenus
            .Include(m => m.Days)
            .FirstOrDefaultAsync(
                m => m.LocationId == request.LocationId && m.Year == request.Year && m.Month == request.Month,
                cancellationToken);

        return menu is null
            ? MonthlyMenuMapper.EmptyShell()
            : MonthlyMenuMapper.ToResponse(menu);
    }
}
