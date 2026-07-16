namespace ChildCare.Application.Common;

/// <summary>
/// Port for rendering a betalingsbewijs (payment receipt) as a PDF. Mirrors
/// IInvoicePdfGenerator's exact port/adapter split — rendered on-demand from the invoice's
/// Paid-state fields, never persisted to storage (research.md R5). A payment confirmation only
/// (KDV identity, child/parent name, invoice reference, amount paid, date paid) — not a
/// tax/legal document (spec.md FR-015).
/// </summary>
public interface IBetalingsbewijsGenerator
{
    Task<byte[]> GenerateAsync(BetalingsbewijsModel model, CancellationToken cancellationToken = default);
}

public record BetalingsbewijsModel(
    string LocationName,
    string LocationAddress,
    string ParentName,
    string ChildName,
    string OgmReference,
    int AmountPaidCents,
    DateTime PaidAt,
    string Locale);
