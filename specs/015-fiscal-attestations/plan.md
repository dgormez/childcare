# Implementation Plan: Fiscal Attestations

**Branch**: `015-fiscal-attestations` | **Date**: 2026-07-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/015-fiscal-attestations/spec.md`

## Summary

Directors bulk-generate a per-child, per-tax-year Belgian fiscal attestation PDF (QuestPDF) from
each child's `Paid` invoices (014), split into up to four contiguous daily-rate periods and, if a
child was enrolled at more than one location that year, into one attestation per location. Unlike
014/014a's on-demand invoice/receipt PDFs, an attestation is rendered once and persisted to GCS
(deliberate departure — research.md R1) so a filed tax document stays a stable snapshot;
regenerating re-renders and overwrites the same object in place. Parents view/download their
child's attestation(s) from the parent app, and every linked contact is notified on generation or
regeneration, mirroring `InvoiceNotificationService` (014).

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript 5 / React 19 (`web/`,
`parent-mobile/`).

**Primary Dependencies**: MediatR + FluentValidation (Constitution III); EF Core 9 / PostgreSQL;
QuestPDF (Constitution's fixed PDF library, `QuestPdfInvoiceGenerator` is the direct structural
precedent); `Google.Cloud.Storage.V1` for server-side GCS writes (research.md R1 — mirrors
`GcsGroupActivityPhotoStorage`'s direct-`StorageClient.UploadObjectAsync` pattern, not the
client-signed-upload pattern used for health attachments, since the PDF is backend-rendered, not
user-uploaded); Next.js/Tailwind (`web/`, new `fiscal-attestations` route + sidebar entry); Expo/
React Native (`parent-mobile/`, extending the existing invoices-area download pattern —
`expo-file-system`/`expo-sharing`, already established by 014).

**Storage**: PostgreSQL, tenant schema. New `fiscal_attestations` table — one row per (ChildId,
LocationId, TaxYear), holding up to four rate periods as raw JSON (mirrors `Invoice.LineItems`'
precedent: JSON text validated in the Application layer, not a JSONB EF value converter), a total
amount, and a GCS object-path reference. Full shape in data-model.md.

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, Constitution V) — period
aggregation across a mid-year rate change and the NRN-never-persisted assertion are exactly the
correctness/compliance logic this principle calls out for real-database tests. Vitest + Testing
Library (`web/`). Jest + `@testing-library/react-native` (`parent-mobile/`).

**Target Platform**: Director web (bulk generation, per-child status, regenerate). Parent mobile
(view/download). No caregiver-tablet surface — caregivers have no billing/fiscal interaction,
same as 014.

**Performance Goals**: Not a hot/latency-sensitive path — an explicit, infrequent (year-end)
director action, same posture as 014's `GenerateInvoicesCommand`. Bulk generation runs
synchronously within the triggering HTTP request (research.md R4); per-child failure isolation
(FR-010) means one child's rendering/upload failure never aborts the batch.

**Constraints**: NRN/SSIN MUST NEVER be persisted anywhere (FR-007/FR-015) — no column, log, or
intermediate structure captures it. Money in integer cents throughout (matching 014's
convention). Regenerating overwrites the existing attestation/PDF in place — no duplicate row,
no orphaned prior GCS object (FR-008/FR-009).

**Scale/Scope**: One new tenant-schema entity, one new GCS-backed storage port + QuestPDF
generator, ~5 new endpoints (director bulk-generate/list/regenerate, parent list/download), one
new director-web screen + sidebar entry, one new parent-mobile screen extending the existing
invoices-area pattern, one new notification type + service.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation (NON-NEGOTIABLE) | ✅ Pass | `FiscalAttestation` is a tenant-schema entity, read/written exclusively through `ITenantDbContext`, identical to every existing feature (`Invoice`, `HealthRecord`, etc.). No cross-tenant lookup is needed — unlike 014a's payment webhook, nothing here is triggered from outside a tenant's own authenticated session. |
| II. Regulatory Compliance by Design (NON-NEGOTIABLE) | ✅ Pass | This feature *is* a regulatory-compliance feature (Belgian fiscal attest 281.86, `workflows.md`'s Government Reporting & Compliance workflow). The constitution's own Phase-scoping bullet explicitly names "fiscal attestations" as Phase 2, matching this feature's BACKLOG.md placement — no premature-phase violation. Per the constitution's regulatory-source rule, the Belcotax-on-web legal note (deadline, mandatory federal model attest, manual-entry-satisfies-MVP) is the verified 2026-07 fact already embedded in BACKLOG.md's prompt block and cited in spec.md's Out of Scope/Assumptions — `docs/integrations/opgroeien/README.md` has no dedicated archived file for 015 (its "Related external references" section lists the Belcotax-on-web URL and Claude-markdown corpus pointers only, feature 019 item 5's future scope), so this plan cites that external reference rather than inventing a local document. |
| III. CQRS via MediatR & Thin Endpoints | ✅ Pass | Bulk-generate/regenerate are MediatR commands with FluentValidation; list (director/parent)/download-url are MediatR queries. `FiscalAttestationEndpoints.cs` contains only route/DTO mapping. |
| IV. Internationalization First (NON-NEGOTIABLE) | ✅ Pass, must verify at implementation | All new strings (director screen labels/status, parent screen labels, notification copy, and the PDF's declaration/label text) go through `web/i18n/locales/{en,fr,nl}.json`, `parent-mobile/i18n/locales/{en,fr,nl}.json`, and a per-locale `Labels` dictionary on the new PDF generator, mirroring `QuestPdfInvoiceGenerator`'s existing pattern exactly. |
| V. Test with Real Infrastructure (NON-NEGOTIABLE) | ✅ Pass, must verify at implementation | Period-aggregation correctness (mid-year rate change, >4-period consolidation, multi-location split), the NRN-never-persisted assertion, and regenerate-overwrites-in-place are exactly the money-correctness/compliance logic this principle calls out for real-PostgreSQL integration tests. |
| VI. Secure Configuration & Storage | ✅ Pass | PDF access is via time-limited GCS signed URLs only (`UrlSigner`, mirrors every existing `Gcs*Storage` port) — no public blob URL. The server-side write uses the API's own GCS credentials (`GoogleCredential.GetApplicationDefaultAsync`), same as `GcsGroupActivityPhotoStorage`. No secret hardcoded. The NRN/SSIN constraint (FR-007) is this feature's sharpest instance of this principle's spirit even though the constitution text doesn't name NRN specifically — a blank PDF field, never a stored value, never logged. |
| VII. Monolith-First Simplicity | ✅ Pass | No new backend project, no new deployable, no scheduled-job infrastructure needed (unlike 014a) — bulk generation is a synchronous, director-triggered MediatR command within the existing API, matching 014's `GenerateInvoicesCommand` shape. |

No unjustified violations. No Complexity Tracking entries needed.

## Project Structure

### Documentation (this feature)

```text
specs/015-fiscal-attestations/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── fiscal-attestations-api.md  # Phase 1 output
└── tasks.md              # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/FiscalAttestation.cs                    # NEW (tenant schema)
│   └── Enums/NotificationType.cs                         # extended: FiscalAttestationGenerated
├── ChildCare.Application/
│   ├── Common/IFiscalAttestationPdfGenerator.cs          # NEW (research.md R2, mirrors IInvoicePdfGenerator)
│   ├── Common/IFiscalAttestationStorage.cs               # NEW (research.md R1, mirrors IGroupActivityPhotoStorage)
│   ├── FiscalAttestations/
│   │   ├── FiscalAttestationPeriods.cs                   # NEW — JSON line_items shape (mirrors InvoiceLineItems)
│   │   ├── FiscalAttestationAggregator.cs                # NEW (research.md R3 — Paid-invoice → period aggregation, shared by both commands below)
│   │   ├── GenerateFiscalAttestationsCommand.cs           # NEW — bulk, per tax year (research.md R4)
│   │   ├── RegenerateFiscalAttestationCommand.cs          # NEW — single child+location
│   │   ├── ListFiscalAttestationsQuery.cs                 # NEW — director, per tax year
│   │   ├── GetParentFiscalAttestationsQuery.cs             # NEW (mirrors GetParentInvoicesQuery)
│   │   ├── FiscalAttestationMapper.cs                      # NEW
│   │   └── FiscalAttestationNotificationService.cs         # NEW (mirrors InvoiceNotificationService)
│   └── Common/DownloadUrlResult.cs                         # reused if present, else inline on the query response
├── ChildCare.Contracts/
│   ├── Requests/FiscalAttestationRequests.cs                # NEW
│   └── Responses/FiscalAttestationResponse.cs                # NEW
├── ChildCare.Infrastructure/
│   ├── Pdf/QuestPdfFiscalAttestationGenerator.cs             # NEW (mirrors QuestPdfInvoiceGenerator)
│   ├── Storage/GcsFiscalAttestationStorage.cs                 # NEW (mirrors GcsGroupActivityPhotoStorage)
│   └── Persistence/
│       ├── TenantDbContext.cs                                 # extended: FiscalAttestations DbSet
│       └── Migrations/Tenant/                                  # NEW migration
├── ChildCare.Api/
│   └── Endpoints/FiscalAttestationEndpoints.cs                 # NEW

web/
├── app/(app)/fiscal-attestations/page.tsx                      # NEW
├── components/Sidebar.tsx                                       # extended: new nav entry
├── components/fiscal-attestations/FiscalAttestationTable.tsx    # NEW
├── lib/types.ts                                                  # extended
└── i18n/locales/{en,fr,nl}.json                                  # extended

parent-mobile/
├── app/(app)/fiscal-attestations/index.tsx                       # NEW (list, per tax year)
├── services/fiscalAttestations.ts                                 # NEW (mirrors services/invoices.ts's download pattern)
└── i18n/locales/{en,fr,nl}.json                                    # extended
```

**Structure Decision**: A new `FiscalAttestations` slice across the existing five backend
projects (no new project — Constitution VII), one new GCS-backed storage port (the deliberate
persisted-PDF departure from 014/014a, research.md R1), and one new screen per platform
(director-web gets its own sidebar entry, matching `invoices`' existing flat top-level placement
— no "Billing" parent grouping exists to nest under; parent-mobile gets a new screen extending
the existing invoices-area download pattern rather than a new mechanism).
