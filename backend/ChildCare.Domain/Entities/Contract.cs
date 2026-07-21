using ChildCare.Domain.Enums;
using ChildCare.Domain.ValueObjects;

namespace ChildCare.Domain.Entities;

public class Contract
{
    public Guid Id                 { get; set; } = Guid.NewGuid();
    public Guid ChildId            { get; set; }
    public Guid LocationId         { get; set; }

    // Set on the successor contract created by an amendment (research.md R5); null for a
    // fresh first-ever contract or one with no predecessor.
    public Guid? PreviousContractId { get; set; }

    public DateOnly  StartDate { get; set; }
    public DateOnly? EndDate   { get; set; }

    public List<ContractedDay> ContractedDays { get; set; } = [];

    public int DailyRateCents { get; set; }

    public ContractStatus Status { get; set; } = ContractStatus.Draft;

    public ContractConsent Consent { get; set; } = new();

    // Reserved for Phase 3 IKT subsidy-rate support — always null in this feature (FR-013).
    public string?   TariefCode     { get; set; }
    public DateOnly? RateValidUntil { get; set; }

    // Feature 024-esignature. The *current* valid signing token's value (not the cryptographic
    // secret itself — see IContractSigningTokenService) — null means no outstanding invitation.
    // Cleared/replaced on resend, on revision (FR-013), and on successful signing (FR-009).
    public string?   SigningToken           { get; set; }
    public DateTime? SigningTokenExpiresAt  { get; set; }

    // Set once, atomically with the SigningToken clear (FR-009) — non-null is the source of
    // truth for "this contract is signed." Signing is additive to Draft/Active/Ended (FR-015):
    // it never changes Status.
    public DateTime?      SignedAt       { get; set; }
    public string?        SignatureData  { get; set; }
    public SignatureType? SignatureType  { get; set; }
    public string?        SignedByIp     { get; set; }

    // SEPA direct debit mandate, captured in the same signing session (FR-007). IBAN is
    // encrypted at rest via IIbanProtector — see data-model.md. SepaIbanLast4 is stored
    // separately, in plaintext, purely so director-facing reads (FR-020's masked display) never
    // need to decrypt the full IBAN just to show "•••• 0166" — mirrors this codebase's general
    // avoidance of needless decryption on read paths.
    public string?   SepaIbanEncrypted     { get; set; }
    public string?   SepaIbanLast4         { get; set; }
    public string?   SepaMandateReference  { get; set; }
    public DateTime? SepaAuthorisedAt      { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
