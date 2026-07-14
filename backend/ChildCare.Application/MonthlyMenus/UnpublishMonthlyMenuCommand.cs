using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.MonthlyMenus;

// Clears PublishedAt back to null, reverting to a director-only draft for corrections (FR-004).
// NotFound (→ 404) if no menu row exists (contracts/monthly-menu-api.md).
public record UnpublishMonthlyMenuCommand(Guid LocationId, int Year, int Month) : IRequest<MonthlyMenuPublishResult>;

public class UnpublishMonthlyMenuCommandHandler(ITenantDbContext db) : IRequestHandler<UnpublishMonthlyMenuCommand, MonthlyMenuPublishResult>
{
    public async Task<MonthlyMenuPublishResult> Handle(UnpublishMonthlyMenuCommand request, CancellationToken cancellationToken)
    {
        var menu = await db.MonthlyMenus.FirstOrDefaultAsync(
            m => m.LocationId == request.LocationId && m.Year == request.Year && m.Month == request.Month,
            cancellationToken);
        if (menu is null)
            return MonthlyMenuPublishResult.Fail(MonthlyMenuFailure.NotFound);

        menu.PublishedAt = null;
        await db.SaveChangesAsync(cancellationToken);

        return MonthlyMenuPublishResult.Success(new MonthlyMenuPublishStateResponse(false, null));
    }
}
