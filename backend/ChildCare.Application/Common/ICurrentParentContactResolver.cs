using ChildCare.Domain.Entities;

namespace ChildCare.Application.Common;

/// <summary>
/// Resolves the `Contact` record linked to a `ParentOnly`-authenticated caller's TenantUserId
/// (feature 013, research.md R1). Every parent-facing handler needing "which family is this
/// request for" depends on this rather than re-querying `Contacts.FirstOrDefaultAsync` inline —
/// the shared authorization primitive behind spec.md FR-006/FR-017. Returns null when no
/// Contact is linked (should not happen for a genuinely `ParentOnly` token, but handlers still
/// check explicitly rather than assuming).
/// </summary>
public interface ICurrentParentContactResolver
{
    Task<Contact?> ResolveAsync(Guid tenantUserId, CancellationToken cancellationToken = default);
}
