using ChildCare.Application.Common;
using MediatR;

namespace ChildCare.Application.Auth;

public class DeleteAccountCommandHandler(ITenantDbContext db) : IRequestHandler<DeleteAccountCommand, bool>
{
    public async Task<bool> Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([request.UserId], cancellationToken);
        if (user is null) return false;

        db.Users.Remove(user); // cascades to refresh_tokens (DB-level ON DELETE CASCADE)
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
