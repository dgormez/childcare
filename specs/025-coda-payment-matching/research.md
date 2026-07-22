# Research: CODA/CODABOX Payment Matching

## R1 — CODA parsing library

**Decision**: Use the `CodaParser` NuGet package (https://github.com/phmatray/coda-parser,
`Install-Package CodaParser`), a fork of `supervos/coda-parser`.

**Rationale**: The BACKLOG prompt explicitly requires reusing an existing .NET CODA parser
rather than writing one from scratch. Only two real Belgian-CODA .NET libraries exist on NuGet:
`CodaParser` (GPL-2.0) and `Neuroglia.Data.Coda` (GPL-3.0) — no MIT/Apache-licensed option
exists. This was flagged to the product owner given this codebase's existing MIT-only posture
for third-party libraries (constitution's Technology Stack Constraints: "QuestPDF (MIT licence)
— no other PDF library"). Resolved 2026-07-22: ChildCare is a pure multi-tenant SaaS with no
on-prem/distributed deployment of the backend — under plain GPLv2/GPLv3 (not AGPL), the
copyleft "distribution" trigger requires conveying a copy of the software to a third party,
which running your own server for a network service does not do. Both libraries are plain GPL,
not AGPL, so referencing `CodaParser` from `ChildCare.Api`/`ChildCare.Infrastructure` carries no
meaningful copyleft exposure under the current SaaS-only distribution model. **Revisit this
decision if the product's distribution model ever changes** (an on-prem/self-hosted SKU, or
open-sourcing any part of this codebase under incompatible terms).

`CodaParser`'s model directly exposes everything this feature needs from a transaction line —
confirmed from source (`src/library/CodaParser/Statements/Transaction.cs`,
`AccountOtherParty.cs`, `Values/StructuredMessage.cs`):

- `Transaction.ValutaDate` (value date), `Transaction.Amount` (decimal).
- `Transaction.Account.Number` (counterparty IBAN), `Transaction.Account.Name`.
- `Transaction.Message` (free-text communication) vs `Transaction.StructuredMessage` (populated
  only when the CODA line's communication-type bit marks it structured; type "101"/"102" yields
  a 12-digit numeric value — exactly the same 12 digits this codebase's own
  `OgmReferenceGenerator` produces for `Invoice.OgmReference`, just without the `+++XXX/XXXX/
  XXXXX+++` display formatting). Matching FR-004 therefore compares
  `Transaction.StructuredMessage` (digits only) against `Invoice.OgmReference` with the display
  punctuation stripped, not the other way around.
