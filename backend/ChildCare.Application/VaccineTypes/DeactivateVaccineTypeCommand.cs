using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.VaccineTypes;

// FR-007/FR-008: ActingUserId/ActingUserEmail are resolved server-side by the endpoint from the
// caller's own JWT claims (mirrors every other "resolved server-side, never client-supplied"
// identity field in this codebase, e.g. child_events.recorded_by) — never trust a client-supplied
// identity for an audit field. No-op (unchanged audit fields) if already inactive (Edge Cases) —
// the original deactivation's who/when is preserved, not overwritten by a redundant call.
public record DeactivateVaccineTypeCommand(Guid Id, Guid ActingUserId, string ActingUserEmail) : IRequest<PlatformAdminVaccineTypeResult>;

public class DeactivateVaccineTypeCommandHandler(IPublicDbContext publicDb)
    : IRequestHandler<DeactivateVaccineTypeCommand, PlatformAdminVaccineTypeResult>
{
    public async Task<PlatformAdminVaccineTypeResult> Handle(DeactivateVaccineTypeCommand request, CancellationToken cancellationToken)
    {
        var entry = await publicDb.VaccineTypes.FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken);
        if (entry is null)
            return PlatformAdminVaccineTypeResult.Fail(PlatformAdminVaccineTypeFailure.NotFound);

        if (!entry.IsActive)
            return PlatformAdminVaccineTypeResult.Success(PlatformAdminVaccineTypeMapper.ToResponse(entry));

        entry.IsActive = false;
        entry.DeactivatedByUserId = request.ActingUserId;
        entry.DeactivatedByEmail = request.ActingUserEmail;
        entry.DeactivatedAt = DateTime.UtcNow;
        entry.UpdatedAt = entry.DeactivatedAt.Value;

        await publicDb.SaveChangesAsync(cancellationToken);

        return PlatformAdminVaccineTypeResult.Success(PlatformAdminVaccineTypeMapper.ToResponse(entry));
    }
}
