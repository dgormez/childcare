using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

public class Tenant
{
    public Guid   Id                     { get; set; } = Guid.NewGuid();
    public string Name                   { get; set; } = string.Empty;
    public string Slug                   { get; set; } = string.Empty;
    public string SchemaName             { get; set; } = string.Empty;
    public PlanTier Plan                 { get; set; } = PlanTier.Trial;
    public ProvisioningStatus ProvisioningStatus { get; set; } = ProvisioningStatus.Provisioning;

    // Guards FR-015 (at most one organisation per invitation) and is the sole
    // source of truth for "already used" (FR-004) — see research.md R10.
    public Guid   CreatedFromInvitationId { get; set; }

    // Feature 014 — Belgian company registration number, org-wide (one legal entity regardless
    // of location count), printed on every invoice PDF.
    public string? KboNumber             { get; set; }

    // Feature 024-esignature — the organisation's own SEPA Creditor Identifier (issued by the
    // National Bank of Belgium), director-entered once. Required before any contract signing
    // invitation can be sent (FR-016); distinct from a per-signing SepaMandateReference (Contract).
    public string? SepaCreditorIdentifier { get; set; }

    public DateTime CreatedAt            { get; set; } = DateTime.UtcNow;
}
