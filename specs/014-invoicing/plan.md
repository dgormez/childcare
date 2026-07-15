# Implementation Plan: Invoicing

**Branch**: `014-invoicing` | **Date**: 2026-07-15 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/014-invoicing/spec.md`

## Summary

Generate monthly invoices per (child, contract, location): compute billable days directly from
`AttendanceRecord` (present + unjustified-absent, excluding closure days, restricted to the
contract's active date range), render a QuestPDF invoice with a Belgian OGM structured payment
reference, and track a draft → sent → paid lifecycle with a computed `overdue` view. Director web
gets bulk generation, review/edit, send, and mark-paid; parent mobile gets a read-only list and
PDF download of their own children's sent/paid/overdue invoices.

## Technical Context

**Language/Version**: C# / .NET 10 (backend, matching this codebase's fixed stack); TypeScript 5
/ React 19 (`web/`, `parent-mobile/`).

**Primary Dependencies**: MediatR + FluentValidation (backend CQRS, Constitution III); EF Core 9
/ PostgreSQL (JSONB for `LineItems`, mirroring `ChildEvent`'s existing JSONB-payload pattern);
QuestPDF (already wired in this codebase — `IContractPdfGenerator`/`QuestPdfContractGenerator`
is the exact pattern this feature's `IInvoicePdfGenerator` reuses); Next.js/Tailwind
(`web/`, reusing `ReservationSettingsForm.tsx`'s per-location-settings pattern for
`InvoiceSettingsForm.tsx`, and 013a's day-reservations queue's table/filter pattern for the
invoice list); Expo/React Native (`parent-mobile/`, reusing `services/menu.ts`'s fetch pattern
for `services/invoices.ts`).

**Storage**: PostgreSQL, tenant schema. New table `invoices` (`ChildId`, `ContractId`,
`LocationId`, `PeriodMonth`, `Status`, `SubtotalCents`, `TotalCents`, `LineItems` JSONB,
`OgmReference`, `SequenceNumber` (identity, OGM base number source), `SentAt`, `PaidAt`,
`DueDate`, `CreatedAt`, `UpdatedAt`), unique on `(ChildId, ContractId, LocationId, PeriodMonth)`.
`Location` gains nullable `Erkenningsnummer`/`BankAccountNumber` and `InvoiceDueDays` (int,
default 14). `Tenant` (public schema) gains nullable `KboNumber`.

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, Constitution V); Vitest +
Testing Library (`web/`); Jest + `@testing-library/react-native` (`parent-mobile/`).

**Target Platform**: Director web (generation, review, send, payment tracking) and parent mobile
(view, download). No caregiver-tablet surface (`Workflows/billing.md`).

**Performance Goals**: Not a hot path — bulk generation is bounded by one location's
active-contract count (tens to low hundreds), runs on an explicit director action.

**Constraints**: Money is always integer cents, never floating-point (constitution's Technology
Stack Constraints + spec.md FR-017). A `paid` invoice is immutable (spec.md FR-012/SC-005). No
background-job infrastructure exists in this codebase — "overdue" MUST be a computed view, not a
scheduled status transition (spec.md FR-010, Assumptions).

**Scale/Scope**: One new backend aggregate (`Invoice` + its commands/queries), one new PDF
generator/model pair, one new director-web section (settings + generate + list/detail), one new
parent-mobile section (list + detail/download), two small entity extensions (`Location`,
`Tenant`).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation (NON-NEGOTIABLE) | ✅ Pass | All new queries/commands go through `ITenantDbContext`, identical to every existing tenant-schema access in this codebase. `Tenant.KboNumber` is the one public-schema change — read/write through the existing `IPublicDbContext`/organisation-profile path, not a new context. |
| II. Regulatory Compliance by Design (NON-NEGOTIABLE) | ✅ Pass (N/A) | Invoicing is not a BKR-ratio or split-location-overlap concern; untouched by this feature. |
| III. CQRS via MediatR & Thin Endpoints | ✅ Pass | Generate/send/mark-paid/regenerate are MediatR commands with FluentValidation; list/detail are MediatR queries. Endpoint files gain only route/DTO mapping. |
| IV. Internationalization First (NON-NEGOTIABLE) | ✅ Pass, must verify at implementation | All new strings (invoice statuses in plain language, settings labels, PDF static text) MUST be added as locale keys on `web/i18n/locales/{en,fr,nl}.json` and `parent-mobile`'s equivalent. The PDF itself follows `QuestPdfContractGenerator`'s existing per-locale `Labels` dictionary pattern — no hardcoded PDF text in one language. |
| V. Test with Real Infrastructure (NON-NEGOTIABLE) | ✅ Pass, must verify at implementation | The billable-day computation and the OGM modulo-97 checksum are exactly the kind of money-correctness logic this principle calls out for real-PostgreSQL integration testing, not a unit-test-only double. |
| VI. Secure Configuration & Storage | ✅ Pass (N/A) | No secrets. Invoice PDFs are rendered on-demand from stored `LineItems` (mirrors `GenerateContractPdfQuery` — no GCS storage, no signed-URL concern; see research.md R1). |
| VII. Monolith-First Simplicity | ✅ Pass | No new project or service. "Overdue" is a computed view specifically to avoid introducing background-job infrastructure this codebase doesn't have yet (research.md R4). |

No violations. Complexity Tracking table below is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/014-invoicing/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/            # Phase 1 output
│   └── invoicing-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/Invoice.cs                         # NEW
│   ├── Entities/Location.cs                         # extended: Erkenningsnummer/BankAccountNumber/InvoiceDueDays
│   └── Entities/Tenant.cs                            # extended: KboNumber
├── ChildCare.Application/
│   ├── Invoices/
│   │   ├── GenerateInvoicesCommand.cs                # NEW (bulk, per location/month)
│   │   ├── AddInvoiceExtraChargeCommand.cs           # NEW
│   │   ├── SendInvoicesCommand.cs                    # NEW (one or many)
│   │   ├── MarkInvoicePaidCommand.cs                 # NEW
│   │   ├── RegenerateInvoiceCommand.cs                # NEW
│   │   ├── ListInvoicesQuery.cs                       # NEW (director, filtered)
│   │   ├── GetInvoiceByIdQuery.cs                     # NEW
│   │   ├── GetParentInvoicesQuery.cs                  # NEW
│   │   ├── BillableDayCalculator.cs                   # NEW — shared present/unjustified/closure computation
│   │   ├── OgmReferenceGenerator.cs                   # NEW — modulo-97 checksum
│   │   └── InvoiceMapper.cs                           # NEW
│   └── Locations/
│       ├── UpdateLocationInvoiceSettingsCommand.cs    # NEW (mirrors UpdateLocationReservationSettingsCommand, 013f)
│       └── LocationMapper.cs                          # extended: erkenningsnummer/bankAccountNumber/invoiceDueDays
├── ChildCare.Contracts/
│   ├── Requests/InvoiceRequests.cs                    # NEW
│   ├── Requests/LocationRequests.cs                   # extended
│   └── Responses/InvoiceResponse.cs                   # NEW
├── ChildCare.Infrastructure/
│   ├── Pdf/QuestPdfInvoiceGenerator.cs                 # NEW (mirrors QuestPdfContractGenerator.cs)
│   └── Persistence/
│       ├── TenantDbContext.cs                          # extended: Invoice DbSet + config, Location extension
│       ├── PublicDbContext.cs                          # extended: Tenant.KboNumber config
│       └── Migrations/{Tenant,Public}/                 # NEW migrations
└── ChildCare.Api/Endpoints/
    ├── InvoiceEndpoints.cs                             # NEW
    └── LocationEndpoints.cs                            # extended: invoice-settings route

web/
├── app/(app)/invoices/page.tsx                         # NEW — director invoice list/generate
├── app/(app)/invoices/[id]/page.tsx                    # NEW — director invoice detail/edit/send/mark-paid
├── app/(app)/locations/[id]/page.tsx                   # extended: new "Invoicing" settings tab
├── components/
│   ├── InvoiceSettingsForm.tsx                         # NEW (mirrors ReservationSettingsForm.tsx)
│   ├── invoices/InvoiceTable.tsx                       # NEW
│   └── invoices/InvoiceDetail.tsx                      # NEW
└── i18n/locales/{en,fr,nl}.json                         # extended

parent-mobile/
├── services/invoices.ts                                # NEW
├── app/(app)/invoices/index.tsx                         # NEW — list
├── app/(app)/invoices/[id].tsx                           # NEW — detail/download
└── i18n/locales/{en,fr,nl}.json                          # extended
```

**Structure Decision**: New `Invoices`/`Invoice*` slice across the existing five backend
projects and both client apps — no new projects. Billable-day computation and OGM generation are
each a single, shared, independently-testable unit (`BillableDayCalculator`,
`OgmReferenceGenerator`) rather than inlined in the generate command, since both are exactly the
kind of money-correctness logic that needs focused, real-database integration tests per
constitution Principle V.
