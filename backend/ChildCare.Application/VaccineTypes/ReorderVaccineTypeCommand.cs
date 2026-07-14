using ChildCare.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.VaccineTypes;

// FR-006, research.md R4 — one step per call, scoped to the entry's own category (matching the
// list's own Category-then-SortOrder grouping, so a reorder always moves an entry within the
// visually contiguous block it's already displayed in). Mirrors
// ReorderWaitingListEntryCommand's up/down-adjacent-swap shape exactly.
public record ReorderVaccineTypeCommand(Guid Id, string Direction) : IRequest<PlatformAdminVaccineTypeListResult>;

public class ReorderVaccineTypeCommandValidator : AbstractValidator<ReorderVaccineTypeCommand>
{
    public ReorderVaccineTypeCommandValidator()
    {
        RuleFor(x => x.Direction).Must(d => d is "up" or "down");
    }
}

public class ReorderVaccineTypeCommandHandler(IPublicDbContext publicDb)
    : IRequestHandler<ReorderVaccineTypeCommand, PlatformAdminVaccineTypeListResult>
{
    public async Task<PlatformAdminVaccineTypeListResult> Handle(ReorderVaccineTypeCommand request, CancellationToken cancellationToken)
    {
        var entry = await publicDb.VaccineTypes.FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken);
        if (entry is null)
            return PlatformAdminVaccineTypeListResult.Fail(PlatformAdminVaccineTypeFailure.NotFound);

        var queue = await publicDb.VaccineTypes
            .Where(v => v.Category == entry.Category)
            .OrderBy(v => v.SortOrder)
            .ToListAsync(cancellationToken);

        var index = queue.FindIndex(v => v.Id == entry.Id);
        var neighborIndex = request.Direction == "up" ? index - 1 : index + 1;

        if (neighborIndex < 0 || neighborIndex >= queue.Count)
            return PlatformAdminVaccineTypeListResult.Fail(PlatformAdminVaccineTypeFailure.AlreadyAtBoundary);

        var neighbor = queue[neighborIndex];
        (entry.SortOrder, neighbor.SortOrder) = (neighbor.SortOrder, entry.SortOrder);
        entry.UpdatedAt = DateTime.UtcNow;
        neighbor.UpdatedAt = DateTime.UtcNow;

        await publicDb.SaveChangesAsync(cancellationToken);

        var fullList = await publicDb.VaccineTypes
            .AsNoTracking()
            .OrderBy(v => v.Category)
            .ThenBy(v => v.SortOrder)
            .ToListAsync(cancellationToken);

        return PlatformAdminVaccineTypeListResult.Success(fullList.Select(PlatformAdminVaccineTypeMapper.ToResponse).ToList());
    }
}
