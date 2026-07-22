namespace ChildCare.Domain.Enums;

// Feature 025, data-model.md. Extends the BACKLOG draft's four-value sketch to the six states
// spec.md's FRs actually distinguish — see data-model.md's "Match Type" section for the mapping.
public enum CodaMatchType
{
    Ogm,
    IbanAmount,
    Unmatched,
    Duplicate,
    ClosedInvoice,
    Reversal,
}
