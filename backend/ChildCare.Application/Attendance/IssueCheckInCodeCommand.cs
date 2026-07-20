using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Attendance;

// Feature 021 — spec.md FR-005. TenantUserId comes from the calling parent's own JWT claims
// (endpoint layer resolves it), never client-supplied — same convention as
// GetParentMonthlyMenuQuery (013j).
public record IssueCheckInCodeCommand(Guid TenantUserId, Guid ChildId) : IRequest<IssueCheckInCodeResult>;

public class IssueCheckInCodeCommandValidator : AbstractValidator<IssueCheckInCodeCommand>
{
    public IssueCheckInCodeCommandValidator()
    {
        RuleFor(x => x.ChildId).NotEmpty();
    }
}

public enum IssueCheckInCodeFailure
{
    ChildNotFound,
    NotYourChild,
    NotEnabled,
}

public class IssueCheckInCodeResult
{
    public IssuedCheckInCode? Code { get; private init; }
    public IssueCheckInCodeFailure? Failure { get; private init; }
    public bool Succeeded => Failure is null;

    public static IssueCheckInCodeResult Success(IssuedCheckInCode code) => new() { Code = code };
    public static IssueCheckInCodeResult Fail(IssueCheckInCodeFailure failure) => new() { Failure = failure };
}

/// <summary>
/// Feature 021, research.md R3/R4 — reuses the existing Contact.TenantUserId/ChildContact
/// ownership model (the same one 013's messaging and 031's photo-download endpoints check)
/// rather than inventing a second parent-to-child linkage mechanism.
/// </summary>
public class IssueCheckInCodeCommandHandler(
    ITenantDbContext db,
    ICurrentParentContactResolver contactResolver,
    ICheckInCodeService codeService) : IRequestHandler<IssueCheckInCodeCommand, IssueCheckInCodeResult>
{
    public async Task<IssueCheckInCodeResult> Handle(IssueCheckInCodeCommand request, CancellationToken cancellationToken)
    {
        var childExists = await db.Children.AnyAsync(c => c.Id == request.ChildId, cancellationToken);
        if (!childExists)
            return IssueCheckInCodeResult.Fail(IssueCheckInCodeFailure.ChildNotFound);

        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        var isLinked = contact is not null && await db.ChildContacts
            .AnyAsync(cc => cc.ChildId == request.ChildId && cc.ContactId == contact.Id, cancellationToken);
        if (!isLinked)
            return IssueCheckInCodeResult.Fail(IssueCheckInCodeFailure.NotYourChild);

        var enabledAtAnyEnrolledLocation = await db.Contracts
            .Where(c => c.ChildId == request.ChildId && c.Status == ContractStatus.Active)
            .Join(db.Locations, c => c.LocationId, l => l.Id, (c, l) => l.QrCheckInEnabled)
            .AnyAsync(enabled => enabled, cancellationToken);
        if (!enabledAtAnyEnrolledLocation)
            return IssueCheckInCodeResult.Fail(IssueCheckInCodeFailure.NotEnabled);

        return IssueCheckInCodeResult.Success(codeService.Issue(request.ChildId));
    }
}
