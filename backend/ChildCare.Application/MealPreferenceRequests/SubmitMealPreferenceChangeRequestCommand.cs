using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.MealPreferenceRequests;

public record SubmitMealPreferenceChangeRequestCommand(
    Guid TenantUserId,
    Guid ChildId,
    string? NewTexture,
    List<string>? NewDietaryType,
    string? Notes) : IRequest<MealPreferenceChangeRequestResult>;

public class SubmitMealPreferenceChangeRequestCommandValidator : AbstractValidator<SubmitMealPreferenceChangeRequestCommand>
{
    public SubmitMealPreferenceChangeRequestCommandValidator()
    {
        RuleFor(x => x.ChildId).NotEmpty();

        // data-model.md: a request that changes neither field is meaningless.
        RuleFor(x => x)
            .Must(c => c.NewTexture is not null || (c.NewDietaryType is not null && c.NewDietaryType.Count > 0))
            .WithMessage("errors.meal_preference_requests.nothing_requested");

        RuleFor(x => x.NewTexture)
            .Must(v => v is null || Enum.TryParse<MealTexture>(v, ignoreCase: true, out _))
            .WithMessage("errors.meal_preference_requests.texture_invalid");

        RuleFor(x => x.NewDietaryType)
            .Must(v => v is null || v.All(d => DietaryTypeExtensions.TryParseWireString(d, out _)))
            .WithMessage("errors.meal_preference_requests.dietary_type_invalid");

        RuleFor(x => x.Notes).MaximumLength(2000).WithMessage("errors.meal_preference_requests.notes_too_long");
    }
}

public class SubmitMealPreferenceChangeRequestCommandHandler(
    ITenantDbContext db,
    ICurrentParentContactResolver contactResolver) : IRequestHandler<SubmitMealPreferenceChangeRequestCommand, MealPreferenceChangeRequestResult>
{
    public async Task<MealPreferenceChangeRequestResult> Handle(SubmitMealPreferenceChangeRequestCommand request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return MealPreferenceChangeRequestResult.Fail(MealPreferenceChangeRequestFailure.ChildNotLinked);

        var isContactOfChild = await db.ChildContacts
            .AnyAsync(cc => cc.ContactId == contact.Id && cc.ChildId == request.ChildId, cancellationToken);
        if (!isContactOfChild)
            return MealPreferenceChangeRequestResult.Fail(MealPreferenceChangeRequestFailure.ChildNotLinked);

        // research.md R6: one pending request per child, enforced here (not a DB constraint).
        var hasPending = await db.MealPreferenceChangeRequests
            .AnyAsync(r => r.ChildId == request.ChildId && r.Status == MealPreferenceChangeRequestStatus.Pending, cancellationToken);
        if (hasPending)
            return MealPreferenceChangeRequestResult.Fail(MealPreferenceChangeRequestFailure.DuplicatePendingRequest);

        var changeRequest = new MealPreferenceChangeRequest
        {
            ChildId = request.ChildId,
            RequestedBy = request.TenantUserId,
            NewTexture = request.NewTexture,
            NewDietaryType = request.NewDietaryType,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };
        db.MealPreferenceChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync(cancellationToken);

        var child = await db.Children.FirstAsync(c => c.Id == request.ChildId, cancellationToken);
        var response = MealPreferenceRequestMapper.ToResponse(changeRequest, $"{child.FirstName} {child.LastName}", $"{contact.FirstName} {contact.LastName}", []);
        return MealPreferenceChangeRequestResult.Success(response);
    }
}
