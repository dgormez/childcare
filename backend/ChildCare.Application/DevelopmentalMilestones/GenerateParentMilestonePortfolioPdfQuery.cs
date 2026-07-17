using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.DevelopmentalMilestones;

// Parent PDF export — mirrors GenerateParentInvoicePdfQuery's ownership-check shape, reusing
// GetParentMilestonePortfolioQuery's authorization (no per-observation history in the PDF
// either, same as the JSON endpoint).
public record GenerateParentMilestonePortfolioPdfQuery(Guid TenantUserId, Guid ChildId, string? Locale)
    : IRequest<GenerateParentMilestonePortfolioPdfResult>;

public record GenerateParentMilestonePortfolioPdfResult(bool Authorized, byte[] Bytes);

public class GenerateParentMilestonePortfolioPdfQueryHandler(ITenantDbContext db, IMediator mediator, IMilestonePortfolioPdfGenerator pdfGenerator)
    : IRequestHandler<GenerateParentMilestonePortfolioPdfQuery, GenerateParentMilestonePortfolioPdfResult>
{
    private static readonly string[] SupportedLocales = ["nl", "fr", "en"];

    public async Task<GenerateParentMilestonePortfolioPdfResult> Handle(GenerateParentMilestonePortfolioPdfQuery request, CancellationToken cancellationToken)
    {
        var authorized = await mediator.Send(new GetParentMilestonePortfolioQuery(request.TenantUserId, request.ChildId), cancellationToken);
        if (!authorized.Authorized)
            return new GenerateParentMilestonePortfolioPdfResult(false, []);

        var child = await db.Children.FirstAsync(c => c.Id == request.ChildId, cancellationToken);
        var locale = request.Locale is not null && SupportedLocales.Contains(request.Locale) ? request.Locale : "nl";

        var model = new MilestonePortfolioPdfModel($"{child.FirstName} {child.LastName}", authorized.Response!, locale);
        var bytes = await pdfGenerator.GenerateAsync(model, cancellationToken);
        return new GenerateParentMilestonePortfolioPdfResult(true, bytes);
    }
}
