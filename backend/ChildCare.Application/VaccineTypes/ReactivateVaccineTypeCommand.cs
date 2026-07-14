using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.VaccineTypes;

// FR-007/FR-008: clears all three audit fields (research.md R2) — no history retained, a later
// re-deactivation starts from a clean slate (data-model.md's invariant: IsActive true iff all
// three audit fields are null). No-op if already active.
public record ReactivateVaccineTypeCommand(Guid Id) : IRequest<PlatformAdminVaccineTypeResult>;

public class ReactivateVaccineTypeCommandHandler(IPublicDbContext publicDb)
    : IRequestHandler<ReactivateVaccineTypeCommand, PlatformAdminVaccineTypeResult>
{
    public async Task<PlatformAdminVaccineTypeResult> Handle(ReactivateVaccineTypeCommand request, CancellationToken cancellationToken)
    {
        var entry = await publicDb.VaccineTypes.FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken);
        if (entry is null)
            return PlatformAdminVaccineTypeResult.Fail(PlatformAdminVaccineTypeFailure.NotFound);

        if (entry.IsActive)
            return PlatformAdminVaccineTypeResult.Success(PlatformAdminVaccineTypeMapper.ToResponse(entry));

        entry.IsActive = true;
        entry.DeactivatedByUserId = null;
        entry.DeactivatedByEmail = null;
        entry.DeactivatedAt = null;
        entry.UpdatedAt = DateTime.UtcNow;

        await publicDb.SaveChangesAsync(cancellationToken);

        return PlatformAdminVaccineTypeResult.Success(PlatformAdminVaccineTypeMapper.ToResponse(entry));
    }
}
