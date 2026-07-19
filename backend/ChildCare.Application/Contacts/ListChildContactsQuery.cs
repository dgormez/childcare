using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace ChildCare.Application.Contacts;

// Feature 030 (US4) — contracts/family-siblings-api.md documents this route as "existing", but
// no GET endpoint to list a child's linked contacts existed before this feature (006/013 only
// ever exposed POST/PUT/DELETE on this resource) — the web Contacts tab is genuinely the first
// consumer needing a list read, so this query/route is new despite the contract's wording.
public record ListChildContactsQuery(Guid ChildId) : IRequest<IReadOnlyList<ChildContactResponse>>;

public class ListChildContactsQueryHandler(ITenantDbContext db) : IRequestHandler<ListChildContactsQuery, IReadOnlyList<ChildContactResponse>>
{
    public async Task<IReadOnlyList<ChildContactResponse>> Handle(ListChildContactsQuery request, CancellationToken cancellationToken)
    {
        var links = await db.ChildContacts
            .Where(cc => cc.ChildId == request.ChildId)
            .Join(db.Contacts, cc => cc.ContactId, c => c.Id, (cc, c) => new { Link = cc, Contact = c })
            .ToListAsync(cancellationToken);

        return links
            .OrderByDescending(x => x.Link.IsPrimary)
            .Select(x => ContactMapper.ToChildContactResponse(x.Link, x.Contact))
            .ToList();
    }
}
