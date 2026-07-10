using ChildCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Common;

public class CurrentParentContactResolver(ITenantDbContext db) : ICurrentParentContactResolver
{
    public Task<Contact?> ResolveAsync(Guid tenantUserId, CancellationToken cancellationToken = default)
        => db.Contacts.FirstOrDefaultAsync(c => c.TenantUserId == tenantUserId, cancellationToken);
}
