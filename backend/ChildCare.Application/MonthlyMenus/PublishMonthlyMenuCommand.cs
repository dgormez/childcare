using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.MonthlyMenus;

// Sets PublishedAt = NOW(), making the menu parent-visible (FR-003). NotFound (→ 404) if no menu
// row exists yet — the director must save at least a draft first (contracts/monthly-menu-api.md).
public record PublishMonthlyMenuCommand(Guid LocationId, int Year, int Month) : IRequest<MonthlyMenuPublishResult>;

public class PublishMonthlyMenuCommandHandler(ITenantDbContext db) : IRequestHandler<PublishMonthlyMenuCommand, MonthlyMenuPublishResult>
{
    public async Task<MonthlyMenuPublishResult> Handle(PublishMonthlyMenuCommand request, CancellationToken cancellationToken)
    {
        var menu = await db.MonthlyMenus.FirstOrDefaultAsync(
            m => m.LocationId == request.LocationId && m.Year == request.Year && m.Month == request.Month,
            cancellationToken);
        if (menu is null)
            return MonthlyMenuPublishResult.Fail(MonthlyMenuFailure.NotFound);

        menu.PublishedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return MonthlyMenuPublishResult.Success(new MonthlyMenuPublishStateResponse(true, menu.PublishedAt));
    }
}
