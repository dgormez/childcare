# Implementation Plan: Management Reporting

**Branch**: `018-management-reporting` | **Date**: 2026-07-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/018-management-reporting/spec.md`

## Summary

A read-only director dashboard aggregating five report sections — today/week-ahead occupancy
(per group and location, colour-coded), live per-group BKR compliance plus a reconstructed breach
history, a monthly attendance summary exportable as CSV/PDF, a current-month invoice status
overview, and a data-completeness monitor — entirely from existing tenant tables. The one schema
change is a nullable `Capacity` column on `Group`, needed to colour-code per-group occupancy the
way `Location.MaxCapacity` already does at the location level. No new persisted reporting
schema; BKR breach history is reconstructed on demand from existing check-in/check-out
timestamps rather than a new event log (research.md R3).

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript 5 / React 19 (`web/`, director-only —
this feature has no caregiver/parent surface).

**Primary Dependencies**: MediatR + FluentValidation (Constitution III) for every new query;
EF Core 9 / PostgreSQL (`ITenantDbContext` only — no public-schema data involved, unlike 016/013g);
QuestPDF (constitution's fixed PDF library) for the attendance-summary PDF export, following
`QuestPdfInvoiceGenerator`'s on-demand/unstored pattern; a new CSV writer (first CSV export in
this codebase — research.md R8); Next.js/Tailwind extending the existing
`web/app/(app)/dashboard/page.tsx`.

**Storage**: PostgreSQL, tenant schema only. One new nullable column (`Group.Capacity`); every
other read is against existing tables (`AttendanceRecord`, `Invoice`, `Contract`, `StaffSchedule`,
`RoomShift`, `Group`, `ChildGroupAssignment`, `Child`, `ChildContact`, `VaccineRecord`,
`StaffProfile`). Full shape in data-model.md.

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, Constitution V) — the
midnight calendar-day boundary, the multi-contract/multi-location attendance rollup, the
BKR-breach reconstruction, and cross-tenant isolation are exactly the correctness/isolation logic
this principle calls out for real-database tests. Vitest + Testing Library (`web/`).

**Target Platform**: Director web only (per platform-rules.md, desktop-first, min `1280px`). No
caregiver tablet or parent mobile surface.

**Performance Goals**: Each dashboard section is a bounded, indexed aggregation over one tenant's
data for a single day/month/short date range — not a hot path, but each section must load
independently so one slow query (e.g. a wide BKR-breach range) never blocks the rest of the
dashboard from rendering (spec.md's Loading/empty/error states requirement).

**Constraints**: No new persisted reporting schema or data warehouse (BACKLOG.md's explicit
constraint) — every report is a live query against existing tables, with indexes added where
needed. BKR breach history is computed by replaying existing timestamps, not stored
(research.md R3). "Today" is always `BelgianCalendarDay`, never a rolling 24h window (FR-016).

**Scale/Scope**: One schema change (`Group.Capacity`). Six new read-only endpoints (occupancy,
BKR live, BKR breaches, attendance summary + its CSV/PDF export, invoice overview, data
completeness). One new CSV writer, one new QuestPDF generator. One extended director-web page
(`dashboard/page.tsx`) with five new sections/components and a shared location filter. No changes
to `mobile/` or `parent-mobile/`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation (NON-NEGOTIABLE) | ✅ Pass | Every new query reads exclusively through `ITenantDbContext`; the optional `locationId` filter only ever narrows within the caller's own tenant (FR-012). No public-schema data involved. |
| II. Regulatory Compliance by Design (NON-NEGOTIABLE) | ✅ Pass | This feature does not implement or alter BKR enforcement — it *reports* the ratio feature 010 already computes and enforces, reusing its exact thresholds/status logic (research.md R2) rather than re-deriving them. Not a regulatory-source feature (015/019/033–041); no `docs/integrations/opgroeien/` citation needed. |
| III. CQRS via MediatR & Thin Endpoints | ✅ Pass | Every report is a MediatR query (no commands — this feature is entirely read-only); `ReportingEndpoints.cs` contains only route/DTO mapping. |
| IV. Internationalization First (NON-NEGOTIABLE) | ✅ Pass, must verify at implementation | All dashboard strings (section headers, empty states, flag reasons) go through `web/i18n/locales/{en,fr,nl}.json`; the PDF export uses a per-locale `Labels` dictionary mirroring `QuestPdfInvoiceGenerator`'s pattern. |
| V. Test with Real Infrastructure (NON-NEGOTIABLE) | ✅ Pass, must verify at implementation | Midnight-boundary, multi-contract-period aggregation, BKR-breach reconstruction, and cross-tenant rejection are exactly the correctness/isolation logic this principle calls out for real-PostgreSQL integration tests. |
| VI. Secure Configuration & Storage | ✅ Pass | No new persisted file — both CSV and PDF exports are rendered on-demand and streamed directly in the HTTP response, matching every existing export in this codebase. No secret hardcoded. Errors are logged server-side with a human-readable client message (FR-019). |
| VII. Monolith-First Simplicity | ✅ Pass | No new backend project, no background job, no new deployable — every report is a synchronous MediatR query within the existing API. |

No unjustified violations. No Complexity Tracking entries needed.

## Project Structure

### Documentation (this feature)

```text
specs/018-management-reporting/
├── plan.md              # This file
├── research.md           # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── management-reporting-api.md  # Phase 1 output
└── tasks.md              # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   └── Entities/Group.cs                                   # extended: + Capacity (int?)
├── ChildCare.Application/
│   └── Reporting/
│       ├── GetOccupancySummaryQuery.cs                      # NEW — today actual (per group/location) + week-ahead (reuses GetOccupancyQuery)
│       ├── GetGroupBkrRatioQuery.cs                         # NEW — per-group live ratio (research.md R2)
│       ├── GetBkrBreachHistoryQuery.cs                      # NEW — on-demand reconstruction (research.md R3)
│       ├── GetAttendanceSummaryQuery.cs                     # NEW — shared aggregation, feeds JSON/CSV/PDF
│       ├── ExportAttendanceSummaryQuery.cs                  # NEW — CSV/PDF export, reuses GetAttendanceSummaryQuery's result
│       ├── GetInvoiceStatusOverviewQuery.cs                 # NEW — reuses Invoice.Status/DueDate convention (research.md R6)
│       ├── GetDataCompletenessQuery.cs                      # NEW — four checks (research.md R7)
│       └── ReportingMapper.cs                                # NEW
├── ChildCare.Contracts/
│   └── Responses/ReportingResponses.cs                       # NEW
├── ChildCare.Infrastructure/
│   ├── Reporting/CsvAttendanceSummaryWriter.cs                # NEW — first CSV export in this codebase (research.md R8)
│   ├── Pdf/QuestPdfAttendanceSummaryGenerator.cs              # NEW (mirrors QuestPdfInvoiceGenerator)
│   └── Persistence/
│       ├── TenantDbContext.cs                                  # extended: Group.Capacity column mapping
│       └── Migrations/Tenant/                                  # NEW migration: groups.capacity
├── ChildCare.Api/
│   └── Endpoints/ReportingEndpoints.cs                         # NEW

web/
├── app/(app)/dashboard/page.tsx                                 # extended: five new sections + location filter
├── components/reporting/
│   ├── OccupancySection.tsx                                     # NEW
│   ├── BkrComplianceSection.tsx                                 # NEW
│   ├── AttendanceSummarySection.tsx                             # NEW (+ CSV/PDF export controls)
│   ├── InvoiceStatusSection.tsx                                 # NEW
│   ├── DataCompletenessSection.tsx                              # NEW
│   └── LocationFilter.tsx                                       # NEW — shared across all five sections
└── i18n/locales/{en,fr,nl}.json                                  # extended
```

**Structure Decision**: A new `Reporting` slice across the existing five backend projects (no new
project — Constitution VII), entirely read-only (no commands, no new tenant tables beyond the one
nullable column), reusing `ITenantDbContext` and the existing `QuestPdfInvoiceGenerator`/
`GetOccupancyQuery`/`GetBkrRatioQuery` patterns directly. Director web gets one extended page
(the existing `dashboard/page.tsx`, per its own comment anticipating this) with five new section
components — no new route, no new nav entry, matching how this feature is purely additive to an
existing screen.

## Complexity Tracking

No violations — table intentionally omitted.
