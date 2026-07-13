using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.HealthRecords;

public record DeleteHealthRecordCommand(Guid ChildId, Guid Id) : IRequest<HealthRecordDeleteResult>;

public class DeleteHealthRecordCommandHandler(ITenantDbContext db) : IRequestHandler<DeleteHealthRecordCommand, HealthRecordDeleteResult>
{
    public async Task<HealthRecordDeleteResult> Handle(DeleteHealthRecordCommand request, CancellationToken cancellationToken)
    {
        var record = await db.HealthRecords
            .SingleOrDefaultAsync(r => r.Id == request.Id && r.ChildId == request.ChildId && r.DeletedAt == null, cancellationToken);
        if (record is null)
            return HealthRecordDeleteResult.Fail(HealthRecordFailure.NotFound);

        record.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return HealthRecordDeleteResult.Success();
    }
}

public class HealthRecordDeleteResult
{
    public HealthRecordFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static HealthRecordDeleteResult Success() => new();
    public static HealthRecordDeleteResult Fail(HealthRecordFailure failure) => new() { Failure = failure };
}
