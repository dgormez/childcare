using ChildCare.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.WaitingList;

public record RecordTourOutcomeCommand(Guid Id, string Outcome) : IRequest<WaitingListEntryResult>;

public class RecordTourOutcomeCommandValidator : AbstractValidator<RecordTourOutcomeCommand>
{
    public RecordTourOutcomeCommandValidator()
    {
        RuleFor(x => x.Outcome).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(2000);
    }
}

/// <summary>FR-017 — callable with or without a prior invitation, independent of `TourInvitationStatus`.</summary>
public class RecordTourOutcomeCommandHandler(ITenantDbContext db) : IRequestHandler<RecordTourOutcomeCommand, WaitingListEntryResult>
{
    public async Task<WaitingListEntryResult> Handle(RecordTourOutcomeCommand request, CancellationToken cancellationToken)
    {
        var entry = await db.WaitingListEntries.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (entry is null)
            return WaitingListEntryResult.Fail(WaitingListFailure.NotFound);

        entry.TourOutcome = request.Outcome.Trim();
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var isDuplicate = await db.WaitingListEntries.AnyAsync(e =>
            e.LocationId == entry.LocationId && e.Id != entry.Id &&
            e.ChildFirstName == entry.ChildFirstName && e.ChildLastName == entry.ChildLastName && e.DateOfBirth == entry.DateOfBirth,
            cancellationToken);

        return WaitingListEntryResult.Success(WaitingListMapper.ToResponse(entry, isDuplicate));
    }
}
