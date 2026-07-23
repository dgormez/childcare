using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffTimeEntries;

// FR-010: at least one function is required — a staff member with none configured cannot clock
// in (ClockInCommand's NoFunctionConfigured failure).
public record UpdateStaffTimeEntryFunctionsCommand(Guid StaffProfileId, IReadOnlyList<string> Functions) : IRequest<bool>;

public class UpdateStaffTimeEntryFunctionsCommandHandler(ITenantDbContext db) : IRequestHandler<UpdateStaffTimeEntryFunctionsCommand, bool>
{
    public async Task<bool> Handle(UpdateStaffTimeEntryFunctionsCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.Id == request.StaffProfileId, cancellationToken);
        if (profile is null)
            return false;

        if (request.Functions.Count == 0)
            return false;

        var functions = new List<StaffTimeEntryFunction>();
        foreach (var wireValue in request.Functions)
        {
            if (!StaffTimeEntryFunctionExtensions.TryParseWireString(wireValue, out var function))
                return false;
            functions.Add(function);
        }

        profile.TimeEntryFunctions = functions.Distinct().ToList();
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
