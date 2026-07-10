# Implementation Plan: Waiting List Management

**Branch**: `012a-waiting-list` | **Date**: 2026-07-10 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/012a-waiting-list/spec.md`

## Summary

Build the `waiting_list_entries` tenant-schema table and a director-web screen to register,
prioritize (per location), and progress families through a `waiting → offered →
enrolled/withdrawn` status lifecycle, plus a forward-looking occupancy view and a manual
child-record link on enrollment. During specification, research confirmed BACKLOG's original
premise ("occupancy reads from attendance") doesn't hold — feature 010's attendance data is
same-day/historical and doesn't exist for the future dates this view actually needs — so
occupancy is instead projected from active `Contract`s (007) against `Location.MaxCapacity`
(004), with published `KdvClosureDay`s (011) marking a date `Closed` rather than a numeric
count.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Next.js 15 App Router (web admin)

**Primary Dependencies**: ASP.NET Core Minimal APIs, EF Core 9, MediatR, FluentValidation,
MailKit (existing `IEmailSender`/`EmailService`) (backend); Tailwind, shadcn/ui, openapi-fetch,
next-intl (web)

**Storage**: PostgreSQL 16, tenant schema — new `waiting_list_entries` table

**Testing**: xUnit + Moq + TestContainers-provisioned PostgreSQL (backend, constitution
Principle V — no InMemory provider); Vitest + `@testing-library/react` (web, jsdom
environment, per feature 007a's precedent)

**Target Platform**: Cloud Run (backend API), browser (director web, desktop ≥1280px)

**Project Type**: Web application — backend API + Next.js director-web admin (no mobile/parent
UI in this feature; parent self-registration is feature 023)

**Performance Goals**: Waiting list (tens to low hundreds of rows per tenant) loads and
re-sorts without pagination; occupancy query for a date range is a single set-based query, not
N per-date round trips

**Constraints**: All endpoints `DirectorOnly`; occupancy MUST NOT read from attendance records
(research.md decision); a closure day MUST render as `Closed`, never a numeric free-capacity
count; status transitions restricted to the FR-007 allow-list; email notification on `offered`
reuses the existing `IEmailSender` port rather than introducing a second mechanism

**Scale/Scope**: Single-tenant scale (tens to low hundreds of waiting-list entries, 1–3
locations per organisation in Phase 1) — no virtualization or server-side pagination needed at
this scale

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation | PASS | `waiting_list_entries` lives in the tenant schema, no explicit tenant FK column — same structural pattern as `Location`/`Contract`/`StaffSchedule` (004/007/012). All access goes through `TenantDbContext`/`ITenantDbContext`. |
| II. Regulatory Compliance by Design (BKR) | N/A | This feature has no BKR/ratio surface — it is pre-enrollment data with no caregiver-to-child ratio implication. |
| III. CQRS via MediatR & Thin Endpoints | PASS | All writes (create, update, reorder, status transition, child-link) are MediatR commands; list and occupancy are MediatR queries; `WaitingListEndpoints.cs` maps HTTP only. |
| IV. Internationalization First | PASS | All new director-web strings added as `next-intl` keys (NL/FR/EN); no hardcoded text. API error responses return `errorKey` values, matching every existing endpoint's pattern. The `offered`-notification email body follows the existing English-only raw-HTML precedent (`SendStaffInvitationAsync`, feature 005) — feature 019 owns the email-templating/i18n rework, not this feature. |
| V. Test with Real Infrastructure | PASS | Backend integration tests run against TestContainers PostgreSQL, covering the full status lifecycle (including rejected invalid transitions), per-location priority reorder, duplicate-flagging, occupancy computation (including closure-day exclusion), and enrollment child-linking. |
| VI. Secure Configuration & Storage | N/A | No secrets, no file storage, no PDF in this feature. Email uses the existing `IEmailSender`/`EmailService`, already configured via environment variables (feature 001/003 precedent). |
| VII. Monolith-First Simplicity | PASS | No new projects; new code lives in the existing five-project solution (`ChildCare.Domain`/`Application`/`Contracts`/`Api`/`Infrastructure`) and the existing `web/` app. |

No violations — Complexity Tracking table not needed.

## Project Structure

### Documentation (this feature)

```text
specs/012a-waiting-list/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── waiting-list-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/
│   │   └── WaitingListEntry.cs                      # new
│   └── Enums/
│       └── WaitingListStatus.cs                     # new: Waiting/Offered/Enrolled/Withdrawn
├── ChildCare.Application/
│   ├── Common/
│   │   ├── ITenantDbContext.cs                      # +DbSet<WaitingListEntry>
│   │   └── IEmailSender.cs                           # +SendWaitingListOfferedAsync
│   └── WaitingList/                                  # new folder, mirrors ClosureCalendar/
│       ├── WaitingListResult.cs                      # failure enum + result records
│       ├── CreateWaitingListEntryCommand.cs
│       ├── UpdateWaitingListEntryCommand.cs
│       ├── ReorderWaitingListEntryCommand.cs          # up/down within a location's queue
│       ├── TransitionWaitingListStatusCommand.cs      # FR-007 allow-list enforcement
│       ├── LinkChildToWaitingListEntryCommand.cs      # FR-010/FR-011 (creates child if needed)
│       ├── ListWaitingListEntriesQuery.cs             # director list, filter/sort
│       └── GetOccupancyQuery.cs                       # FR-013/FR-014/FR-015
├── ChildCare.Contracts/
│   ├── Requests/
│   │   └── WaitingListRequests.cs
│   └── Responses/
│       └── WaitingListResponses.cs
├── ChildCare.Infrastructure/
│   └── Persistence/
│       ├── TenantDbContext.cs                        # +modelBuilder config, index (location_id, status, priority)
│       └── Migrations/                                # new EF migration
├── ChildCare.Api/
│   ├── Endpoints/
│   │   └── WaitingListEndpoints.cs                    # new, mirrors ClosureCalendarEndpoints.cs
│   └── Services/
│       └── EmailService.cs                            # +SendWaitingListOfferedAsync
└── ChildCare.Api.Tests/
    └── WaitingList/
        ├── WaitingListEndpointsTests.cs
        ├── StatusTransitionTests.cs                    # allow-list + rejected transitions
        └── OccupancyTests.cs                            # capacity math + closure-day exclusion

web/
├── app/(app)/waiting-list/
│   └── page.tsx                                       # new — list + occupancy panel, data loading/actions
├── components/
│   ├── WaitingListTable.tsx                            # new — sortable/filterable table, reorder actions
│   ├── OccupancyPanel.tsx                               # new — per-date free-capacity / Closed display
│   ├── WaitingListEntryDialog.tsx                       # new — create/edit form
│   └── EnrollChildLinkDialog.tsx                        # new — link existing / "create child record now?"
├── lib/generated/api-types.ts                           # regenerated (openapi-typescript)
├── components/Sidebar.tsx                               # +Waiting List nav entry
└── i18n/locales/{en,fr,nl}.json                         # +waitingList.* keys
```

**Structure Decision**: Standard web-application layout already established by this monorepo —
backend feature code follows the `ClosureCalendar`/`StaffScheduling` module precedent (one
`Application/<Feature>/` folder, one `Endpoints.cs` file, entities in `Domain/Entities`), web
feature code adds one route under `web/app/(app)/`. No new projects, no new top-level
directories.

## Complexity Tracking

*No Constitution Check violations — table not needed.*
