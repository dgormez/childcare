using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Children;

public record RequestChildPhotoUploadUrlCommand(Guid ChildId) : IRequest<RequestPhotoUploadUrlResult>;

/// <summary>Reuses feature 005's shared result shape — no full ChildResponse needed here.</summary>
public class RequestPhotoUploadUrlResult
{
    public RequestPhotoUploadUrlResponse? Response { get; private init; }
    public bool Succeeded => Response is not null;

    public static RequestPhotoUploadUrlResult Success(RequestPhotoUploadUrlResponse response) => new() { Response = response };
    public static RequestPhotoUploadUrlResult NotFound() => new();
}

public class RequestChildPhotoUploadUrlCommandHandler(ITenantDbContext db, IProfilePhotoStorage photoStorage)
    : IRequestHandler<RequestChildPhotoUploadUrlCommand, RequestPhotoUploadUrlResult>
{
    public async Task<RequestPhotoUploadUrlResult> Handle(RequestChildPhotoUploadUrlCommand request, CancellationToken cancellationToken)
    {
        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == request.ChildId, cancellationToken);
        if (child is null)
            return RequestPhotoUploadUrlResult.NotFound();

        var (objectPath, uploadUrl) = await photoStorage.CreateUploadUrlAsync("children", request.ChildId, cancellationToken);

        child.ProfilePhotoObjectPath = objectPath;
        child.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return RequestPhotoUploadUrlResult.Success(new RequestPhotoUploadUrlResponse(uploadUrl, objectPath));
    }
}
