using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffDocuments;

public record ListStaffDocumentsQuery(Guid StaffProfileId) : IRequest<IReadOnlyList<StaffDocumentResponse>>;

public class ListStaffDocumentsQueryHandler(ITenantDbContext db, IStaffDocumentStorage storage)
    : IRequestHandler<ListStaffDocumentsQuery, IReadOnlyList<StaffDocumentResponse>>
{
    public async Task<IReadOnlyList<StaffDocumentResponse>> Handle(ListStaffDocumentsQuery request, CancellationToken cancellationToken)
    {
        var documents = await db.StaffDocuments
            .Where(d => d.StaffProfileId == request.StaffProfileId && d.DeletedAt == null)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        var responses = new List<StaffDocumentResponse>();
        foreach (var document in documents)
        {
            var downloadUrl = await storage.CreateDownloadUrlAsync(document.ObjectPath, cancellationToken);
            responses.Add(StaffDocumentMapper.ToResponse(document, downloadUrl));
        }
        return responses;
    }
}
