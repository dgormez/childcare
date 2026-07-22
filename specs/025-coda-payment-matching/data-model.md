# Data Model: CODA/CODABOX Payment Matching

Tenant-schema tables (per-organisation, via `TenantDbContext` — Principle I). No public-schema
changes; no changes to `Invoice` (research.md R5).

## `coda_imports`

One row per uploaded CODA file. Exists purely to give FR-002's summary and FR-013's "N skipped
as already imported" a concrete home, and an audit trail of who uploaded what/when — mirrors
this codebase's general preference for an explicit provenance record over inferring one from
aggregate queries over child rows.

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` PK | |
| `FileName` | `text` | Original uploaded filename, for director-facing display only. |
| `ImportedAt` | `timestamptz` | Server time of upload. |
| `ImportedByUserId` | `uuid` | `TenantUser.Id` of the director who uploaded it. |
| `TransactionCount` | `int` | Total transaction lines parsed from the file. |
| `SkippedDuplicateCount` | `int` | Count of lines recognized as already-imported (FR-013) and not inserted as new `CodaTransaction` rows. |
| `CreatedAt` | `timestamptz` | |

No `Status`/failure column: FR-002's malformed-file case is rejected entirely before any
`coda_imports` row is written — a failed parse never reaches persistence, matching this
project's global error-handling convention (log server-side, show a clean message, no partial
state left behind).

## `coda_transactions`

One row per parsed transaction line, scoped to the import that produced it.

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` PK | |
| `ImportId` | `uuid` FK → `coda_imports(Id)` | |
| `ValueDate` | `date` | `Transaction.ValutaDate` from the parser (research.md R1). |
| `AmountCents` | `int` | Signed — negative for a reversal (FR-016, edge case). |
| `SenderIbanEncrypted` | `text` | Via `IIbanProtector` (research.md R2), same mechanism as `Contract.SepaIbanEncrypted`. |
| `SenderIbanLast4` | `text` | Plaintext, for display and candidate narrowing without decryption — mirrors `Contract.SepaIbanLast4`. |
| `SenderName` | `text` | `Transaction.Account.Name`, plaintext (not classified as sensitive financial identifier the way the account number is). |
| `Communication` | `text` | Raw free-text (`Transaction.Message`) or the structured message's raw digits (`Transaction.StructuredMessage`), whichever the parser returned — kept for director-facing display regardless of match outcome. |
| `MatchedInvoiceId` | `uuid?` FK → `invoices(Id)` | Null for `Unmatched`/`Reversal`. Set (without `Applied`) for a pending `IbanAmount` suggestion; set and `Applied = true` once a match is actually reflected on the invoice. |
| `MatchType` | `text` CHECK | `'Ogm'`, `'IbanAmount'`, `'Unmatched'`, `'Duplicate'`, `'ClosedInvoice'`, `'Reversal'` — see Match Type below. |
| `Applied` | `boolean` NOT NULL DEFAULT `false` | Whether `MatchedInvoiceId`'s paid-status has actually been written (true immediately for `Ogm`; true only after director confirmation for `IbanAmount`; always false for every other `MatchType`). |
| `ReviewedAt` | `timestamptz?` | Set when a director marks an `Unmatched`/`Duplicate`/`ClosedInvoice` row handled (FR-012). Null = still needs attention. |
| `ReviewedByUserId` | `uuid?` | |
| `CreatedAt` | `timestamptz` | |

**Indexes**: `(ImportId)`; `(ValueDate, AmountCents, SenderIbanLast4)` non-unique, for both
FR-013's dedupe narrowing and FR-005's IBAN+amount candidate narrowing (research.md R2) —
exact confirmation always requires decrypting `SenderIbanEncrypted` for the narrowed rows before
treating a candidate as a true match/duplicate, since Data Protection ciphertext isn't
equality-comparable in SQL.

### Match Type

Extends the BACKLOG draft's four-value sketch (`ogm`/`iban_amount`/`manual`/`unmatched`) to the
six states the spec's FRs actually require distinguishing — the `manual` value from that sketch
is folded into `IbanAmount` + `Applied = true` (a director-confirmed suggestion), since no FR
describes an unprompted arbitrary manual link beyond confirming/rejecting a system suggestion
(FR-006):

- **`Ogm`** — FR-004. Exact structured-reference match at import time. `Applied = true`
  immediately.
- **`IbanAmount`** — FR-005/FR-006. `Applied = false` until the director confirms (FR-006 →
  `Applied = true`, invoice marked paid) or the row moves to `Unmatched` on rejection.
- **`Unmatched`** — FR-007. No reference match, no unambiguous amount+IBAN candidate (or FR-005a's
  multiple-candidate case), or a rejected `IbanAmount` suggestion.
- **`Duplicate`** — FR-008. Exact reference match against an invoice already `Paid`.
- **`ClosedInvoice`** — FR-009. Amount+sender coincidentally matches an already-`Paid` invoice
  from an earlier billing period (a dedicated check against `Paid` invoices, distinct from
  FR-005's suggestion search which only considers `Sent` invoices).
- **`Reversal`** — FR-016. Negative amount; never eligible for matching.

### Partial payment

Not a `MatchType` value — a partial payment is simply an `Ogm` or confirmed `IbanAmount` row
whose `AmountCents` is less than the matched invoice's `TotalCents` at read time (research.md
R5). `Applied` stays `false` for these (the invoice is never marked `Paid` on a partial amount —
FR-010); the outstanding balance is `Invoice.TotalCents` minus the sum of that invoice's applied-
or-partial `CodaTransaction.AmountCents`.

## Relationships

```text
coda_imports (1) ──< (N) coda_transactions
invoices (1, feature 014) ──< (0..N) coda_transactions [MatchedInvoiceId]
```

`MatchedInvoiceId` has no DB-level FK-driven cascade behavior beyond standard referential
integrity — an invoice is never deleted in this codebase (matches every other feature's
append-only financial-record posture), so no orphan-handling logic is needed.

## Validation rules

- `AmountCents` MUST NOT be zero (a zero-amount CODA line is not a real transaction; parser-level
  guard, not a UI concern).
- `MatchedInvoiceId` MUST be null when `MatchType` is `Unmatched` or `Reversal`.
- `Applied = true` MUST only ever coexist with `MatchType` in (`Ogm`, `IbanAmount`) and a non-null
  `MatchedInvoiceId` — enforced in the command handlers (FluentValidation covers the request
  shape; this invariant is a handler-level guard mirroring `MarkInvoicePaidCommand`'s own
  `NotSent` guard, not something a CHECK constraint can express given the cross-column
  dependency).
