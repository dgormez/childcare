using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// Feature 014a (data-model.md) — one row per payment ATTEMPT against an invoice. Lives in the
// public schema, not the tenant schema: the webhook (research.md R2) must resolve
// TenantId/InvoiceId from PaymentReference alone, before any tenant context exists — this
// table IS that resolution index. InvoiceId refers to a tenant-schema row; deliberately no
// cross-schema FK, same posture as VaccineRecord.VaccineTypeId (013g).
public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // System-generated, unique, opaque — the webhook URL path segment (research.md R2). Never
    // derived from the OGM reference, which is only unique within a tenant schema.
    public Guid PaymentReference { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }

    // Null only in the brief window between this row's creation and Mollie's create-payment
    // API response.
    public string? ProviderPaymentId { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Open;

    // The invoice's outstanding amount at creation time — never mutates Invoice.TotalCents
    // (spec.md FR-011).
    public int AmountCents { get; set; }

    // PSP fee, recorded once Mollie reports it — separate from AmountCents/the invoice total.
    public int? FeeCents { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
