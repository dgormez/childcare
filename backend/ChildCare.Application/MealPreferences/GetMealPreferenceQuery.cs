using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.MealPreferences;

// DirectorOnly, additive gap found while wiring the child-profile edit form (mirrors 007a's
// precedent of adding small, additive, read-only endpoints once a UI surfaces a real need): the
// PUT is a partial-upsert, so the director-facing edit form needs the *current* values to
// pre-fill, not just the aggregated meal-list's per-child entry.
public record GetMealPreferenceQuery(Guid ChildId) : IRequest<MealPreferenceResult>;

public class GetMealPreferenceQueryHandler(ITenantDbContext db) : IRequestHandler<GetMealPreferenceQuery, MealPreferenceResult>
{
    public async Task<MealPreferenceResult> Handle(GetMealPreferenceQuery request, CancellationToken cancellationToken)
    {
        var childExists = await db.Children.AnyAsync(c => c.Id == request.ChildId, cancellationToken);
        if (!childExists)
            return MealPreferenceResult.Fail(MealPreferenceFailure.ChildNotFound);

        var preference = await db.MealPreferences.FirstOrDefaultAsync(p => p.ChildId == request.ChildId, cancellationToken);

        // No row yet: return column defaults rather than 404 — FR-005's "Geen voorkeur" default
        // applies equally to the single-child read, not just the aggregated meal list.
        preference ??= new MealPreference { ChildId = request.ChildId };

        return MealPreferenceResult.Success(MealListMapper.ToPreferenceResponse(preference));
    }
}
