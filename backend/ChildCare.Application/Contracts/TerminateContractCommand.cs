using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contracts;

public record TerminateContractCommand(Guid Id, DateOnly EndDate) : IRequest<ContractResult>;

public class TerminateContractCommandHandler(ITenantDbContext db) : IRequestHandler<TerminateContractCommand, ContractResult>
{
    public async Task<ContractResult> Handle(TerminateContractCommand request, CancellationToken cancellationToken)
    {
        var contract = await db.Contracts.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (contract is null)
            return ContractResult.Fail(ContractFailure.NotFound);

        if (contract.Status != ContractStatus.Active)
            return ContractResult.Fail(ContractFailure.NotActive);

        if (request.EndDate < contract.StartDate)
            return ContractResult.Fail(ContractFailure.TerminationDateInvalid);

        // FR-009a: ends the contract with no successor — no lock needed, this is a single-row
        // status change with no cross-contract check (unlike activation/amendment).
        contract.Status = ContractStatus.Ended;
        contract.EndDate = request.EndDate;
        contract.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return ContractResult.Success(ContractMapper.ToResponse(contract));
    }
}
