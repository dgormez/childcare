using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.DevelopmentalMilestones;

// Mirrors GetParentDailySummaryQuery's exact ChildContact ownership-check pattern (feature 009).
// History is stripped from the response (data-model.md's Derived View note) — parents see
// current status + current-focus only, not the full observation-by-observation history.
public record GetParentMilestonePortfolioQuery(Guid TenantUserId, Guid ChildId) : IRequest<ParentMilestonePortfolioResult>;

public class ParentMilestonePortfolioResult
{
    public bool Authorized { get; private init; }
    public bool DateOfBirthMissing { get; private init; }
    public MilestonePortfolioResponse? Response { get; private init; }

    public static ParentMilestonePortfolioResult Ok(MilestonePortfolioResponse response) => new() { Authorized = true, Response = response };
    public static ParentMilestonePortfolioResult Forbidden() => new() { Authorized = false };
    public static ParentMilestonePortfolioResult MissingDateOfBirth() => new() { Authorized = true, DateOfBirthMissing = true };
}

public class GetParentMilestonePortfolioQueryHandler(
    ITenantDbContext db,
    ICurrentParentContactResolver contactResolver,
    IMediator mediator) : IRequestHandler<GetParentMilestonePortfolioQuery, ParentMilestonePortfolioResult>
{
    public async Task<ParentMilestonePortfolioResult> Handle(GetParentMilestonePortfolioQuery request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return ParentMilestonePortfolioResult.Forbidden();

        var isContactOfChild = await db.ChildContacts
            .AnyAsync(cc => cc.ContactId == contact.Id && cc.ChildId == request.ChildId, cancellationToken);
        if (!isContactOfChild)
            return ParentMilestonePortfolioResult.Forbidden();

        var full = await mediator.Send(new GetChildMilestonePortfolioQuery(request.ChildId), cancellationToken);
        if (full.DateOfBirthMissing)
            return ParentMilestonePortfolioResult.MissingDateOfBirth();
        if (full.Response is null)
            return ParentMilestonePortfolioResult.Forbidden();

        var withoutHistory = new MilestonePortfolioResponse(
            full.Response.ChildId,
            full.Response.Domains.Select(d => d with
            {
                Milestones = d.Milestones.Select(m => m with { History = null }).ToList()
            }).ToList());

        return ParentMilestonePortfolioResult.Ok(withoutHistory);
    }
}
