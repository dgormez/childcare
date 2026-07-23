using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.DevelopmentalMilestones;

// Director/caregiver read (StaffOrDirector) — full per-milestone history included, unlike the
// parent-facing query (GetParentMilestonePortfolioQuery), per data-model.md's Derived View note.
public record GetChildMilestonePortfolioQuery(Guid ChildId) : IRequest<MilestonePortfolioResult>;

public class MilestonePortfolioResult
{
    public bool ChildFound { get; private init; }
    public bool DateOfBirthMissing { get; private init; }
    public MilestonePortfolioResponse? Response { get; private init; }

    public static MilestonePortfolioResult Ok(MilestonePortfolioResponse response) => new() { ChildFound = true, Response = response };
    public static MilestonePortfolioResult NotFound() => new() { ChildFound = false };
    public static MilestonePortfolioResult MissingDateOfBirth() => new() { ChildFound = true, DateOfBirthMissing = true };
}

public class GetChildMilestonePortfolioQueryHandler(ITenantDbContext db, IPublicDbContext publicDb)
    : IRequestHandler<GetChildMilestonePortfolioQuery, MilestonePortfolioResult>
{
    public async Task<MilestonePortfolioResult> Handle(GetChildMilestonePortfolioQuery request, CancellationToken cancellationToken)
    {
        var child = await db.Children.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.ChildId, cancellationToken);
        if (child is null)
            return MilestonePortfolioResult.NotFound();

        // A child registered before birth (or before the date is known) has no age to resolve
        // milestones against yet — a distinct, expected state, not an error (spec.md 006a).
        if (child.DateOfBirth is null)
            return MilestonePortfolioResult.MissingDateOfBirth();

        var domains = await publicDb.DevelopmentalDomains.AsNoTracking().ToListAsync(cancellationToken);
        var milestones = await publicDb.DevelopmentalMilestones.AsNoTracking().ToListAsync(cancellationToken);
        var observations = await db.ChildMilestoneObservations
            .AsNoTracking()
            .Where(o => o.ChildId == request.ChildId)
            .ToListAsync(cancellationToken);

        var portfolio = MilestonePortfolioBuilder.Build(
            request.ChildId, child.DateOfBirth.Value, DateOnly.FromDateTime(DateTime.UtcNow),
            domains, milestones, observations, includeHistory: true);

        return MilestonePortfolioResult.Ok(portfolio);
    }
}
