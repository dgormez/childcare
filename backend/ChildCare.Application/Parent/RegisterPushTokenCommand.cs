using ChildCare.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Parent;

/// <summary>
/// FR-014: overwrites Contact.PushToken (research.md R2) — a single active token per parent
/// account, replacing any previously stored value so a reinstall never leaves a stale one active.
/// Returns false if the caller has no linked Contact (should not happen for a genuine
/// ParentOnly token, but the handler checks explicitly rather than assuming).
/// </summary>
public record RegisterPushTokenCommand(Guid TenantUserId, string PushToken) : IRequest<bool>;

public class RegisterPushTokenCommandValidator : AbstractValidator<RegisterPushTokenCommand>
{
    public RegisterPushTokenCommandValidator()
    {
        RuleFor(x => x.PushToken)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.parent.push_token_required")
            .MaximumLength(200).WithMessage("errors.parent.push_token_too_long");
    }
}

public class RegisterPushTokenCommandHandler(ITenantDbContext db, ICurrentParentContactResolver contactResolver)
    : IRequestHandler<RegisterPushTokenCommand, bool>
{
    public async Task<bool> Handle(RegisterPushTokenCommand request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return false;

        contact.PushToken = request.PushToken;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
