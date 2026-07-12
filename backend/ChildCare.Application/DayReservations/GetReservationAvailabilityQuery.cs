using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.DayReservations;

public record GetReservationAvailabilityQuery(Guid TenantUserId, Guid ChildId) : IRequest<ReservationAvailabilityResult>;

public class ReservationAvailabilityResult
{
    public bool Succeeded { get; init; }
    public ReservationAvailabilityResponse? Response { get; init; }

    public static ReservationAvailabilityResult Success(ReservationAvailabilityResponse response) => new() { Succeeded = true, Response = response };
    public static ReservationAvailabilityResult Fail() => new();
}

/// <summary>
/// Feature 013f — resolves "today" as the representative date for each type (research.md;
/// this is a UI hint only, not authoritative — actual enforcement always re-resolves against
/// the real requested date at submission time, per FR-017). Reuses the same child-linkage check
/// SubmitDayReservationCommand performs.
/// </summary>
public class GetReservationAvailabilityQueryHandler(
    ITenantDbContext db,
    ICurrentParentContactResolver contactResolver,
    ReservationPolicyResolver policyResolver) : IRequestHandler<GetReservationAvailabilityQuery, ReservationAvailabilityResult>
{
    public async Task<ReservationAvailabilityResult> Handle(GetReservationAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return ReservationAvailabilityResult.Fail();

        var isContactOfChild = await db.ChildContacts
            .AnyAsync(cc => cc.ContactId == contact.Id && cc.ChildId == request.ChildId, cancellationToken);
        if (!isContactOfChild)
            return ReservationAvailabilityResult.Fail();

        var today = BelgianCalendarDay.Today();
        var absence = await policyResolver.ResolveAsync(request.ChildId, DayReservationType.Absence, today, cancellationToken);
        var extra = await policyResolver.ResolveAsync(request.ChildId, DayReservationType.Extra, today, cancellationToken);
        var exchange = await policyResolver.ResolveAsync(request.ChildId, DayReservationType.Exchange, today, cancellationToken);

        return ReservationAvailabilityResult.Success(new ReservationAvailabilityResponse(
            ReservationModeMapper.ToWire(absence.Mode),
            ReservationModeMapper.ToWire(extra.Mode),
            ReservationModeMapper.ToWire(exchange.Mode),
            Math.Max(absence.NoticeHours, Math.Max(extra.NoticeHours, exchange.NoticeHours))));
    }
}
