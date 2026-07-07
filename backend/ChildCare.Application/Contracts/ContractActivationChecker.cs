using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contracts;

/// <summary>
/// Shared FR-004/FR-004a/FR-005 check used by both ActivateContractCommand (US1) and
/// AmendContractCommand (US3) — always invoked from inside
/// IAdvisoryLockService.RunExclusiveAsync(childId, ...) so the check-then-write is atomic per
/// child (FR-006, research.md R2).
///
/// Correctness note: <paramref name="excludeContractId"/> must be passed the predecessor's id
/// when called from an amendment. The predecessor's Status = Ended transition is only staged
/// in-memory at that point (not yet saved), and this method's query is translated to SQL —
/// its WHERE clause is evaluated against the still-Active *persisted* row, so the predecessor
/// would otherwise be (incorrectly) included in the conflict check below despite its pending
/// in-memory status change. Explicit id exclusion, not EF's identity-map, is what's needed
/// here — a tracked entity already in the change tracker is still returned by a query whose
/// filter matches its persisted (not yet flushed) column value.
/// </summary>
internal static class ContractActivationChecker
{
    public static async Task<ContractFailure?> CheckAndActivateAsync(
        ITenantDbContext db, Contract contract, CancellationToken cancellationToken, Guid? excludeContractId = null)
    {
        // FR-004a: a draft created while its location was still active must not be activatable
        // (or, for an amendment, re-pointed) once that location has since been deactivated.
        var locationActive = await db.Locations.AnyAsync(
            l => l.Id == contract.LocationId && l.DeactivatedAt == null, cancellationToken);
        if (!locationActive)
            return ContractFailure.LocationNotFound;

        var activeContracts = await db.Contracts
            .Where(c => c.ChildId == contract.ChildId
                     && c.Status == ContractStatus.Active
                     && c.Id != contract.Id
                     && c.Id != excludeContractId)
            .ToListAsync(cancellationToken);

        if (activeContracts.Any(c => c.LocationId == contract.LocationId))
            return ContractFailure.AlreadyActiveAtLocation;

        var weekdays = contract.ContractedDays.Select(d => d.Weekday).ToHashSet();
        if (activeContracts.Any(c => c.ContractedDays.Any(d => weekdays.Contains(d.Weekday))))
            return ContractFailure.DayOverlap;

        contract.Status = ContractStatus.Active;
        contract.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return null;
    }
}
