using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.SepaBatches;

// Feature 026 — contracts/sepa-direct-debit-api.md, spec.md FR-002 through FR-007, FR-013.
public record GenerateSepaBatchCommand(
    Guid LocationId,
    IReadOnlyList<Guid> InvoiceIds,
    DateOnly ExecutionDate,
    Guid GeneratedByUserId) : IRequest<GenerateSepaBatchResult>;

public enum GenerateSepaBatchFailure
{
    NoInvoicesSelected,
    ExecutionDateTooSoon,
    CreditorNotConfigured,
    InvoiceNotEligible,
    GenerationFailed,
}

public class GenerateSepaBatchResult
{
    public byte[]? Xml { get; private init; }
    public Guid? BatchId { get; private init; }
    public GenerateSepaBatchFailure? Failure { get; private init; }
    public DateOnly? MinimumExecutionDate { get; private init; }
    public bool Succeeded => Failure is null;

    public static GenerateSepaBatchResult Success(byte[] xml, Guid batchId) => new() { Xml = xml, BatchId = batchId };
    public static GenerateSepaBatchResult Fail(GenerateSepaBatchFailure failure, DateOnly? minimumExecutionDate = null) =>
        new() { Failure = failure, MinimumExecutionDate = minimumExecutionDate };
}

public class GenerateSepaBatchCommandValidator : AbstractValidator<GenerateSepaBatchCommand> { }

