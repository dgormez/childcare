using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Children;

public class VerifyChildIdentityCommandHandler(ITenantDbContext db, IProfilePhotoStorage photoStorage)
    : IRequestHandler<VerifyChildIdentityCommand, ChildResult>
{
    public async Task<ChildResult> Handle(VerifyChildIdentityCommand request, CancellationToken cancellationToken)
    {
        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == request.ChildId, cancellationToken);
        if (child is null)
            return ChildResult.Fail(ChildFailure.NotFound);

        var now = DateTime.UtcNow;

        child.IdVerifiedAt = now;
        child.IdVerifiedByUserId = request.VerifiedByUserId;
        child.IdVerifiedByEmail = request.VerifiedByEmail;
        child.IdDocumentType = request.DocumentType;
        child.IdDocumentNote = request.Note;

        // FR-006: the first verification is set once and never overwritten by a later correction.
        if (child.FirstIdVerifiedAt is null)
        {
            child.FirstIdVerifiedAt = now;
            child.FirstIdVerifiedByUserId = request.VerifiedByUserId;
            child.FirstIdVerifiedByEmail = request.VerifiedByEmail;
        }

        child.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);

        var photoUrl = await photoStorage.CreateDownloadUrlAsync(child.ProfilePhotoObjectPath, cancellationToken);
        return ChildResult.Success(ChildMapper.ToResponse(child, photoUrl));
    }
}
