using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contracts;

// Feature 026 — contracts/sepa-direct-debit-api.md, spec.md FR-011. Only valid on a contract with
// a signed, non-revoked mandate. Does not touch any invoice already PendingDebit from an earlier
// batch (spec.md FR-011's no-retroactive-effect clarification) — this is purely a Contract-level
// change; batch-eligibility exclusion (FR-001) reads SepaRevokedAt at eligibility-check time, not
// at revoke time, so no other write is needed here.
public record RevokeSepaMandateCommand(Guid ContractId) : IRequest<ContractResult>;

public class RevokeSepaMandateCommandHandler(ITenantDbContext db) : IRequestHandler<RevokeSepaMandateCommand, ContractResult>
{
    public async Task<ContractResult> Handle(RevokeSepaMandateCommand request, CancellationToken cancellationToken)
    {
        var contract = await db.Contracts.FirstOrDefaultAsync(c => c.Id == request.ContractId, cancellationToken);
        if (contract is null)
            return ContractResult.Fail(ContractFailure.NotFound);

        if (contract.SepaAuthorisedAt is null || contract.SepaRevokedAt is not null)
            return ContractResult.Fail(ContractFailure.MandateNotRevocable);

        contract.SepaRevokedAt = DateTime.UtcNow;
        contract.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return ContractResult.Success(ContractMapper.ToResponse(contract));
    }
}
