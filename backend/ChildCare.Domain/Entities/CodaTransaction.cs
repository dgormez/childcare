using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// Feature 025, data-model.md. One row per parsed CODA transaction line.
public class CodaTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ImportId { get; set; }

    public DateOnly ValueDate { get; set; }

    // Signed — negative for a reversal (FR-016).
    public int AmountCents { get; set; }

    // Via IIbanProtector (research.md R2) — same mechanism as Contract.SepaIbanEncrypted.
    public string SenderIbanEncrypted { get; set; } = string.Empty;

    // Plaintext, for display and candidate narrowing without decryption — mirrors
    // Contract.SepaIbanLast4 (feature 024).
    public string SenderIbanLast4 { get; set; } = string.Empty;

    public string SenderName { get; set; } = string.Empty;

    // Raw free-text or the structured message's raw digits, whichever the parser returned.
    public string Communication { get; set; } = string.Empty;

    public Guid? MatchedInvoiceId { get; set; }

    public CodaMatchType MatchType { get; set; } = CodaMatchType.Unmatched;

    // Whether MatchedInvoiceId's paid-status has actually been written (research.md R4/R5,
    // data-model.md's Match Type section).
    public bool Applied { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
