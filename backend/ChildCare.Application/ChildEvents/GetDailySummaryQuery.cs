using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;

namespace ChildCare.Application.ChildEvents;

public record GetDailySummaryQuery(Guid ChildId, DateOnly Date) : IRequest<DailySummaryResponse>;

/// <summary>Thin MediatR wrapper over DailySummaryCalculator (feature 020 extracted the
/// calculation itself so a CLI job can call it with an explicit, non-ambient `db` — see that
/// class's doc comment).</summary>
public class GetDailySummaryQueryHandler(ITenantDbContext db, DailySummaryCalculator calculator) : IRequestHandler<GetDailySummaryQuery, DailySummaryResponse>
{
    public Task<DailySummaryResponse> Handle(GetDailySummaryQuery request, CancellationToken cancellationToken) =>
        calculator.CalculateAsync(db, request.ChildId, request.Date, cancellationToken);
}