public class GenerateSepaBatchCommandHandler(
    ITenantDbContext db,
    IPublicDbContext publicDb,
    ICurrentTenantService currentTenant,
    IIbanProtector ibanProtector,
    ISepaBatchXmlGenerator xmlGenerator,
    ILogger<GenerateSepaBatchCommandHandler> logger)
    : IRequestHandler<GenerateSepaBatchCommand, GenerateSepaBatchResult>
{
    public async Task<GenerateSepaBatchResult> Handle(GenerateSepaBatchCommand request, CancellationToken cancellationToken)
    {
        if (request.InvoiceIds.Count == 0)
            return GenerateSepaBatchResult.Fail(GenerateSepaBatchFailure.NoInvoicesSelected);

        var minimumExecutionDate = MinimumExecutionDate(DateOnly.FromDateTime(DateTime.UtcNow));
        if (request.ExecutionDate < minimumExecutionDate)
            return GenerateSepaBatchResult.Fail(GenerateSepaBatchFailure.ExecutionDateTooSoon, minimumExecutionDate);

        var location = await db.Locations.FirstAsync(l => l.Id == request.LocationId, cancellationToken);
        var tenant = await publicDb.Tenants.FirstAsync(t => t.Id == currentTenant.TenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(tenant.SepaCreditorIdentifier) || string.IsNullOrWhiteSpace(location.BankAccountNumber))
            return GenerateSepaBatchResult.Fail(GenerateSepaBatchFailure.CreditorNotConfigured);

        // Re-validate every requested invoice server-side — never trust the client's earlier
        // eligibility read (spec.md FR-002/data-model.md's Eligibility rule). AsNoTracking is
        // deliberate: this context also runs LockInvoicesForUpdateAsync's row-locked read further
        // down, inside the transaction — if these Invoice instances were tracked here first, EF's
        // identity resolution would return the SAME (now-stale) in-memory instance from that later
        // query instead of the fresh, lock-guaranteed database value, silently defeating the whole
        // concurrency guard (found by this feature's own concurrent-request test, tasks.md T016a).
        var rows = await db.Invoices.AsNoTracking()
            .Where(i => request.InvoiceIds.Contains(i.Id) && i.LocationId == request.LocationId)
            .Join(db.Contracts.AsNoTracking(), i => i.ContractId, c => c.Id, (i, c) => new { Invoice = i, Contract = c })
            .ToListAsync(cancellationToken);

        if (rows.Count != request.InvoiceIds.Count
            || rows.Any(r => r.Invoice.Status != InvoiceStatus.Sent
                           || r.Contract.SepaAuthorisedAt is null
                           || r.Contract.SepaRevokedAt is not null
                           || r.Invoice.TotalCents <= 0
                           || string.IsNullOrEmpty(r.Contract.SepaIbanEncrypted)))
            return GenerateSepaBatchResult.Fail(GenerateSepaBatchFailure.InvoiceNotEligible);

        // FR-006a — a corrupted/undecryptable IBAN aborts the whole batch before any persistence,
        // never a silent partial drop.
        var instructions = new List<SepaDebitInstruction>();
        try
        {
            foreach (var row in rows)
            {
                var debtorIban = ibanProtector.Unprotect(row.Contract.SepaIbanEncrypted!);
                logger.LogInformation(
                    "Decrypted contract IBAN for SEPA batch generation (ContractId={ContractId}, InvoiceId={InvoiceId}).",
                    row.Contract.Id, row.Invoice.Id);

                var debtorName = await GetSepaBatchEligibilityQueryHandler.ResolveDebtorNameAsync(db, row.Invoice.ChildId, cancellationToken);
                var sequenceType = await ResolveSequenceTypeAsync(row.Contract, cancellationToken);

                instructions.Add(new SepaDebitInstruction(
                    EndToEndId: row.Invoice.OgmReference,
                    AmountCents: row.Invoice.TotalCents,
                    DebtorIban: debtorIban,
                    DebtorName: debtorName,
                    MandateReference: row.Contract.SepaMandateReference!,
                    MandateSigningDate: DateOnly.FromDateTime(row.Contract.SepaAuthorisedAt!.Value),
                    SequenceType: sequenceType));
            }
        }
        catch (Exception ex) when (ex is not SepaBatchGenerationException)
        {
            logger.LogError(ex, "Failed to decrypt a debtor IBAN during SEPA batch generation for location {LocationId}.", request.LocationId);
            return GenerateSepaBatchResult.Fail(GenerateSepaBatchFailure.GenerationFailed);
        }

        var batchId = Guid.NewGuid();
        SepaBatchXmlResult xmlResult;
        try
        {
            xmlResult = xmlGenerator.Generate(
                new SepaBatchCreditor(tenant.SepaCreditorIdentifier!, tenant.Name, location.BankAccountNumber!),
                instructions,
                request.ExecutionDate,
                // "N" format (32 hex chars, no hyphens) — MsgId/PmtInfId are Max35Text; the
                // default GUID format (36 chars with hyphens) exceeds that by one character.
                batchId.ToString("N"));
        }
        catch (SepaBatchGenerationException ex)
        {
            logger.LogError(ex, "SEPA batch XML failed schema validation for location {LocationId}.", request.LocationId);
            return GenerateSepaBatchResult.Fail(GenerateSepaBatchFailure.GenerationFailed);
        }

        // FR-007 — the invoice claim and the batch record are a single all-or-nothing outcome.
        // FR-013/CHK003 — LockInvoicesForUpdateAsync re-selects every invoice with a row lock
        // inside this transaction before claiming it: a concurrent request racing for the same
        // invoice blocks on that lock until this transaction commits or rolls back, then re-reads
        // the row and correctly sees it's no longer Sent — a genuine DB-level guard, not a
        // read-then-write race.
        var invoiceIds = rows.Select(r => r.Invoice.Id).ToArray();
        var claimed = await db.ExecuteInTransactionAsync(async ct =>
        {
            var lockedInvoices = await db.LockInvoicesForUpdateAsync(invoiceIds, ct);

            if (lockedInvoices.Count != invoiceIds.Length || lockedInvoices.Any(i => i.Status != InvoiceStatus.Sent))
                return false;

            var batch = new SepaBatch
            {
                Id = batchId,
                LocationId = request.LocationId,
                ExecutionDate = request.ExecutionDate,
                GeneratedByUserId = request.GeneratedByUserId,
                TotalCents = xmlResult.TotalCents,
                InvoiceCount = xmlResult.InvoiceCount,
            };
            db.SepaBatches.Add(batch);

            foreach (var invoice in lockedInvoices)
            {
                var mandateReferenceUsed = rows.First(r => r.Invoice.Id == invoice.Id).Contract.SepaMandateReference;
                invoice.Status = InvoiceStatus.PendingDebit;
                invoice.SepaBatchId = batchId;
                invoice.SepaMandateReferenceUsed = mandateReferenceUsed;
                invoice.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        if (!claimed)
            return GenerateSepaBatchResult.Fail(GenerateSepaBatchFailure.InvoiceNotEligible);

        return GenerateSepaBatchResult.Success(xmlResult.Xml, batchId);
    }

    // FR-005 — Monday-Friday only (spec.md Clarifications), independent of the closure calendar
    // (011) or any Belgian public-holiday calendar (none exists in this codebase).
    private static DateOnly MinimumExecutionDate(DateOnly today)
    {
        var candidate = today.AddDays(1);
        while (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            candidate = candidate.AddDays(1);
        return candidate;
    }

    // research.md R3 — FRST if no invoice for this contract has ever been generated into a batch
    // under its current mandate reference (the immutable SepaMandateReferenceUsed snapshot, not
    // the live, clearable SepaBatchId), RCUR otherwise.
    private async Task<string> ResolveSequenceTypeAsync(Contract contract, CancellationToken cancellationToken)
    {
        var alreadyUsed = await db.Invoices.AnyAsync(
            i => i.ContractId == contract.Id && i.SepaMandateReferenceUsed == contract.SepaMandateReference,
            cancellationToken);
        return SepaSequenceTypeResolver.Resolve(alreadyUsed);
    }
}
