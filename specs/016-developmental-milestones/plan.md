# Implementation Plan: Developmental Milestones

**Branch**: `016-developmental-milestones` | **Date**: 2026-07-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/016-developmental-milestones/spec.md`

## Summary

Caregivers record append-only observations (`emerging`/`achieved`/`not_yet`) against a shared,
platform-wide catalog of developmental domains and milestones (the standard Belgian framework),
from the caregiver tablet. Directors and parents view the same child's portfolio — grouped by
domain, most recent status per milestone, full history on demand, and the current age-appropriate
band highlighted (computed live from the child's age, never cached). An on-demand PDF export
(QuestPDF, unstored — mirrors invoice PDFs, not fiscal attestations) is available to both
directors and parents. The reference catalog lives in the shared public schema, following the
`VaccineType` (013g) precedent exactly, rather than inventing a new per-tenant seeding mechanism
that doesn't exist anywhere in this codebase (research.md R1).

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript 5 / React 19 (`web/`); TypeScript /
React Native (Expo) for `mobile/` (caregiver) and `parent-mobile/` (parent).

**Primary Dependencies**: MediatR + FluentValidation (Constitution III); EF Core 9 / PostgreSQL
(two `DbContext`s — `IPublicDbContext` for the shared catalog, `ITenantDbContext` for
observations, mirroring `VaccineType`/`VaccineRecord`'s split exactly); QuestPDF (constitution's
fixed PDF library, `QuestPdfInvoiceGenerator` is the direct structural precedent for on-demand,
unstored rendering); Next.js/Tailwind (`web/`, new tab on the existing child-profile page);
Expo/React Native for `mobile/` (new caregiver observation-entry sheet + timeline, matching
`EditEventModal.tsx`/`EventTimeline.tsx`) and `parent-mobile/` (new per-child "Development"
screen).

**Storage**: PostgreSQL. `developmental_domains` and `developmental_milestones` are **public
schema** tables (shared, platform-wide, admin-seeded — mirrors `VaccineType`). New tenant-schema
table `child_milestone_observations` — append-only, indexed by `ChildId` and `MilestoneId`, no DB
FK on `MilestoneId` (same precedent as `VaccineRecord.VaccineTypeId` — the referenced row lives in
a different schema, which PostgreSQL cannot FK across). Full shape in data-model.md.

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, Constitution V) — age-band
resolution at a boundary, append-only enforcement, and parent cross-family access rejection are
exactly the correctness/isolation logic this principle calls out for real-database tests. Vitest +
Testing Library (`web/`). Jest + `@testing-library/react-native` (`mobile/`, `parent-mobile/`).

**Target Platform**: Caregiver tablet (`mobile/`, record — primary). Director web (view/manage —
primary). Parent mobile (`parent-mobile/`, view + PDF download — primary).

**Performance Goals**: Not a hot/latency-sensitive path — recording is a single-row insert;
portfolio reads are a single indexed query per child, same posture as `ListChildVaccineRecordsQuery`.

**Constraints**: Observations are immutable — no update/delete endpoint or handler exists for
`child_milestone_observations` at all (not just policy-enforced; structurally absent, matching how
`VaccineRecord` at least has a delete path but this entity intentionally has none). Age-band
resolution MUST be computed at request time from the child's current date of birth, never stored
or cached against a point-in-time age. Reference catalog is read-only in this feature (no
create/update/deactivate endpoint) — out of scope per spec.md's Assumptions.

**Scale/Scope**: Two new public-schema catalog entities (read-only in this feature), one new
tenant-schema append-only entity, one new on-demand PDF generator, ~5 new endpoints (list catalog,
record observation, director portfolio view, parent portfolio view, PDF export), one new tab on
the existing director-web child-profile page, one new caregiver-tablet entry sheet + timeline
section, one new parent-mobile screen.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation (NON-NEGOTIABLE) | ✅ Pass | `ChildMilestoneObservation` is a tenant-schema entity, read/written exclusively through `ITenantDbContext`, identical to every existing per-child feature (`ChildEvent`, `VaccineRecord`). The shared catalog (`DevelopmentalDomain`/`DevelopmentalMilestone`) lives in the public schema and is read through `IPublicDbContext`, exactly like `VaccineType` — this is not a tenant-isolation bypass, since it holds no tenant/child data, only platform-wide reference text (research.md R1, same reasoning already accepted for 013g). |
| II. Regulatory Compliance by Design (NON-NEGOTIABLE) | ✅ Pass | Not a regulatory feature — developmental milestone tracking is a product/pedagogical capability, not a Belgian legal requirement (unlike BKR, MeMoQ, or the Government Reporting workflow). The constitution's own Phase-scoping bullet explicitly names "developmental milestones" as Phase 2, matching this feature's BACKLOG.md placement — no premature-phase violation, no regulatory-source citation needed. |
| III. CQRS via MediatR & Thin Endpoints | ✅ Pass | Recording is a MediatR command with FluentValidation; catalog list, director portfolio, parent portfolio, and PDF export are MediatR queries. `DevelopmentalMilestoneEndpoints.cs`/`MilestoneObservationEndpoints.cs` contain only route/DTO mapping. |
| IV. Internationalization First (NON-NEGOTIABLE) | ✅ Pass, must verify at implementation | Domain/milestone names and descriptions are stored NL/FR/EN in the catalog itself (spec FR-001/FR-009); all surrounding UI strings go through `mobile/i18n/locales/{en,fr,nl}.json`, `web/i18n/locales/{en,fr,nl}.json`, `parent-mobile/i18n/locales/{en,fr,nl}.json`; the PDF generator uses a per-locale `Labels` dictionary mirroring `QuestPdfInvoiceGenerator`'s exact pattern. |
| V. Test with Real Infrastructure (NON-NEGOTIABLE) | ✅ Pass, must verify at implementation | Age-band boundary resolution, append-only enforcement (no update/delete path reachable), and parent cross-family rejection are exactly the correctness/isolation logic this principle calls out for real-PostgreSQL integration tests. |
| VI. Secure Configuration & Storage | ✅ Pass | No new persisted file — the PDF is rendered on-demand and streamed directly in the HTTP response (same as invoice PDFs), so no GCS storage port or signed URL is needed for this feature. No secret hardcoded. |
| VII. Monolith-First Simplicity | ✅ Pass | No new backend project, no new deployable, no background job — recording and portfolio reads are synchronous MediatR requests within the existing API, matching `VaccineRecord`'s shape. |

No unjustified violations. No Complexity Tracking entries needed.

## Project Structure

### Documentation (this feature)

```text
specs/016-developmental-milestones/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── developmental-milestones-api.md  # Phase 1 output
└── tasks.md              # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/DevelopmentalDomain.cs                    # NEW (public schema)
│   ├── Entities/DevelopmentalMilestone.cs                 # NEW (public schema)
│   └── Entities/ChildMilestoneObservation.cs              # NEW (tenant schema, append-only)
├── ChildCare.Application/
│   ├── Common/IPublicDbContext.cs                          # extended: DevelopmentalDomains, DevelopmentalMilestones DbSets
│   ├── Common/ITenantDbContext.cs                          # extended: ChildMilestoneObservations DbSet
│   ├── Common/IMilestonePortfolioPdfGenerator.cs           # NEW (mirrors IInvoicePdfGenerator — on-demand, unstored)
│   ├── DevelopmentalMilestones/
│   │   ├── ListDevelopmentalMilestonesQuery.cs             # NEW — reads IPublicDbContext (mirrors ListVaccineTypesQuery)
│   │   ├── RecordMilestoneObservationCommand.cs            # NEW
│   │   ├── GetChildMilestonePortfolioQuery.cs               # NEW — director, full history
│   │   ├── GetParentMilestonePortfolioQuery.cs               # NEW — mirrors GetParentDailySummaryQuery's ChildContact check
│   │   ├── MilestonePortfolioBuilder.cs                      # NEW — shared age-band + grouping logic (research.md R2), used by both portfolio queries and the PDF export
│   │   └── DevelopmentalMilestoneMapper.cs                   # NEW
├── ChildCare.Contracts/
│   ├── Requests/MilestoneObservationRequests.cs               # NEW
│   └── Responses/DevelopmentalMilestoneResponses.cs           # NEW
├── ChildCare.Infrastructure/
│   ├── Pdf/QuestPdfMilestonePortfolioGenerator.cs              # NEW (mirrors QuestPdfInvoiceGenerator)
│   └── Persistence/
│       ├── PublicDbContext.cs                                  # extended: DevelopmentalDomains, DevelopmentalMilestones DbSets + seed
│       ├── TenantDbContext.cs                                  # extended: ChildMilestoneObservations DbSet
│       └── Migrations/
│           ├── Public/                                          # NEW migration: catalog tables + seed data
│           └── Tenant/                                          # NEW migration: child_milestone_observations
├── ChildCare.Api/
│   └── Endpoints/DevelopmentalMilestoneEndpoints.cs             # NEW

