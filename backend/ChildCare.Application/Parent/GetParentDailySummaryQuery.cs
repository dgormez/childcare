using ChildCare.Application.ChildEvents;
using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Parent;

public record GetParentDailySummaryQuery(Guid TenantUserId, Guid ChildId, DateOnly Date) : IRequest<ParentDailySummaryResult>;

public class ParentDailySummaryResult
{
    public bool Authorized { get; init; }
    public DailySummaryResponse? Response { get; init; }

    public static ParentDailySummaryResult Ok(DailySummaryResponse response) => new() { Authorized = true, Response = response };
    public static ParentDailySummaryResult Forbidden() => new() { Authorized = false };
}

/// <summary>
/// Authorizes that the caller's linked Contact is actually a contact of ChildId (FR-006/FR-017),
/// then delegates to the existing GetDailySummaryQuery (feature 009) — reused, not rebuilt
/// (research.md R5). The underlying query needs no auth-aware changes; it already assumes its
/// caller has established authorization, same as the device-token caregiver route does today.
/// </summary>
public class GetParentDailySummaryQueryHandler(
    ITenantDbContext db,
    ICurrentParentContactResolver contactResolver,
    IMediator mediator) : IRequestHandler<GetParentDailySummaryQuery, ParentDailySummaryResult>
{
    public async Task<ParentDailySummaryResult> Handle(GetParentDailySummaryQuery request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return ParentDailySummaryResult.Forbidden();

        var isContactOfChild = await db.ChildContacts
            .AnyAsync(cc => cc.ContactId == contact.Id && cc.ChildId == request.ChildId, cancellationToken);
        if (!isContactOfChild)
            return ParentDailySummaryResult.Forbidden();

        var summary = await mediator.Send(new GetDailySummaryQuery(request.ChildId, request.Date), cancellationToken);
        return ParentDailySummaryResult.Ok(summary);
    }
}
