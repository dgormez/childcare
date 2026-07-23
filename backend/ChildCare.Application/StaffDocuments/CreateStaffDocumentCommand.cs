using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffDocuments;

// FR-011/FR-012a: CreatedBy is the acting director's TenantUserId, resolved server-side, never
// client-supplied.
public record CreateStaffDocumentCommand(
    Guid StaffProfileId,
    Guid DirectorTenantUserId,
    string DocumentType,
    string Title,
    string ObjectPath,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil) : IRequest<StaffDocumentResult>;

public class CreateStaffDocumentCommandHandler(ITenantDbContext db, IStaffDocumentStorage storage)
    : IRequestHandler<CreateStaffDocumentCommand, StaffDocumentResult>
{
    public async Task<StaffDocumentResult> Handle(CreateStaffDocumentCommand request, CancellationToken cancellationToken)
    {
        var exists = await db.StaffProfiles.AnyAsync(p => p.Id == request.StaffProfileId, cancellationToken);
        if (!exists)
            return StaffDocumentResult.Fail(StaffDocumentFailure.StaffNotFound);

        if (!StaffDocumentTypeExtensions.TryParseWireString(request.DocumentType, out var documentType))
            return StaffDocumentResult.Fail(StaffDocumentFailure.InvalidDocumentType);

        var document = new StaffDocument
        {
            StaffProfileId = request.StaffProfileId,
            DocumentType = documentType,
            Title = request.Title,
            ObjectPath = request.ObjectPath,
            ValidFrom = request.ValidFrom,
            ValidUntil = request.ValidUntil,
            CreatedBy = request.DirectorTenantUserId,
        };

        db.StaffDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        var downloadUrl = await storage.CreateDownloadUrlAsync(document.ObjectPath, cancellationToken);
        return StaffDocumentResult.Success(StaffDocumentMapper.ToResponse(document, downloadUrl));
    }
}
