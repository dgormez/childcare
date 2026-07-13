using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.VaccineRecords;

public record DeleteVaccineRecordCommand(Guid ChildId, Guid Id) : IRequest<VaccineRecordDeleteResult>;

public class DeleteVaccineRecordCommandHandler(ITenantDbContext db) : IRequestHandler<DeleteVaccineRecordCommand, VaccineRecordDeleteResult>
{
    public async Task<VaccineRecordDeleteResult> Handle(DeleteVaccineRecordCommand request, CancellationToken cancellationToken)
    {
        var record = await db.VaccineRecords
            .SingleOrDefaultAsync(v => v.Id == request.Id && v.ChildId == request.ChildId && v.DeletedAt == null, cancellationToken);
        if (record is null)
            return VaccineRecordDeleteResult.Fail(VaccineRecordFailure.NotFound);

        record.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return VaccineRecordDeleteResult.Success();
    }
}

public class VaccineRecordDeleteResult
{
    public VaccineRecordFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static VaccineRecordDeleteResult Success() => new();
    public static VaccineRecordDeleteResult Fail(VaccineRecordFailure failure) => new() { Failure = failure };
}
