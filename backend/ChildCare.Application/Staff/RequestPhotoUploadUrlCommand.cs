using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Staff;

public record RequestPhotoUploadUrlCommand(Guid StaffProfileId) : IRequest<RequestPhotoUploadUrlResult>;

/// <summary>Separate, smaller result shape — this command never returns a full StaffResponse.</summary>
public class RequestPhotoUploadUrlResult
{
    public RequestPhotoUploadUrlResponse? Response { get; private init; }
    public bool Succeeded => Response is not null;

    public static RequestPhotoUploadUrlResult Success(RequestPhotoUploadUrlResponse response) => new() { Response = response };
    public static RequestPhotoUploadUrlResult NotFound() => new();
}

public class RequestPhotoUploadUrlCommandHandler(ITenantDbContext db, IProfilePhotoStorage photoStorage)
    : IRequestHandler<RequestPhotoUploadUrlCommand, RequestPhotoUploadUrlResult>
{
    public async Task<RequestPhotoUploadUrlResult> Handle(RequestPhotoUploadUrlCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.Id == request.StaffProfileId, cancellationToken);
        if (profile is null)
            return RequestPhotoUploadUrlResult.NotFound();

        var (objectPath, uploadUrl) = await photoStorage.CreateUploadUrlAsync(request.StaffProfileId, cancellationToken);

        // No separate "confirm upload" step — the object path is deterministic per profile
        // (research.md R3), so the next GET simply reflects whatever exists once the client's
        // direct PUT to GCS succeeds.
        profile.ProfilePhotoObjectPath = objectPath;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return RequestPhotoUploadUrlResult.Success(new RequestPhotoUploadUrlResponse(uploadUrl, objectPath));
    }
}
