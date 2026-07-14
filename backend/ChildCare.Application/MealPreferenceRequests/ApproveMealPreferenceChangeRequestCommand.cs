using ChildCare.Application.Common;
using ChildCare.Application.MealPreferences;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.MealPreferenceRequests;

public record ApproveMealPreferenceChangeRequestCommand(Guid DirectorTenantUserId, Guid RequestId) : IRequest<MealPreferenceChangeRequestResult>;

public class ApproveMealPreferenceChangeRequestCommandHandler(
    ITenantDbContext db,
    IMediator mediator,
    MealPreferenceRequestNotificationService notificationService)
    : IRequestHandler<ApproveMealPreferenceChangeRequestCommand, MealPreferenceChangeRequestResult>
{
    public async Task<MealPreferenceChangeRequestResult> Handle(ApproveMealPreferenceChangeRequestCommand request, CancellationToken cancellationToken)
    {
        var changeRequest = await db.MealPreferenceChangeRequests.FirstOrDefaultAsync(r => r.Id == request.RequestId, cancellationToken);
        if (changeRequest is null || changeRequest.Status != MealPreferenceChangeRequestStatus.Pending)
            return MealPreferenceChangeRequestResult.Fail(MealPreferenceChangeRequestFailure.NotPending);

        // research.md R1: writes through the existing UpsertMealPreferenceCommand rather than a
        // second direct EF write — FR-014's partial-write-through rule falls out of that command's
        // own null-coalesce merge (only NewTexture/NewDietaryType are non-null here, so
        // PortionSize/AdditionalNotes and any dietary tags the request didn't touch are untouched).
        // A ChildNotFound failure here (spec.md Edge Cases: child deactivated since the request was
        // submitted) MUST propagate as a clean error, not be swallowed into a silent no-op decision.
        var upsertResult = await mediator.Send(
            new UpsertMealPreferenceCommand(changeRequest.ChildId, changeRequest.NewTexture, changeRequest.NewDietaryType, null, null, request.DirectorTenantUserId),
            cancellationToken);
        if (!upsertResult.Succeeded)
            return MealPreferenceChangeRequestResult.Fail(MealPreferenceChangeRequestFailure.ChildNotFound);

        changeRequest.Status = MealPreferenceChangeRequestStatus.Approved;
        changeRequest.DecidedBy = request.DirectorTenantUserId;
        changeRequest.DecidedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyDecisionAsync(changeRequest, cancellationToken);

        var child = await db.Children.FirstAsync(c => c.Id == changeRequest.ChildId, cancellationToken);
        var response = MealPreferenceRequestMapper.ToResponse(changeRequest, $"{child.FirstName} {child.LastName}", string.Empty, []);
        return MealPreferenceChangeRequestResult.Success(response);
    }
}
