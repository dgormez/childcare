using ChildCare.Application.Common;
using ChildCare.Application.CodaTransactions;
using ChildCare.Domain.Enums;
using Xunit;

namespace ChildCare.Api.Tests.CodaTransactions;

/// <summary>Feature 025, tasks.md T010 — every CodaTransactionMatcher branch.</summary>
public class CodaTransactionMatcherTests
{
    private static CodaParsedTransaction Transaction(int amountCents, bool structured = true, string communication = "123456789012") =>
        new(new DateOnly(2026, 7, 15), amountCents, "BE68539007547034", "Test Sender", communication, structured);

    [Fact]
    public void ExactOgmMatch_AgainstSentInvoice_FullAmount_AppliesImmediately()
    {
        var invoiceId = Guid.NewGuid();
        var candidate = new CodaInvoiceCandidate(invoiceId, TotalCents: 45000, InvoiceStatus.Sent);

        var result = CodaTransactionMatcher.Match(Transaction(45000), candidate, [], null);

        Assert.Equal(CodaMatchType.Ogm, result.MatchType);
        Assert.Equal(invoiceId, result.MatchedInvoiceId);
        Assert.True(result.Applied);
    }

    [Fact]
    public void ExactOgmMatch_AgainstAlreadyPaidInvoice_IsDuplicate_NeverReapplied()
    {
        var invoiceId = Guid.NewGuid();
        var candidate = new CodaInvoiceCandidate(invoiceId, TotalCents: 45000, InvoiceStatus.Paid);

        var result = CodaTransactionMatcher.Match(Transaction(45000), candidate, [], null);

        Assert.Equal(CodaMatchType.Duplicate, result.MatchType);
        Assert.Equal(invoiceId, result.MatchedInvoiceId);
        Assert.False(result.Applied);
    }

    [Fact]
    public void AmountIbanCandidate_ExactlyOne_IsSuggestedButNotApplied()
    {
        var invoiceId = Guid.NewGuid();
        var candidate = new CodaInvoiceCandidate(invoiceId, TotalCents: 45000, InvoiceStatus.Sent);

        var result = CodaTransactionMatcher.Match(Transaction(45000, structured: false), null, [candidate], null);

        Assert.Equal(CodaMatchType.IbanAmount, result.MatchType);
        Assert.Equal(invoiceId, result.MatchedInvoiceId);
        Assert.False(result.Applied);
    }

    [Fact]
    public void AmountIbanCandidate_MultipleMatches_NeverGuesses_LeftUnmatched()
    {
        var candidates = new[]
        {
            new CodaInvoiceCandidate(Guid.NewGuid(), 45000, InvoiceStatus.Sent),
            new CodaInvoiceCandidate(Guid.NewGuid(), 45000, InvoiceStatus.Sent),
        };

        var result = CodaTransactionMatcher.Match(Transaction(45000, structured: false), null, candidates, null);

        Assert.Equal(CodaMatchType.Unmatched, result.MatchType);
        Assert.Null(result.MatchedInvoiceId);
    }

    [Fact]
    public void ClosedInvoiceCandidate_CoincidentalAmountSenderMatch_NeverReopensInvoice()
    {
        var invoiceId = Guid.NewGuid();
        var candidate = new CodaInvoiceCandidate(invoiceId, TotalCents: 45000, InvoiceStatus.Paid);

        var result = CodaTransactionMatcher.Match(Transaction(45000, structured: false), null, [], candidate);

        Assert.Equal(CodaMatchType.ClosedInvoice, result.MatchType);
        Assert.Equal(invoiceId, result.MatchedInvoiceId);
        Assert.False(result.Applied);
    }

    [Fact]
    public void NoCandidateAtAll_IsUnmatched()
    {
        var result = CodaTransactionMatcher.Match(Transaction(45000, structured: false), null, [], null);

        Assert.Equal(CodaMatchType.Unmatched, result.MatchType);
        Assert.Null(result.MatchedInvoiceId);
    }

    [Fact]
    public void NegativeAmount_IsReversal_NeverEligibleForMatching_EvenWithACandidate()
    {
        var candidate = new CodaInvoiceCandidate(Guid.NewGuid(), 45000, InvoiceStatus.Sent);

        var result = CodaTransactionMatcher.Match(Transaction(-4500), candidate, [], null);

        Assert.Equal(CodaMatchType.Reversal, result.MatchType);
        Assert.Null(result.MatchedInvoiceId);
        Assert.False(result.Applied);
    }

    [Fact]
    public void MalformedStructuredReference_FallsThroughToAmountIbanMatching_NotTreatedAsExact()
    {
        // The caller's exact-match lookup already returns null for a reference that doesn't
        // equal any real (checksum-valid) invoice OGM — see CodaTransactionMatcher's own doc
        // comment for why no separate checksum validation is needed here.
        var ibanCandidate = new CodaInvoiceCandidate(Guid.NewGuid(), 45000, InvoiceStatus.Sent);

        var result = CodaTransactionMatcher.Match(
            Transaction(45000, structured: true, communication: "999999999999"),
            exactOgmMatch: null,
            openAmountIbanCandidates: [ibanCandidate],
            closedInvoiceCandidate: null);

        Assert.Equal(CodaMatchType.IbanAmount, result.MatchType);
        Assert.Equal(ibanCandidate.InvoiceId, result.MatchedInvoiceId);
    }

    [Fact]
    public void PartialPayment_ZeroAlreadyReceived_StaysUnapplied()
    {
        var invoiceId = Guid.NewGuid();
        var candidate = new CodaInvoiceCandidate(invoiceId, TotalCents: 50000, InvoiceStatus.Sent);

        var result = CodaTransactionMatcher.Match(Transaction(30000), candidate, [], null, alreadyReceivedCentsForExactMatch: 0);

        Assert.Equal(CodaMatchType.Ogm, result.MatchType);
        Assert.Equal(invoiceId, result.MatchedInvoiceId);
        Assert.False(result.Applied);
    }

    [Fact]
    public void PartialPayment_CombinedWithAlreadyReceived_MeetsTotal_Applies()
    {
        var invoiceId = Guid.NewGuid();
        var candidate = new CodaInvoiceCandidate(invoiceId, TotalCents: 50000, InvoiceStatus.Sent);

        // 30000 already received + this 20000 transaction = 50000, exactly the total.
        var result = CodaTransactionMatcher.Match(Transaction(20000), candidate, [], null, alreadyReceivedCentsForExactMatch: 30000);

        Assert.Equal(CodaMatchType.Ogm, result.MatchType);
        Assert.True(result.Applied);
    }

    [Fact]
    public void PartialPayment_CombinedWithAlreadyReceived_ExceedsTotal_StillApplies()
    {
        var invoiceId = Guid.NewGuid();
        var candidate = new CodaInvoiceCandidate(invoiceId, TotalCents: 50000, InvoiceStatus.Sent);

        var result = CodaTransactionMatcher.Match(Transaction(25000), candidate, [], null, alreadyReceivedCentsForExactMatch: 30000);

        Assert.True(result.Applied);
    }
}
