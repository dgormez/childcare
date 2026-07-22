namespace ChildCare.Application.SepaBatches;

/// <summary>
/// Feature 026, FR-002a / research.md R3. Pure logic — unit-testable independent of MediatR/EF
/// Core, mirrors CodaTransactionMatcher's own pure-function shape (feature 025). The caller
/// resolves whether any invoice for this contract already carries the given mandate reference in
/// its immutable SepaMandateReferenceUsed snapshot (never the live, clearable SepaBatchId — see
/// data-model.md and research.md R3's Rationale for why that distinction matters across a
/// returned debit and a revoke-and-resign).
/// </summary>
public static class SepaSequenceTypeResolver
{
    public const string First = "FRST";
    public const string Recurring = "RCUR";

    public static string Resolve(bool mandateReferenceAlreadyUsed) =>
        mandateReferenceAlreadyUsed ? Recurring : First;
}
