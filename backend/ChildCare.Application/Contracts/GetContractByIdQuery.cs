using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contracts;

public record GetContractByIdQuery(Guid Id) : IRequest<ContractResult>;

public class GetContractByIdQueryHandler(ITenantDbContext db) : IRequestHandler<GetContractByIdQuery, ContractResult>
{
    public async Task<ContractResult> Handle(GetContractByIdQuery request, CancellationToken cancellationToken)
    {
        var contract = await db.Contracts.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        return contract is null
            ? ContractResult.Fail(ContractFailure.NotFound)
            : ContractResult.Success(ContractMapper.ToResponse(contract));
    }
}
