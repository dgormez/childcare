using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffDocuments;

// FR-012a: soft-delete (DeletedAt/DeletedBy) preserves the audit trail; the GCS object is still
// hard-deleted (research.md R3).
public record DeleteStaffDocumentCommand(Guid Id, Guid DirectorTenantUserId) : IRequest<bool>;

public class DeleteStaffDocumentCommandHandler(ITenantDbContext db, IStaffDocumentStorage storage)
    : IRequestHandler<DeleteStaffDocumentCommand, bool>
{
    public async Task<bool> Handle(DeleteStaffDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await db.StaffDocuments.FirstOrDefaultAsync(d => d.Id == request.Id && d.DeletedAt == null, cancellationToken);
        if (document is null)
            return false;

        document.DeletedAt = DateTime.UtcNow;
        document.DeletedBy = request.DirectorTenantUserId;
        await db.SaveChangesAsync(cancellationToken);

        await storage.DeleteAsync(document.ObjectPath, cancellationToken);
        return true;
    }
}
