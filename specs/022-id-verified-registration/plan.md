# Implementation Plan: ID-Verified Registration

**Branch**: `022-id-verified-registration` | **Date**: 2026-07-20 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/022-id-verified-registration/spec.md`

## Summary

Directors record, in the web admin, that they've visually confirmed a child's and a guardian
contact's identity (document type + optional note), producing an Opgroeien/GDPR-required audit
trail with no card-reader hardware. Since no per-change history table or organisation-owner role
exists anywhere in this codebase (research.md R1/R2), the anti-tampering intent is satisfied with
a first-verified/most-recently-verified attribution pair on `Child` and `Contact` — the same
shape as the `CreatedAt`/`UpdatedAt` pair already used everywhere — rather than a new history
entity. `Child` also gains an optional, Data-Protection-encrypted National Register Number
(masked to its last 4 digits everywhere it's shown). The admin-home "unverified dossiers" signal
extends the existing `DataCompletenessSection` (feature 018) with a new flag type, and a per-child
badge on `/children` reuses the list endpoint's already-returned `ChildResponse`.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript (web, Next.js 15 App Router).

**Primary Dependencies**: MediatR, FluentValidation, EF Core 9, `Microsoft.AspNetCore.DataProtection`
(backend, already in use since 014a — no new package); Radix UI + shadcn/ui, `next-intl`,
openapi-fetch generated client (web). No new dependencies on either side.

**Storage**: PostgreSQL 16, schema-per-tenant — one additive, all-nullable-column migration on
the tenant `children` and `contacts` tables (research.md R7).

**Testing**: xUnit + Moq + TestContainers-provisioned PostgreSQL (backend, Constitution V); Jest +
React Testing Library (web, feature 007a's precedent).

**Target Platform**: Director web only (desktop, 1280px+); backend (ASP.NET Core Minimal API,
Cloud Run). No caregiver-tablet or parent-mobile surface (spec.md Assumptions).

**Project Type**: Web application (backend API + Next.js web, existing monorepo — no new
project).

**Performance Goals**: None beyond existing single-record CRUD and the existing
`GetDataCompletenessQuery` aggregate pattern — no new list/bulk operation, no new heavy query
shape.

**Constraints**: EF Core migrations MUST NOT auto-apply in production (CLAUDE.md); all
user-facing strings MUST use i18n keys (NL/FR/EN, Constitution IV); verification/NRN
write endpoints MUST remain `DirectorOnly` (Constitution I/III via existing policy reuse); NRN
MUST be encrypted at rest and never logged or rendered beyond its last 4 digits (spec.md
FR-011/FR-012).

**Scale/Scope**: Single-tenant CRUD additions — 8 new nullable columns on `Child` (attribution
pair ×2 fields ×3 + NRN ×2), 6 on `Contact` (attribution pair ×2 fields ×3), 1 new enum
(`IdDocumentType`), 3 new commands + 1 new port/adapter pair, 1 extended query
(`GetDataCompletenessQuery`), ~3 new web components, 1 extended list column.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation | Pass | All new endpoints sit under the existing `DirectorOnly` groups in `ChildrenEndpoints.cs`/`ContactsEndpoints.cs`, both already behind `TenantMiddleware`. No new endpoint reads across tenants. |
| II. Regulatory Compliance by Design | Pass | This *is* the regulatory feature (Opgroeien/GDPR identity-verification obligation) — enforcement lives in Application-layer command handlers (`VerifyChildIdentityCommand`/`VerifyContactIdentityCommand`), not UI-only validation; FR-003's "document type required" rule is enforced by FluentValidation server-side. |
| III. CQRS via MediatR & Thin Endpoints | Pass | Three new commands (`VerifyChildIdentityCommand`, `VerifyContactIdentityCommand`, `SetChildNrnCommand`), each with its own validator and handler; `GetDataCompletenessQueryHandler` extended in place. No business logic added to `*Endpoints.cs` files beyond existing claim-extraction (`CallerIdentity`, already established in `ChildrenEndpoints.cs`). |
| IV. Internationalization First | Pass | All new UI strings (verification section labels, document-type options, validation messages, dashboard flag label, list badge) ship as NL/FR/EN keys under `children.identity.*`/`dashboard.reporting.dataCompleteness.missingIdentityVerification` (spec FR-013). |
| V. Test with Real Infrastructure | Pass | New command handlers and the extended `GetDataCompletenessQuery` are tested against TestContainers PostgreSQL, per existing test-project setup. |
| VI. Secure Configuration & Storage | Pass | NRN encrypted via `IDataProtectionProvider` (existing key storage, research.md R3) — never logged, never returned in plain text by any contract. Migration authored as a normal EF Core migration file; SQL script generated and run manually against existing tenant schemas, not auto-applied. |
| VII. Monolith-First Simplicity | Pass | No new project/service. Extends the existing `ChildrenEndpoints`/`ContactsEndpoints`/`ReportingEndpoints` and the existing `web` app; one new port/adapter pair (`INrnProtector`) mirrors the existing `IPaymentTokenProtector` shape rather than inventing a new one. |

No violations — Complexity Tracking is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/022-id-verified-registration/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── identity-verification-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks — not yet created)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/Child.cs                                     # + attribution pair + EncryptedNrn/NrnLast4
│   ├── Entities/Contact.cs                                   # + attribution pair
│   └── Enums/IdDocumentType.cs                                # NEW — BirthCertificate|KidsId|Eid|Passport|Other
├── ChildCare.Application/
│   ├── Common/INrnProtector.cs                                 # NEW — mirrors IPaymentTokenProtector
│   ├── Children/
│   │   ├── VerifyChildIdentityCommand.cs                        # NEW
│   │   ├── VerifyChildIdentityCommandValidator.cs                # NEW
│   │   ├── VerifyChildIdentityCommandHandler.cs                  # NEW
│   │   ├── SetChildNrnCommand.cs                                 # NEW
│   │   ├── SetChildNrnCommandValidator.cs                        # NEW
│   │   ├── SetChildNrnCommandHandler.cs                          # NEW
│   │   └── ChildMapper.cs                                        # + new response fields
│   ├── Contacts/
│   │   ├── VerifyContactIdentityCommand.cs                       # NEW
│   │   ├── VerifyContactIdentityCommandValidator.cs               # NEW
│   │   ├── VerifyContactIdentityCommandHandler.cs                 # NEW
│   │   └── ContactMapper.cs                                       # + new response fields
│   └── Reporting/GetDataCompletenessQuery.cs                     # + missing_identity_verification flag (research.md R5)
├── ChildCare.Infrastructure/
│   ├── Children/NrnProtector.cs                                  # NEW — IDataProtectionProvider-backed
│   └── Persistence/Migrations/Tenant/<timestamp>_AddIdentityVerificationAndNrn.cs   # NEW
├── ChildCare.Contracts/
│   ├── Requests/ChildRequests.cs                                 # + VerifyChildIdentityRequest, SetChildNrnRequest
│   ├── Requests/ContactRequests.cs                                # + VerifyContactIdentityRequest
│   ├── Responses/ChildResponse.cs                                 # + verification fields + NrnLast4
│   └── Responses/ContactResponse.cs                                # + verification fields (both records)
├── ChildCare.Api/Endpoints/
│   ├── ChildrenEndpoints.cs                                       # + POST .../identity-verification, PUT .../nrn
│   └── ContactsEndpoints.cs                                       # + POST .../identity-verification
└── ChildCare.Api.Tests/
    ├── Children/VerifyChildIdentityTests.cs                       # NEW
    ├── Children/SetChildNrnTests.cs                                # NEW
    ├── Contacts/VerifyContactIdentityTests.cs                      # NEW
    └── Reporting/DataCompletenessEndpointsTests.cs                 # + missing_identity_verification cases

web/
├── components/children/
│   ├── ChildIdentityVerificationSection.tsx                       # NEW — verification action/state + NRN entry
│   └── ContactIdentityVerificationDialog.tsx                       # NEW — mirrors LinkContactDialog's modal pattern
├── components/ui/badge.tsx                                        # unchanged — reused for the list badge
├── app/(app)/children/
│   ├── page.tsx                                                    # + "Niet geverifieerd" badge column
│   └── [id]/page.tsx                                                # + ChildIdentityVerificationSection in profile tab
├── components/children/ChildContactsTab.tsx                        # + verify action per row, opens the new dialog
├── components/reporting/DataCompletenessSection.tsx                 # + missingIdentityVerification label mapping
├── i18n/locales/{en,fr,nl}.json                                    # + children.identity.* / dashboard.reporting.dataCompleteness.missingIdentityVerification
├── lib/generated/api-types.ts                                      # regenerated (mechanical)
└── __tests__/ (or co-located) — ChildIdentityVerificationSection, ContactIdentityVerificationDialog, badge rendering, DataCompletenessSection's new flag label
```

**Structure Decision**: No new top-level project or app. Backend changes extend the existing
feature-006/030 vertical slices in place (Domain → Application → Contracts → Api →
Infrastructure/Migrations) and extend feature 018's `GetDataCompletenessQuery` rather than
introducing a parallel reporting mechanism. Web adds two new components under the existing
`web/components/children/` directory and wires them into the existing child-detail and
child-list screens — no new route, no new tab (research.md R6).

## Complexity Tracking

Not applicable — no Constitution Check violations.
