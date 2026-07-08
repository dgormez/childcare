using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Staff;

/// <summary>Clears a caregiver's PIN — they can no longer check in until a director sets a new one.</summary>
public record DeleteCaregiverPinCommand(Guid StaffProfileId) : IRequest<PinManagementResult>;

public class DeleteCaregiverPinCommandHandler(ITenantDbContext db) : IRequestHandler<DeleteCaregiverPinCommand, PinManagementResult>
{
    public async Task<PinManagementResult> Handle(DeleteCaregiverPinCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.Id == request.StaffProfileId, cancellationToken);
        if (profile is null)
            return PinManagementResult.Fail(PinManagementFailure.NotFound);

        profile.PinHash = null;
        profile.PinFailedAttempts = 0;
        profile.PinFirstFailedAttemptAt = null;
        profile.PinLockedUntil = null;
        profile.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return PinManagementResult.Success();
    }
}
