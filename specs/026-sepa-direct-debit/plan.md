# Implementation Plan: SEPA Direct Debit Batch Collection

**Branch**: `026-sepa-direct-debit` | **Date**: 2026-07-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/026-sepa-direct-debit/spec.md`

## Summary

Directors currently wait on individual bank transfers to collect invoice payments. This feature
lets a director generate a SEPA direct debit batch (pain.008.001.02 XML) from a location's
`Sent` invoices whose families have a signed SEPA mandate (feature 024), hand-generating and
schema-validating the XML against .NET's built-in `XmlSchemaSet` and the official, in-repo
ISO20022 XSD (research.md R1/R2) — no third-party SEPA-generation dependency. Included invoices
move to a new `PendingDebit` status; feature 025's existing CODA reconciliation is extended to
treat that status as open/matchable, so a confirmed collection reaches `Paid` through the exact
same import step a director already uses (research.md R4, spec.md FR-009). A returned debit or a
revoked mandate reverts an invoice/contract to normal follow-up rather than leaving it stranded
(spec.md FR-010/FR-011). No new settings surface: the creditor identifier, name, and IBAN all
already exist on `Tenant`/`Location` from features 024/014 (research.md R5, spec.md
Clarifications) — a real premise correction found before implementation, not carried through
from the originating BACKLOG description.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / Next.js 15 App Router (director-web).

**Primary Dependencies**: MediatR + FluentValidation (Constitution III), EF Core 10,
`System.Xml.Linq` + `System.Xml.Schema` (BCL, research.md R1/R2 — no new NuGet package),
`IIbanProtector` (existing, feature 024, research.md R4), openapi-typescript + openapi-fetch for
the web client (existing convention).

**Storage**: PostgreSQL 16, tenant schema (Principle I) — one new table (`sepa_batches`), one new
column on `contracts` (`SepaRevokedAt`), three new columns on `invoices` (`SepaBatchId`, an
immutable `SepaMandateReferenceUsed` snapshot needed for correct FRST/RCUR determination across a
return or revoke-and-resign — data-model.md, added during this feature's own safety-checklist
pass, CHK008 — and a `SepaReturnReason` text column for FR-010's reason display), and a new
`PendingDebit` value on the existing `InvoiceStatus` enum.

**Testing**: xUnit + TestContainers-provisioned PostgreSQL for backend integration/API tests
(Constitution V) — including a dedicated schema-validation test asserting the generated XML
passes `XmlSchemaSet` validation against the embedded XSD; Jest + React Testing Library for web
component tests (existing `web/` convention).

**Target Platform**: Director-web only (Cloud Run-hosted ASP.NET Core API + Next.js). No
caregiver-tablet or parent-mobile surface (spec.md Cross-platform Impact).

**Project Type**: Web application (existing `backend/` + `web/` structure).

**Performance Goals**: No special targets beyond this codebase's existing table/read-model
conventions — a batch is one location's monthly `Sent` invoices (tens, not thousands), per
spec.md's Technical Requirements.

**Constraints**: The embedded `pain.008.001.02.xsd` (research.md R2) is the official
SWIFTStandards-generated ISO20022 schema, cross-verified byte-identical from two independent
open-source projects — treated as a fixed, non-editable third-party contract, the same posture
`docs/integrations/opgroeien/`'s archived XSDs already establish for government schemas. Debtor
IBAN is financial PII and MUST reuse `IIbanProtector`'s existing encryption/access-logging
posture (spec.md FR-014, Constitution VI) — no plaintext IBAN persisted, and the generated XML
(which necessarily contains plaintext IBANs) is never written to storage, only streamed as the
download response.

**Scale/Scope**: Single location per batch (a pain.008 file has one `PmtInf` block, one creditor
account) — a director generates one batch per location per period, matching this feature's own
FR-002/FR-003 scope; no cross-location batching.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Check | Result |
| --- | --- | --- |
| I. Multi-Tenant Isolation | `sepa_batches` lives in the tenant schema via `TenantDbContext`; every endpoint requires `DirectorOnly` and resolves tenant from the JWT via existing `TenantMiddleware`. No cross-tenant read path. | Pass |
| II. Regulatory Compliance by Design | Not a BKR/regulatory-ratio feature — no gate applies. (The pain.008 EPC schema is a banking-format contract, not a Flemish childcare regulation this principle governs.) | N/A |
| III. CQRS via MediatR & Thin Endpoints | Every write (generate batch, mark returned, revoke mandate) is a MediatR command; eligibility list and batch history are MediatR queries. Endpoint files map HTTP↔MediatR only, per `InvoiceEndpoints.cs`/`CodaTransactionEndpoints.cs` precedent. | Pass |
| IV. Internationalization First | All new director-facing strings (eligibility reasons, batch screen labels, error messages) ship as `next-intl` keys in NL/FR/EN (spec.md FR-015); C# identifiers stay English (`SepaBatch`, not a Dutch/French term) — "SEPA"/"IBAN"/"pain.008" are the actual international banking-standard names, not translation choices, same carve-out this codebase already grants "CODA" (025) and "OGM"/"KBO" (014/024). | Pass |
| V. Test with Real Infrastructure | Backend integration/API tests run against TestContainers PostgreSQL, covering eligibility filtering, the FRST/RCUR determination, the status-transition invariants, and the embedded-XSD schema-validation test (quickstart.md's six scenarios map directly to test cases). | Pass |
| VI. Secure Configuration & Storage | Debtor IBAN decrypted via existing `IIbanProtector`/Data Protection (feature 024's `"Contract.SepaIban"` purpose string, unchanged); each decryption logged as an access event (FR-014); the generated XML is returned directly in the HTTP response, never persisted to GCS or disk; no new secrets/connection strings. | Pass |
| VII. Monolith-First Simplicity | No new deployable — feature lives inside the existing `ChildCare.Api`/`Application`/`Domain`/`Infrastructure`/`Contracts` five-project structure and the existing `web/` app. | Pass |

No violations — Complexity Tracking section omitted (nothing to justify).

## Project Structure

### Documentation (this feature)

```text
specs/026-sepa-direct-debit/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── sepa-direct-debit-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks — not created by this command)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/
│   │   ├── Contract.cs             # + SepaRevokedAt
│   │   ├── Invoice.cs              # + SepaBatchId, SepaMandateReferenceUsed, SepaReturnReason
│   │   └── SepaBatch.cs            # new
│   └── Enums/
│       └── InvoiceStatus.cs        # + PendingDebit
├── ChildCare.Application/
│   ├── Common/
│   │   └── ISepaBatchXmlGenerator.cs   # thin abstraction: (batch context) -> validated XML bytes,
│   │                                     # mirrors IInvoicePdfGenerator wrapping QuestPDF — Application
│   │                                     # never touches System.Xml.Schema directly.
│   ├── Invoices/
│   │   ├── MarkInvoicePaidCommand.cs    # extended: accept PendingDebit as well as Sent
│   │   └── MarkInvoiceSepaReturnedCommand.cs  # new (FR-010)
│   ├── Contracts/
│   │   └── RevokeSepaMandateCommand.cs  # new (FR-011)
│   ├── CodaTransactions/
│   │   └── ImportCodaFileCommand.cs     # extended: treat PendingDebit as an open/matchable status
│   └── SepaBatches/
│       ├── GetSepaBatchEligibilityQuery.cs   # FR-001, data-model.md's eligibility rule
│       ├── GenerateSepaBatchCommand.cs        # FR-002..FR-007, FR-013
│       ├── ListSepaBatchesQuery.cs            # FR-008
│       └── SepaSequenceTypeResolver.cs        # FR-002a / research.md R3, unit-testable
│                                                # independent of the MediatR handler
├── ChildCare.Infrastructure/
│   ├── Sepa/
│   │   ├── Schemas/
│   │   │   └── pain.008.001.02.xsd     # official EPC/ISO20022 schema (research.md R2)
│   │   └── SepaBatchXmlGenerator.cs    # ISepaBatchXmlGenerator impl: XDocument build + XmlSchemaSet validate
│   └── Persistence/Migrations/Tenant/
│       └── <timestamp>_AddSepaDirectDebit.cs
├── ChildCare.Contracts/
│   └── Responses/
│       ├── SepaBatchEligibilityResponse.cs
│       └── SepaBatchResponse.cs
└── ChildCare.Api/
    └── Endpoints/
        └── SepaBatchEndpoints.cs   # MapSepaBatchEndpoints, DirectorOnly group, mirrors
                                      # CodaTransactionEndpoints.cs; also adds the two invoice/
                                      # contract actions (mark-sepa-returned, revoke-sepa-mandate)

web/
├── app/(app)/invoices/
│   └── sepa-batches/
│       └── page.tsx                # eligibility review + generate screen (spec.md's Main Flow)
├── components/invoices/
│   └── SepaBatchHistoryTable.tsx   # high-density table, per design-system.md
└── lib/api/                        # openapi-fetch client regenerated to include the new routes
```

**Structure Decision**: Extends the existing `backend/` five-project structure and `web/`
Next.js app — no new project, no new deployable (Constitution VII). The web screen nests under
the existing `/invoices` route (`/invoices/sepa-batches`), the same sub-navigation pattern
025's `/invoices/reconciliation` established — final route/nav-entry naming confirmed during
implementation against `design-decisions.md`.
