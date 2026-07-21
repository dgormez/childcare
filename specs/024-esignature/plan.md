# Implementation Plan: Digital Contract E-Signature

**Branch**: `024-esignature` | **Date**: 2026-07-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/024-esignature/spec.md`

## Summary

Let a director send a `Draft` contract (007) for e-signature by email; the parent opens a
secure, single-use, 72-hour link with no login, reviews the contract, signs (drawn or typed), and
authorises a SEPA direct debit mandate with their IBAN in the same session. On successful
signing, the system records the signature/mandate on the `Contract`, generates and persists a
final signed PDF (never regenerated afterward), and emails it to both parent and director.
Signing is additive to the existing `Draft → Active` lifecycle (007), not a precondition for it —
a director can still activate a contract independently, in either order. A new
organisation-level `SepaCreditorIdentifier` (mirrors `Tenant.KboNumber`) must be configured
before any invitation can be sent. Contracts has no dedicated director-web screen yet
(`web/app/(app)/contracts/page.tsx` is a stub); this feature builds the minimal
send/status/resend UI it needs, not full contract management.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / Next.js (App Router) for `web/`
(director-web additions and the new public signing route both live in this app).

**Primary Dependencies**: ASP.NET Core Minimal APIs, MediatR, FluentValidation, EF Core 9,
PostgreSQL 16, QuestPDF (backend, all existing); Next.js/Tailwind (web, existing) — no new
external package on either side. The signing token reuses the existing Data Protection API
already in use for `IUnsubscribeTokenService`/`ITourInvitationTokenService`/`INrnProtector`
(research.md R2/R3); the signature capture is a native `<canvas>` + Pointer Events component, no
signature-pad library (research.md R8).

**Storage**: PostgreSQL — extends existing `Contract` (tenant schema) and `Tenant` (public
schema) tables (no new tables). GCS gains one new object prefix (`signed-contracts/`) via a new
`ISignedContractStorage` port, following the existing `Gcs*Storage` adapter shape.

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, Constitution Principle V);
existing Jest/RTL suite for `web/`.

**Target Platform**: a new public, unauthenticated web route (`web/app/sign/page.tsx`) plus
minimal additions to director-web's contracts screen (currently a stub). No caregiver-tablet or
parent-mobile (Expo) change — explicitly out of scope per spec.md.

**Project Type**: Mixed — backend API extension (public + director-facing) and two `web/`
surfaces (a new public route, and the first real build-out of the contracts director-web screen),
per spec.md's Product Context.

**Performance Goals**: no special target beyond the existing API baseline — low-volume,
one-signing-per-contract traffic (spec.md's Technical Requirements). PDF generation + GCS upload
happen synchronously in the signing-submission request; the two confirmation emails are sent
after the transaction commits.

**Constraints**: signing link expires 72h after issue and is single-use (FR-002/FR-003, SC-002);
IBAN encrypted at rest, never returned in full after capture (FR-020, research.md R3/R4); signed
PDF never regenerated after signing (FR-010, SC-003); signing does not gate `Draft → Active`
(FR-015); all new strings in NL/FR/EN (FR-019).

**Scale/Scope**: two extended entities (`Contract`, `Tenant`, no new tables), one new enum
(`SignatureType`), ~2 new backend interfaces (`IContractSigningTokenService`, `IIbanProtector`,
`ISignedContractStorage` — three, not two), ~6 new/extended MediatR requests, one new
tenant-exempt public endpoint group, one extended `DirectorOnly` endpoint, one new public Next.js
route, and the first real contracts director-web screen.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|---|---|
| I. Multi-Tenant Isolation | Pass. The public signing endpoints carry no JWT `tenant_id` claim, so they're marked `.RequireTenantExempt()` and resolve tenant explicitly from an `org` slug query parameter via `OrganisationSlugResolver` before touching any data — the same pattern features 020/023 already established (research.md R1). Every director-facing addition runs through the existing `TenantMiddleware`/`ITenantDbContext` path unchanged. |
| II. Regulatory Compliance by Design | N/A. No BKR, contract-overlap, or closure-notification logic is touched — `ContractActivationChecker`'s existing conflict checks (007) are explicitly left untouched by this feature (research.md R5). |
| III. CQRS via MediatR & Thin Endpoints | Pass. All new writes (send/resend invitation, submit signature, set creditor ID) are new MediatR commands; new endpoint files stay thin. |
| IV. Internationalization First | Pass. FR-019 requires all new strings (signing page, both emails, new director-facing labels) in NL/FR/EN via existing i18n mechanisms — tracked in tasks.md. |
| V. Test with Real Infrastructure | Pass. Backend tests run against TestContainers PostgreSQL per existing convention; no InMemory provider introduced. |
| VI. Secure Configuration & Storage | Pass. New `Contract`/`Tenant` columns ship as an EF Core migration with a manually-run SQL script (no auto-apply in production). IBAN encrypted at rest via Data Protection (research.md R3), never a hardcoded key. Signed PDFs use GCS signed URLs only, no public blob URLs (research.md R6), following the existing `Gcs*Storage` shape. |
| VII. Monolith-First Simplicity | Pass. No new project/service — new code lives in `ChildCare.Application/Contracts` alongside the existing 007 code, plus a new public Next.js route in the existing `web/` app, not a separate app. |

No violations. Complexity Tracking section is empty.

## Project Structure

### Documentation (this feature)

```text
specs/024-esignature/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── esignature-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/Contract.cs                                      # + SigningToken, SigningTokenExpiresAt, SignedAt, SignatureData, SignatureType, SignedByIp, SepaIbanEncrypted, SepaMandateReference, SepaAuthorisedAt
│   ├── Entities/Tenant.cs                                         # + SepaCreditorIdentifier
│   └── Enums/SignatureType.cs                                     # new — Drawn/Typed
├── ChildCare.Application/
│   ├── Common/
│   │   ├── IContractSigningTokenService.cs                        # new — mirrors ITourInvitationTokenService
│   │   ├── IIbanProtector.cs                                      # new — mirrors INrnProtector
│   │   └── ISignedContractStorage.cs                              # new — mirrors IFiscalAttestationStorage
│   ├── Contracts/
│   │   ├── SendContractSigningInvitationCommand.cs                 # new — director, serves send + resend
│   │   ├── GetContractForSigningQuery.cs                          # new — public, tenant-exempt
│   │   ├── SubmitContractSigningCommand.cs                        # new — public, tenant-exempt
│   │   ├── UpdateContractCommand.cs                                # + clears outstanding SigningToken on save
│   │   └── ContractSigningStatus.cs                                # new — derived status helper (data-model.md)
│   └── Organisations/UpdateOrganisationCommand.cs                  # + SepaCreditorIdentifier alongside existing KboNumber (User Story 4)
├── ChildCare.Contracts/
│   ├── Requests/ContractRequests.cs                                # + signing-invitation, submit-signing requests
│   ├── Requests/OrganisationRequests.cs                            # + SepaCreditorIdentifier on UpdateOrganisationRequest
│   └── Responses/ContractResponses.cs                              # + signing status, masked IBAN, mandate reference fields; OrganisationResponse + SepaCreditorIdentifier
├── ChildCare.Api/
│   ├── Endpoints/
│   │   ├── ContractsEndpoints.cs                                    # + /signing-invitation route
│   │   ├── OrganisationEndpoints.cs                                 # PUT/GET /api/organisations/me + SepaCreditorIdentifier
│   │   └── PublicContractSigningEndpoints.cs                       # new — /api/public/contracts/sign, AllowAnonymous + RequireTenantExempt
│   └── Services/EmailService.cs                                    # + SendContractSigningInvitationAsync, SendSignedContractAsync
└── ChildCare.Infrastructure/
    ├── Email/DataProtectionContractSigningTokenService.cs           # new — implements IContractSigningTokenService (feature-named Email/ folder, matching where DataProtectionTourInvitationTokenService actually lives — not a generic Common/ bucket, which Infrastructure doesn't use)
    ├── Contracts/IbanProtector.cs                                   # new — implements IIbanProtector (mirrors NrnProtector's feature-named-folder placement)
    ├── Storage/GcsSignedContractStorage.cs                          # new — implements ISignedContractStorage
    ├── Pdf/QuestPdfContractGenerator.cs                             # + signature block + SEPA mandate section for the final signed PDF
    ├── Persistence/Migrations/{Tenant,Public}/<timestamp>_AddContractSigningAndSepaMandate.cs  # new
    └── Email/Templates/{contract-signing-invitation,signed-contract-copy}.scriban  # new

web/
├── app/
│   ├── sign/page.tsx                                               # new — public, unauthenticated signing page (outside (app)/(auth) groups)
│   └── (app)/contracts/page.tsx                                    # replaces the NotYetAvailable stub — minimal list + send/resend/status actions
├── components/SignatureCapture.tsx                                  # new — canvas draw + typed-name fallback (research.md R8)
├── lib/publicApiClient.ts                                           # existing (feature 023) — reused for the unauthenticated signing fetch calls
└── i18n/locales/{en,nl,fr}.json                                     # + new keys (signing page, contracts screen, both emails)
```

**Structure Decision**: Follows the existing monolith-first layout (Constitution VII) — new
backend code extends `ChildCare.Application/Contracts` (the existing 007 folder) rather than a
new project; the public signing page is a new top-level route in the existing `web/` Next.js app
(outside the `(app)`/`(auth)` groups, same precedent feature 023's `/enroll` route established),
and the director-web contracts screen is built out from its current stub in place, not as a new
route.

## Complexity Tracking

*No Constitution Check violations — table intentionally empty.*
