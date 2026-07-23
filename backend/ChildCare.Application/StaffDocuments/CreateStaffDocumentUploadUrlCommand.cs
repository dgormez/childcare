using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffDocuments;

public record CreateStaffDocumentUploadUrlResult(bool Succeeded, string? ObjectPath, string? UploadUrl);

public record CreateStaffDocumentUploadUrlCommand(Guid StaffProfileId, string ContentType) : IRequest<CreateStaffDocumentUploadUrlResult>;

public class CreateStaffDocumentUploadUrlCommandHandler(ITenantDbContext db, IStaffDocumentStorage storage)
    : IRequestHandler<CreateStaffDocumentUploadUrlCommand, CreateStaffDocumentUploadUrlResult>
{
    public async Task<CreateStaffDocumentUploadUrlResult> Handle(CreateStaffDocumentUploadUrlCommand request, CancellationToken cancellationToken)
    {
        var exists = await db.StaffProfiles.AnyAsync(p => p.Id == request.StaffProfileId, cancellationToken);
        if (!exists)
            return new CreateStaffDocumentUploadUrlResult(false, null, null);

        var (objectPath, uploadUrl) = await storage.CreateUploadUrlAsync(request.StaffProfileId, request.ContentType, cancellationToken: cancellationToken);
        return new CreateStaffDocumentUploadUrlResult(true, objectPath, uploadUrl);
    }
}
