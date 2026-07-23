using System.Security.Cryptography;
using System.Text;
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

        // Validator already guarantees exactly 11 digits after stripping separators, and that
        // the mod-97 checksum is valid.
        var normalized = Regex.Replace(request.Nrn, @"[.\-\s]", string.Empty);
        var hash = Hash(normalized);

        var alreadyUsedByAnotherChild = await db.Children
            .AnyAsync(c => c.Id != request.ChildId && c.NrnHash == hash, cancellationToken);
        if (alreadyUsedByAnotherChild)
            return ChildResult.Fail(ChildFailure.NrnAlreadyInUse);

        child.NrnLast4 = normalized[^4..];
        child.EncryptedNrn = nrnProtector.Protect(normalized);
        child.NrnHash = hash;
        child.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var photoUrl = await photoStorage.CreateDownloadUrlAsync(child.ProfilePhotoObjectPath, cancellationToken);
        return ChildResult.Success(ChildMapper.ToResponse(child, photoUrl));
    }

    private static string Hash(string normalizedNrn) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedNrn)));
}
