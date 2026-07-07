using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contracts;

public record GenerateContractPdfQuery(Guid Id, string? Locale) : IRequest<GenerateContractPdfResult>;

public record GenerateContractPdfResult(bool Found, byte[] Bytes);

public class GenerateContractPdfQueryHandler(ITenantDbContext db, IContractPdfGenerator pdfGenerator)
    : IRequestHandler<GenerateContractPdfQuery, GenerateContractPdfResult>
{
    private static readonly string[] SupportedLocales = ["nl", "fr", "en"];

    public async Task<GenerateContractPdfResult> Handle(GenerateContractPdfQuery request, CancellationToken cancellationToken)
    {
        var contract = await db.Contracts.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (contract is null)
            return new GenerateContractPdfResult(false, []);

        var child = await db.Children.FirstAsync(c => c.Id == contract.ChildId, cancellationToken);
        var location = await db.Locations.FirstAsync(l => l.Id == contract.LocationId, cancellationToken);

        // FR-011: defaults to Dutch when not specified or unrecognized — the PDF is rendered
        // once server-side, so the language cannot be resolved client-side afterward.
        var locale = request.Locale is not null && SupportedLocales.Contains(request.Locale) ? request.Locale : "nl";

        var model = new ContractPdfModel(
            $"{child.FirstName} {child.LastName}",
            location.Name,
            contract.Status.ToString().ToLowerInvariant(),
            contract.ContractedDays.Select(d => new ContractPdfDay(d.Weekday, d.StartTime, d.EndTime)).ToList(),
            contract.DailyRateCents,
            contract.Consent.PhotosInternal,
            contract.Consent.PhotosWebsite,
            contract.Consent.PhotosSocialMedia,
            contract.Consent.VideoInternal,
            contract.Consent.PhotosPress,
            locale);

        var bytes = await pdfGenerator.GenerateAsync(model, cancellationToken);
        return new GenerateContractPdfResult(true, bytes);
    }
}
