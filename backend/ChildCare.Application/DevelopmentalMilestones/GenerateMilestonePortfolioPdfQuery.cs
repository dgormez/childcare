using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.DevelopmentalMilestones;

// Director/caregiver PDF export — mirrors GenerateInvoicePdfQuery: rendered on-demand, never
// persisted (research.md R4). Includes full history (StaffOrDirector shape).
public record GenerateMilestonePortfolioPdfQuery(Guid ChildId, string? Locale) : IRequest<GenerateMilestonePortfolioPdfResult>;

public record GenerateMilestonePortfolioPdfResult(bool Found, byte[] Bytes);

public class GenerateMilestonePortfolioPdfQueryHandler(ITenantDbContext db, IMediator mediator, IMilestonePortfolioPdfGenerator pdfGenerator)
    : IRequestHandler<GenerateMilestonePortfolioPdfQuery, GenerateMilestonePortfolioPdfResult>
{
    private static readonly string[] SupportedLocales = ["nl", "fr", "en"];

    public async Task<GenerateMilestonePortfolioPdfResult> Handle(GenerateMilestonePortfolioPdfQuery request, CancellationToken cancellationToken)
    {
        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == request.ChildId, cancellationToken);
        if (child is null)
            return new GenerateMilestonePortfolioPdfResult(false, []);

        var portfolio = await mediator.Send(new GetChildMilestonePortfolioQuery(request.ChildId), cancellationToken);
        var locale = request.Locale is not null && SupportedLocales.Contains(request.Locale) ? request.Locale : "nl";

        var model = new MilestonePortfolioPdfModel($"{child.FirstName} {child.LastName}", portfolio.Response!, locale);
        var bytes = await pdfGenerator.GenerateAsync(model, cancellationToken);
        return new GenerateMilestonePortfolioPdfResult(true, bytes);
    }
}
