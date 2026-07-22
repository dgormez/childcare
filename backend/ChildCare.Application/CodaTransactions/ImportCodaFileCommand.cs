using ChildCare.Application.Common;
using ChildCare.Application.Invoices;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.CodaTransactions;

// Feature 025 — contracts/coda-payment-matching-api.md, spec.md FR-001..FR-016.
public record ImportCodaFileCommand(Stream FileStream, string FileName, Guid ImportedByUserId) : IRequest<ImportCodaFileResult>;

public enum ImportCodaFileFailure { ParseFailed }

public class ImportCodaFileResult
{
    public CodaImportSummaryResponse? Response { get; private init; }
    public ImportCodaFileFailure? Failure { get; private init; }
    public bool Succeeded => Failure is null;

    public static ImportCodaFileResult Success(CodaImportSummaryResponse response) => new() { Response = response };
    public static ImportCodaFileResult Fail(ImportCodaFileFailure failure) => new() { Failure = failure };
}

public class ImportCodaFileCommandValidator : AbstractValidator<ImportCodaFileCommand>
{
    public ImportCodaFileCommandValidator()
    {
        RuleFor(x => x.FileName).NotEmpty();
    }
}

public class ImportCodaFileCommandHandler(
    ITenantDbContext db,
    ICodaParser codaParser,
    ICodaSenderIbanProtector senderIbanProtector,
    IIbanProtector contractIbanProtector,
    IMediator mediator,
    ILogger<ImportCodaFileCommandHandler> logger)
    : IRequestHandler<ImportCodaFileCommand, ImportCodaFileResult>
{
    public async Task<ImportCodaFileResult> Handle(ImportCodaFileCommand request, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        using (var reader = new StreamReader(request.FileStream))
        {
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
                lines.Add(line);
        }

        IReadOnlyList<CodaParsedTransaction> parsed;
        try
        {
            parsed = codaParser.Parse(lines);
        }
        catch (CodaParseException)
        {
            return ImportCodaFileResult.Fail(ImportCodaFileFailure.ParseFailed);
        }

        var import = new CodaImport
        {
            FileName = request.FileName,
            ImportedByUserId = request.ImportedByUserId,
            TransactionCount = parsed.Count,
        };

        var counts = new Dictionary<CodaMatchType, int>();
        var skippedDuplicateCount = 0;

        foreach (var transaction in parsed)
        {
            if (await IsAlreadyImportedAsync(transaction, cancellationToken))
            {
                skippedDuplicateCount++;
                continue;
            }

            var matchResult = await MatchAndApplyAsync(transaction, cancellationToken);
            counts[matchResult.MatchType] = counts.GetValueOrDefault(matchResult.MatchType) + 1;

            var ibanNormalized = transaction.SenderIban.Replace(" ", string.Empty).ToUpperInvariant();
            db.CodaTransactions.Add(new CodaTransaction
            {
                ImportId = import.Id,
                ValueDate = transaction.ValueDate,
                AmountCents = transaction.AmountCents,
                SenderIbanEncrypted = senderIbanProtector.Protect(ibanNormalized),
                SenderIbanLast4 = ibanNormalized.Length >= 4 ? ibanNormalized[^4..] : ibanNormalized,
                SenderName = transaction.SenderName,
                Communication = transaction.Communication,
                MatchedInvoiceId = matchResult.MatchedInvoiceId,
                MatchType = matchResult.MatchType,
                Applied = matchResult.Applied,
            });
        }

        import.SkippedDuplicateCount = skippedDuplicateCount;
        db.CodaImports.Add(import);
        await db.SaveChangesAsync(cancellationToken);

        var summary = new CodaImportSummaryCountsResponse(
            Ogm: counts.GetValueOrDefault(CodaMatchType.Ogm),
            IbanAmountSuggested: counts.GetValueOrDefault(CodaMatchType.IbanAmount),
            Unmatched: counts.GetValueOrDefault(CodaMatchType.Unmatched),
            Duplicate: counts.GetValueOrDefault(CodaMatchType.Duplicate),
            ClosedInvoice: counts.GetValueOrDefault(CodaMatchType.ClosedInvoice),
            Reversal: counts.GetValueOrDefault(CodaMatchType.Reversal));

        return ImportCodaFileResult.Success(new CodaImportSummaryResponse(import.Id, import.TransactionCount, skippedDuplicateCount, summary));
    }

    // FR-013 (spec.md Clarifications) — a composite natural key: value date, amount, sender
    // IBAN, and communication text, all together. SenderIbanLast4 narrows the SQL query;
    // SenderIbanEncrypted's ciphertext isn't equality-comparable (research.md R2), so the
    // narrowed candidates are decrypted in memory to confirm true IBAN equality.
    private async Task<bool> IsAlreadyImportedAsync(CodaParsedTransaction transaction, CancellationToken cancellationToken)
    {
        var ibanNormalized = transaction.SenderIban.Replace(" ", string.Empty).ToUpperInvariant();
        var last4 = ibanNormalized.Length >= 4 ? ibanNormalized[^4..] : ibanNormalized;

        var candidates = await db.CodaTransactions
            .Where(t => t.ValueDate == transaction.ValueDate
                     && t.AmountCents == transaction.AmountCents
                     && t.SenderIbanLast4 == last4
                     && t.Communication == transaction.Communication)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return false;

        LogIbanAccess(candidates.Count, "dedupe-check");
        return candidates.Any(c => senderIbanProtector.Unprotect(c.SenderIbanEncrypted) == ibanNormalized);
    }

    private async Task<CodaMatchResult> MatchAndApplyAsync(CodaParsedTransaction transaction, CancellationToken cancellationToken)
    {
        var exactOgmInvoice = transaction.IsStructuredCommunication
            ? await FindExactOgmMatchAsync(transaction.Communication, cancellationToken)
            : null;

        var alreadyReceivedCents = 0;
        CodaInvoiceCandidate? exactOgmCandidate = null;
        if (exactOgmInvoice is not null)
        {
            exactOgmCandidate = new CodaInvoiceCandidate(exactOgmInvoice.Id, exactOgmInvoice.TotalCents, exactOgmInvoice.Status);
            if (exactOgmInvoice.Status == InvoiceStatus.Sent)
            {
                alreadyReceivedCents = await db.CodaTransactions
                    .Where(t => t.MatchedInvoiceId == exactOgmInvoice.Id && t.MatchType == CodaMatchType.Ogm)
                    .SumAsync(t => (int?)t.AmountCents, cancellationToken) ?? 0;
            }
        }

        IReadOnlyList<CodaInvoiceCandidate> openAmountIbanCandidates = [];
        CodaInvoiceCandidate? closedInvoiceCandidate = null;

        // Only search amount+IBAN candidates when there's no exact reference resolution — an
        // exact match (even one landing on Duplicate) always takes precedence (FR-004/FR-008).
        if (exactOgmCandidate is null && transaction.AmountCents > 0)
        {
            var ibanNormalized = transaction.SenderIban.Replace(" ", string.Empty).ToUpperInvariant();
            var last4 = ibanNormalized.Length >= 4 ? ibanNormalized[^4..] : ibanNormalized;

            var openCandidates = await FindAmountIbanCandidatesAsync(transaction.AmountCents, last4, InvoiceStatus.Sent, null, cancellationToken);
            var confirmedOpen = new List<CodaInvoiceCandidate>();
            if (openCandidates.Count > 0)
            {
                LogIbanAccess(openCandidates.Count, "open-invoice-suggestion-search");
                foreach (var (invoiceId, totalCents, status, encryptedIban) in openCandidates)
                    if (contractIbanProtector.Unprotect(encryptedIban) == ibanNormalized)
                        confirmedOpen.Add(new CodaInvoiceCandidate(invoiceId, totalCents, status));
            }
            openAmountIbanCandidates = confirmedOpen;

            // FR-009 — only relevant once there's no open-invoice suggestion at all (an
            // unambiguous open candidate always wins; FR-005a's multi-candidate case is
            // "unmatched", not "closed invoice" — a closed-invoice payment is specifically one
            // that lines up with NO open invoice, only an already-closed one).
            if (confirmedOpen.Count == 0)
            {
                var closedCandidates = await FindAmountIbanCandidatesAsync(transaction.AmountCents, last4, InvoiceStatus.Paid, transaction.ValueDate, cancellationToken);
                if (closedCandidates.Count > 0)
                {
                    LogIbanAccess(closedCandidates.Count, "closed-invoice-check");
                    var match = closedCandidates.FirstOrDefault(c => contractIbanProtector.Unprotect(c.EncryptedIban) == ibanNormalized);
                    if (match != default)
                        closedInvoiceCandidate = new CodaInvoiceCandidate(match.InvoiceId, match.TotalCents, match.Status);
                }
            }
        }

        var result = CodaTransactionMatcher.Match(transaction, exactOgmCandidate, openAmountIbanCandidates, closedInvoiceCandidate, alreadyReceivedCents);

        if (result.Applied && result.MatchedInvoiceId is not null)
            await mediator.Send(new MarkInvoicePaidCommand(result.MatchedInvoiceId.Value, transaction.ValueDate), cancellationToken);

        return result;
    }

    private async Task<Invoice?> FindExactOgmMatchAsync(string communicationDigits, CancellationToken cancellationToken)
    {
        var formatted = TryFormatAsOgmReference(communicationDigits);
        return formatted is null
            ? null
            : await db.Invoices.FirstOrDefaultAsync(i => i.OgmReference == formatted, cancellationToken);
    }

    // The reverse of OgmReferenceGenerator.Generate — reformats a raw 12-digit structured
    // communication into the same "+++XXX/XXXX/XXXXX+++" display form Invoice.OgmReference is
    // stored in, so the two can be compared with a plain equality query. Returns null for
    // anything that isn't exactly 12 digits (a non-OGM structured message type, e.g. SEPA direct
    // debit's type 127) — such a value simply cannot equal any real invoice reference.
    private static string? TryFormatAsOgmReference(string communicationDigits) =>
        communicationDigits.Length == 12 && communicationDigits.All(char.IsDigit)
            ? $"+++{communicationDigits[..3]}/{communicationDigits[3..7]}/{communicationDigits[7..12]}+++"
            : null;

    private async Task<List<(Guid InvoiceId, int TotalCents, InvoiceStatus Status, string EncryptedIban)>> FindAmountIbanCandidatesAsync(
        int amountCents, string senderIbanLast4, InvoiceStatus status, DateOnly? beforePeriodOf, CancellationToken cancellationToken)
    {
        var query = db.Invoices
            .Where(i => i.TotalCents == amountCents && i.Status == status)
            .Join(db.Contracts, i => i.ContractId, c => c.Id, (i, c) => new { Invoice = i, Contract = c })
            .Where(x => x.Contract.SepaIbanLast4 == senderIbanLast4 && x.Contract.SepaIbanEncrypted != null);

        if (beforePeriodOf is not null)
        {
            var currentPeriodStart = new DateOnly(beforePeriodOf.Value.Year, beforePeriodOf.Value.Month, 1);
            query = query.Where(x => x.Invoice.PeriodMonth < currentPeriodStart);
        }

        var rows = await query
            .Select(x => new { x.Invoice.Id, x.Invoice.TotalCents, x.Invoice.Status, x.Contract.SepaIbanEncrypted })
            .ToListAsync(cancellationToken);

        return rows.Select(x => (x.Id, x.TotalCents, x.Status, x.SepaIbanEncrypted!)).ToList();
    }

    private void LogIbanAccess(int candidateCount, string reason) =>
        logger.LogInformation("Decrypted {Count} sender IBAN(s) for CODA matching ({Reason}).", candidateCount, reason);
}
