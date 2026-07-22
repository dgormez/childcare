# Implementation Plan: CODA/CODABOX Payment Matching

**Branch**: `025-coda-payment-matching` | **Date**: 2026-07-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/025-coda-payment-matching/spec.md`

## Summary

Directors currently reconcile incoming bank payments against open invoices by hand, cross-
checking a bank portal against a spreadsheet. This feature lets a director upload a Belgian CODA
bank statement file in director-web; the backend parses it (via the `CodaParser` NuGet package,
research.md R1), auto-matches transactions against invoices' existing OGM payment references
(feature 014) and marks them paid through the existing `MarkInvoicePaidCommand` (research.md
R4), surfaces amount+IBAN candidates as director-confirmable suggestions when a family has a
SEPA mandate on file (feature 024, research.md R2/R3), and leaves everything else — unmatched
transfers, duplicate payments against an already-paid invoice, payments against a closed invoice
from an earlier period, partial payments, and negative-amount reversals — in a clearly labeled
review list rather than silently dropping it. CODABOX's automated statement-delivery API is
explicitly out of scope for this feature (spec.md Assumptions) — manual file upload is the
complete MVP.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / Next.js 15 App Router (director-web).

**Primary Dependencies**: MediatR + FluentValidation (Constitution III), EF Core 9, `CodaParser`
1.0.2 NuGet package (research.md R1, new dependency), `IIbanProtector` / ASP.NET Core Data
Protection (existing, feature 024, research.md R2), openapi-typescript + openapi-fetch for the
web client (existing convention, no NSwag).

**Storage**: PostgreSQL 16, tenant schema (Principle I) — two new tables, `coda_imports` and
`coda_transactions` (data-model.md). No change to the existing `invoices` table.

**Testing**: xUnit + TestContainers-provisioned PostgreSQL for backend integration/API tests
(Constitution V, no InMemory provider); Jest + React Testing Library for web component tests
(existing `web/` convention since feature 007a).

**Target Platform**: Director-web only (Cloud Run-hosted ASP.NET Core API + Next.js). No
caregiver-tablet or parent-mobile surface (spec.md Cross-platform Impact).

**Project Type**: Web application (existing `backend/` + `web/` structure).

**Performance Goals**: No special targets beyond this codebase's existing table-list/read-model
conventions — a CODA file is one bank statement's worth of transactions (tens, not thousands),
per spec.md's Technical Requirements.

**Constraints**: `CodaParser` is GPL-2.0-licensed (research.md R1) — acceptable given this
product's SaaS-only, no-distribution model; revisit if that model ever changes. Sender IBAN is
financial PII and MUST be encrypted at rest with access logged (spec.md FR-014, Constitution VI).

**Scale/Scope**: Single bank account per tenant location for MVP (spec.md Assumptions, matching
the BACKLOG's explicit out-of-scope note) — no multi-account reconciliation.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Check | Result |
| --- | --- | --- |
| I. Multi-Tenant Isolation | `coda_imports`/`coda_transactions` live in the tenant schema via `TenantDbContext`; every endpoint requires `DirectorOnly` and resolves tenant from the JWT via existing `TenantMiddleware`. No cross-tenant read path. | Pass |
| II. Regulatory Compliance by Design | Not a BKR/regulatory-ratio feature — no gate applies. | N/A |
| III. CQRS via MediatR & Thin Endpoints | Every write (import, confirm, reject, review) is a MediatR command; `GET /api/coda-transactions` is a MediatR query (list with filters, not a simple single-entity lookup). Endpoint file maps HTTP↔MediatR only, per `InvoiceEndpoints.cs` precedent. | Pass |
| IV. Internationalization First | All new director-facing strings (import summary labels, match-type badges, error messages) ship as `next-intl` keys in NL/FR/EN (spec.md FR-015); no hardcoded C# identifiers use Dutch/French terms (`CodaImport`, `CodaTransaction`, `MatchType`, not `Coda`-prefixed Flemish terms beyond the domain-standard "CODA" acronym itself, which is the Belgian banking standard's actual name, not a translation choice). | Pass |
| V. Test with Real Infrastructure | Backend integration/API tests run against TestContainers PostgreSQL, covering each `MatchType` branch, the dedupe path, and the once-only-paid invariant (quickstart.md's five scenarios map directly to test cases). | Pass |
| VI. Secure Configuration & Storage | `SenderIbanEncrypted` via `IIbanProtector` (Data Protection, feature 024's existing mechanism, new purpose string); no plaintext IBAN persisted; parser exceptions never surface to the client (FR-002). No new secrets/connection strings. | Pass |
| VII. Monolith-First Simplicity | No new deployable — feature lives inside the existing `ChildCare.Api`/`Application`/`Domain`/`Infrastructure`/`Contracts` five-project structure and the existing `web/` app. | Pass |

No violations — Complexity Tracking section omitted (nothing to justify).

## Project Structure

### Documentation (this feature)

```text
specs/025-coda-payment-matching/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── coda-payment-matching-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks — not created by this command)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   └── Entities/
│       ├── CodaImport.cs
│       └── CodaTransaction.cs
├── ChildCare.Application/
│   ├── Common/
│   │   └── ICodaParser.cs          # thin abstraction over the CodaParser NuGet package,
│   │                                # mirrors IInvoicePdfGenerator wrapping QuestPDF — Application
│   │                                # never references the NuGet package directly.
│   └── CodaTransactions/
│       ├── ImportCodaFileCommand.cs
│       ├── ConfirmCodaTransactionMatchCommand.cs
│       ├── RejectCodaTransactionMatchCommand.cs
│       ├── ReviewCodaTransactionCommand.cs
│       ├── ListCodaTransactionsQuery.cs
│       └── CodaTransactionMatcher.cs   # FR-004/005/005a/007/008/009/016 matching logic,
│                                        # unit-testable independent of the MediatR handler
├── ChildCare.Infrastructure/
│   ├── Coda/
│   │   └── CodaParserAdapter.cs    # ICodaParser implementation wrapping the NuGet package
│   └── Persistence/Migrations/Tenant/
│       └── <timestamp>_AddCodaPaymentMatching.cs
├── ChildCare.Contracts/
│   ├── Requests/  (none beyond the multipart upload, handled via IFormFile)
│   └── Responses/
│       ├── CodaImportSummaryResponse.cs
│       └── CodaTransactionResponse.cs
└── ChildCare.Api/
    └── Endpoints/
        └── CodaTransactionEndpoints.cs   # MapCodaTransactionEndpoints, DirectorOnly group,
                                            # mirrors InvoiceEndpoints.cs

web/
├── app/(app)/invoices/
│   └── reconciliation/
│       └── page.tsx                # upload + review-list screen (spec.md's Main Flow)
├── components/invoices/
│   └── CodaTransactionTable.tsx    # high-density table, match-type badges, per design-system.md
└── lib/api/                        # openapi-fetch client regenerated to include the new routes
```

**Structure Decision**: Extends the existing `backend/` five-project structure and `web/`
Next.js app — no new project, no new deployable (Constitution VII). The web screen nests under
the existing `/invoices` route (`/invoices/reconciliation`) rather than a new top-level nav
entry, since this is a sub-task of the existing Billing & Payments workflow's invoices screen,
not a new domain area — final route naming is confirmed during implementation against
`design-decisions.md`'s sidebar-navigation entry, added there if this warrants its own nav item
rather than a tab/link from the invoices list.
