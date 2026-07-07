using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contacts;

// Tenant-wide, not scoped to a child (research.md R6) — lets a director search and reuse an
// existing contact when linking a sibling (FR-006).
public record ListContactsQuery : IRequest<IReadOnlyList<ContactResponse>>;

public class ListContactsQueryHandler(ITenantDbContext db) : IRequestHandler<ListContactsQuery, IReadOnlyList<ContactResponse>>
{
    public async Task<IReadOnlyList<ContactResponse>> Handle(ListContactsQuery request, CancellationToken cancellationToken)
    {
        var contacts = await db.Contacts.ToListAsync(cancellationToken);
        return contacts.Select(ContactMapper.ToResponse).ToList();
    }
}
