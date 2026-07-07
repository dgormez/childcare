using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contracts;

public record ActivateContractCommand(Guid Id) : IRequest<ContractResult>;

public class ActivateContractCommandHandler(ITenantDbContext db, IAdvisoryLockService advisoryLock)
    : IRequestHandler<ActivateContractCommand, ContractResult>
{
    public async Task<ContractResult> Handle(ActivateContractCommand request, CancellationToken cancellationToken)
    {
        var contract = await db.Contracts.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (contract is null)
            return ContractResult.Fail(ContractFailure.NotFound);

        if (contract.Status != ContractStatus.Draft)
            return ContractResult.Fail(ContractFailure.NotDraft);

        // FR-006: serialized per child so two concurrent activation attempts for the same
        // child cannot both succeed when they would violate FR-004/FR-005 (research.md R2).
        var failure = await advisoryLock.RunExclusiveAsync(
            contract.ChildId,
            () => ContractActivationChecker.CheckAndActivateAsync(db, contract, cancellationToken),
            cancellationToken);

        return failure is null
            ? ContractResult.Success(ContractMapper.ToResponse(contract))
            : ContractResult.Fail(failure.Value);
    }
}