mobile/ (caregiver, Expo)
├── app/(app)/child/[id].tsx                                     # extended: "Milestones" entry point
├── components/milestones/MilestoneEntrySheet.tsx                # NEW (mirrors EditEventModal.tsx)
├── components/milestones/MilestoneTimeline.tsx                  # NEW (mirrors EventTimeline.tsx)
├── services/milestones.ts                                       # NEW (mirrors services/childEvents.ts — integrates offlineQueue)
└── i18n/locales/{en,fr,nl}.json                                  # extended

web/
├── app/(app)/children/[id]/page.tsx                              # extended: new "Milestones" tab
├── components/milestones/MilestonePortfolioView.tsx               # NEW (mirrors VaccineRecordForm.tsx's per-child section pattern)
└── i18n/locales/{en,fr,nl}.json                                   # extended

parent-mobile/
├── app/(app)/children/[id]/milestones.tsx (or a "Development" tab on the existing child screen — exact placement decided during implementation against the current parent-mobile navigation)  # NEW
├── services/milestones.ts                                        # NEW (mirrors services used by fiscal-attestations/invoices for PDF download)
└── i18n/locales/{en,fr,nl}.json                                   # extended
```

**Structure Decision**: A new `DevelopmentalMilestones` slice across the existing five backend
projects (no new project — Constitution VII), reusing the existing `IPublicDbContext`/
`ITenantDbContext` split rather than adding new infrastructure, and one new screen/section per
platform reusing each platform's existing per-child navigation (director-web gets a new tab
alongside `health`, caregiver-tablet gets a new entry point from the child profile mirroring the
child-event quick-action pattern, parent-mobile gets a new per-child section).

## Complexity Tracking

No violations — table intentionally omitted.
