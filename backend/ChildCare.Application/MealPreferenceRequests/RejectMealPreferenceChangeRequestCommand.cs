using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.MealPreferenceRequests;

public record RejectMealPreferenceChangeRequestCommand(Guid DirectorTenantUserId, Guid RequestId, string? Reason) : IRequest<MealPreferenceChangeRequestResult>;

public class RejectMealPreferenceChangeRequestCommandValidator : AbstractValidator<RejectMealPreferenceChangeRequestCommand>
{
    public RejectMealPreferenceChangeRequestCommandValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(2000).WithMessage("errors.meal_preference_requests.notes_too_long");
    }
}

public class RejectMealPreferenceChangeRequestCommandHandler(
    ITenantDbContext db,
    MealPreferenceRequestNotificationService notificationService)
    : IRequestHandler<RejectMealPreferenceChangeRequestCommand, MealPreferenceChangeRequestResult>
{
    public async Task<MealPreferenceChangeRequestResult> Handle(RejectMealPreferenceChangeRequestCommand request, CancellationToken cancellationToken)
    {
        var changeRequest = await db.MealPreferenceChangeRequests.FirstOrDefaultAsync(r => r.Id == request.RequestId, cancellationToken);
        if (changeRequest is null || changeRequest.Status != MealPreferenceChangeRequestStatus.Pending)
            return MealPreferenceChangeRequestResult.Fail(MealPreferenceChangeRequestFailure.NotPending);

        changeRequest.Status = MealPreferenceChangeRequestStatus.Rejected;
        changeRequest.DecidedBy = request.DirectorTenantUserId;
        changeRequest.DecidedAt = DateTime.UtcNow;
        changeRequest.DecisionNotes = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        await db.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyDecisionAsync(changeRequest, cancellationToken);

        var child = await db.Children.FirstAsync(c => c.Id == changeRequest.ChildId, cancellationToken);
        var response = MealPreferenceRequestMapper.ToResponse(changeRequest, $"{child.FirstName} {child.LastName}", string.Empty, []);
        return MealPreferenceChangeRequestResult.Success(response);
    }
}
