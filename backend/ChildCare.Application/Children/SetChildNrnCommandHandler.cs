using System.Text.RegularExpressions;
using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Children;

public class SetChildNrnCommandHandler(ITenantDbContext db, IProfilePhotoStorage photoStorage, INrnProtector nrnProtector)
    : IRequestHandler<SetChildNrnCommand, ChildResult>
{
    public async Task<ChildResult> Handle(SetChildNrnCommand request, CancellationToken cancellationToken)
    {
        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == request.ChildId, cancellationToken);
        if (child is null)
            return ChildResult.Fail(ChildFailure.NotFound);

        // Validator already guarantees exactly 11 digits after stripping separators.
        var normalized = Regex.Replace(request.Nrn, @"[.\-\s]", string.Empty);

        child.NrnLast4 = normalized[^4..];
        child.EncryptedNrn = nrnProtector.Protect(normalized);
        child.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var photoUrl = await photoStorage.CreateDownloadUrlAsync(child.ProfilePhotoObjectPath, cancellationToken);
        return ChildResult.Success(ChildMapper.ToResponse(child, photoUrl));
    }
}