- `Parser.ParseFile(path)` / `Parser.Parse(IEnumerable<string> lines)` returns
  `IEnumerable<Statement>`, each with a `Transactions` collection — a malformed/non-CODA file
  throws from within the library, which the command handler wraps into FR-002's clean
  human-readable rejection rather than letting a raw parser exception surface (Principle VI /
  this project's global error-handling convention).

**Alternatives considered**: `Neuroglia.Data.Coda` — GPL-3.0 (stricter than GPL-2.0, no material
advantage for this use case) and shows minimal maintenance activity (6 commits, no releases).
Writing a from-scratch parser against the published Belgian CODA fixed-width spec — rejected
per the BACKLOG's explicit instruction and because `CodaParser`'s model already matches this
feature's needs field-for-field, making a custom parser pure unjustified effort once the license
concern was resolved.

## R2 — IBAN storage and equality matching

**Decision**: Reuse `IIbanProtector` (`ChildCare.Application.Common`, ASP.NET Core Data
Protection-backed, feature 024) unchanged, under its own purpose string. Store
`SenderIbanEncrypted` (ciphertext) + `SenderIbanLast4` (plaintext) on `CodaTransaction`,
mirroring `Contract.SepaIbanEncrypted`/`SepaIbanLast4` exactly.

**Rationale**: ASP.NET Core Data Protection's `Protect()` output is not deterministic (random
entropy per call) — the same IBAN encrypted twice produces different ciphertext, so
`SenderIbanEncrypted` cannot be used directly in a SQL equality/index lookup for either FR-005's
amount+IBAN matching or FR-013's re-import dedupe. This codebase already solves the identical
problem for `Contract.SepaIbanEncrypted` by also storing a plaintext last-4-digits column
purely for narrowing/display without decryption (`Contract.cs`'s own comment: "purely so
director-facing reads never need to decrypt the full IBAN"). This feature reuses that exact
pattern: `SenderIbanLast4` narrows candidates via an indexed query, and only the narrowed
candidate set is decrypted (via `IIbanProtector.Unprotect`) to confirm true equality before
being treated as a match or a duplicate. At this codebase's per-tenant transaction/contract
volumes (one monthly statement's worth of rows), decrypting a narrowed candidate set is cheap —
no different in cost class from existing per-request decryption call sites (014a's Mollie token
usage, 024's IBAN display).

**Alternatives considered**: A deterministic HMAC "blind index" column for true DB-level
uniqueness — rejected as unnecessary complexity given this codebase's existing last4-narrow-then-
decrypt-confirm pattern already solves the problem at this feature's actual scale, and
introducing a second IBAN-protection mechanism alongside `IIbanProtector` would fragment this
codebase's established one-purpose-string-per-feature encryption convention for no real benefit.

## R3 — Where a family's IBAN actually lives

**Decision**: FR-005's "family whose IBAN matches the sender" resolves technically to: the
`Contract` linked to each open invoice under consideration (`Invoice.ContractId` →
`Contract.SepaIbanEncrypted`/`SepaIbanLast4`). There is no separate `Family`/`PrimaryContact`
entity with its own IBAN field anywhere in this codebase.

**Rationale**: Searched the full domain model — the only IBAN capture point is
`Contract.SepaIbanEncrypted`/`SepaIbanLast4`, populated optionally during e-signature's SEPA
mandate step (feature 024). This means amount+IBAN suggested matching (FR-005) is only possible
for open invoices whose contract has a SEPA mandate on file; invoices without one can only ever
be matched via FR-004's exact OGM reference, falling to FR-007's unmatched-review list
otherwise. This is a direct consequence of what data actually exists, not a product decision
requiring a clarification — documented here so `/speckit-implement` doesn't need to rediscover
it, and so a director isn't surprised that some families' payments never get a suggested match.

**Alternatives considered**: None — this is a factual finding about the existing data model, not
a design choice between competing approaches.

## R4 — Invoice-paid transition and family bundling (feature 030)

**Decision**: Route every "mark this invoice paid" action (FR-004's exact match and FR-006's
confirmed suggestion) through the existing `MarkInvoicePaidCommand`
(`ChildCare.Application/Invoices/MarkInvoicePaidCommand.cs`), unchanged.

**Rationale**: `MarkInvoicePaidCommand` already implements everything this feature needs for
free: the one-way `Sent → Paid` transition (satisfies FR-008's "only ever marked paid once" —
the handler itself rejects a non-`Sent` invoice), feature 030's sibling-invoice cascade for
bundled family invoices sharing a `FamilyGroupId`, and `PaymentReceiptNotificationService`
invocation (feature 014a) for both the primary and cascaded sibling invoices. Reusing it means
this feature does not need to reimplement any of that — including getting the family-bundling
cascade right, which 030's own shipped-notes record as a place a webhook/manual-path parity gap
previously caused a real bug (a payment amount mismatch across siblings). This feature's own
matching logic is responsible only for finding the right `InvoiceId` and the paid amount/date to
pass in; the transition and its side effects are `MarkInvoicePaidCommand`'s job, not
duplicated here.

**Alternatives considered**: A CODA-specific paid-transition path — rejected; it would either
have to reimplement the sibling cascade and receipt notification (duplication risk) or leave
family-bundled invoices half-updated (the exact class of bug 030 already found and fixed once).

## R5 — Partial payment representation

**Decision**: No new column on `Invoice`. A partial payment is represented purely by a
`CodaTransaction` row with `MatchedInvoiceId` set, `Applied = false` (see data-model.md), and
`AmountCents` less than the invoice's outstanding total. The outstanding/received total for a
given invoice is computed at read time by summing `AmountCents` across that invoice's applied
and partial `CodaTransaction` rows — not stored redundantly on `Invoice`.

**Rationale**: Keeps `Invoice` (feature 014) entirely unchanged — no schema migration to an
entity three other features (014a, 015, 030) already depend on — and avoids a second source of
truth for "how much has been received" that could drift from the transaction rows themselves.
This mirrors how `Payment` (014a) already keeps PSP payment attempts in their own table rather
than mutating `Invoice` beyond its existing `Status`/`PaidAt` fields.

**Alternatives considered**: A `ReceivedCents` running-total column on `Invoice` — rejected as an
unnecessary denormalization for a value trivially computable from `CodaTransaction` at this
codebase's scale (one tenant's monthly transaction volume).
