using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.MealPreferenceRequests;

public record GetParentChildMealPreferenceQuery(Guid TenantUserId, Guid ChildId) : IRequest<GetParentChildMealPreferenceResult>;

public class GetParentChildMealPreferenceResult
{
    public bool Authorized { get; private init; }
    public ParentMealPreferenceResponse? Response { get; private init; }

    public static GetParentChildMealPreferenceResult Ok(ParentMealPreferenceResponse response) => new() { Authorized = true, Response = response };
    public static GetParentChildMealPreferenceResult Forbidden() => new() { Authorized = false };
}

public class GetParentChildMealPreferenceQueryHandler(
    ITenantDbContext db,
    ICurrentParentContactResolver contactResolver) : IRequestHandler<GetParentChildMealPreferenceQuery, GetParentChildMealPreferenceResult>
{
    public async Task<GetParentChildMealPreferenceResult> Handle(GetParentChildMealPreferenceQuery request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return GetParentChildMealPreferenceResult.Forbidden();

        var isContactOfChild = await db.ChildContacts
            .AnyAsync(cc => cc.ContactId == contact.Id && cc.ChildId == request.ChildId, cancellationToken);
        if (!isContactOfChild)
            return GetParentChildMealPreferenceResult.Forbidden();

        var preference = await db.MealPreferences.FirstOrDefaultAsync(p => p.ChildId == request.ChildId, cancellationToken);

        var hasPendingRequest = await db.MealPreferenceChangeRequests
            .AnyAsync(r => r.ChildId == request.ChildId && r.Status == MealPreferenceChangeRequestStatus.Pending, cancellationToken);

        return GetParentChildMealPreferenceResult.Ok(new ParentMealPreferenceResponse(
            Texture: preference?.Texture.ToString().ToLowerInvariant(),
            DietaryType: preference?.DietaryType.Select(d => d.ToWireString()).ToList(),
            HasPendingRequest: hasPendingRequest));
    }
}
