# Implementation Plan: Monthly Menu

**Branch**: `013e-monthly-menu` | **Date**: 2026-07-14 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/013e-monthly-menu/spec.md`

## Summary

A director-authored, per-location monthly meal menu (draft → publish → parents see it; unpublish
→ edit → republish for corrections) plus a parent-submitted meal-preference-change request
reviewed by the director. Three new tenant-schema tables (`MonthlyMenu`, `MonthlyMenuDay`,
`MealPreferenceChangeRequest`). No new persistence mechanism for the preference data itself —
approving a request writes through the existing `MealPreference` entity (013d) via its existing
`UpsertMealPreferenceCommand`. Reuses 013a's exact authorization primitive
(`ICurrentParentContactResolver` + `ChildContacts`) and exact notification pattern
(`IExpoPushSender` + in-app `Notification`, distinct body key with/without a reason). Adds the
first parent-scoped closure-day read (reusing `IClosureCalendarReader`, not the existing
`DirectorOnly` closure query). Ships a director-web "Menu" section (flat route + location
selector, mirroring 013d's `meal-list` page) and a new parent-mobile "Menu" tab (new, unlike
013a's tab-less `requests/` — this is a persistent top-level destination per the spec's UX
requirements).

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript (web, Next.js 15 App Router;
parent-mobile, Expo/React Native).

**Primary Dependencies**: MediatR, FluentValidation, EF Core 9 (backend, no new package). Web:
Tailwind + shadcn/ui, `next-intl`, openapi-fetch generated client (no new package). Parent-mobile:
`expo-router`, `react-i18next`, `expo-localization`, `lucide-react-native` (no new package).

**Storage**: PostgreSQL 16, schema-per-tenant — one additive migration adding
`monthly_menus`, `monthly_menu_days`, `meal_preference_change_requests` to the tenant schema.

**Testing**: xUnit + Moq + TestContainers-provisioned PostgreSQL (backend, Constitution V); Jest +
React Testing Library (web, 007a's precedent); Jest + `@testing-library/react-native`
(parent-mobile, 013c's cache-fallback test precedent).

**Target Platform**: Director web (desktop, 1280px+); parent mobile (Expo, portrait); backend
(ASP.NET Core Minimal API, Cloud Run).

**Project Type**: Web application — existing monorepo (`backend`, `web`, `parent-mobile`), no new
project.

**Performance Goals**: A month's menu is ≤31 day-rows — no pagination, single read query per
location per request.

**Constraints**: EF Core migrations MUST NOT auto-apply in production (Constitution VI); all
user-facing strings MUST use i18n keys NL/FR/EN (Constitution IV, on both `web/i18n/locales/` and
`parent-mobile/i18n/locales/`); menu write/publish/unpublish and preference-request review MUST
stay `DirectorOnly`; menu read and preference-request submit MUST stay `ParentOnly`; neither
surface is reachable by the caregiver tablet (FR-019).

**Scale/Scope**: Three new backend tables + one migration, ~6 new director-web endpoints, ~5 new
parent endpoints, one new web route (+ sidebar entry), one new parent-mobile tab + its
preference-request sub-screen, one new notification type.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
| --- | --- | --- |
| I. Multi-Tenant Isolation | Pass | All new endpoints sit behind existing `TenantMiddleware`; all new tables live in the tenant schema; every query goes through `ITenantDbContext`. |
| II. Regulatory Compliance by Design | N/A | No BKR ratio, contract-overlap, or closure-notification rule is implemented or altered — this feature reads existing closure data, it does not compute or enforce closure rules itself. |
| III. CQRS via MediatR & Thin Endpoints | Pass | All new writes (`UpsertMonthlyMenuCommand`, `Publish`/`UnpublishMonthlyMenuCommand`, `SubmitMealPreferenceChangeRequestCommand`, `Approve`/`RejectMealPreferenceChangeRequestCommand`) are MediatR commands; all new reads are MediatR queries; `MonthlyMenuEndpoints.cs`/`MealPreferenceChangeRequestEndpoints.cs` map HTTP only. |
| IV. Internationalization First | Pass | All new UI strings (Menu tab labels, "Voorkeur aanpassen", "Menu nog niet beschikbaar", request-status labels, decision notification text) ship as NL/FR/EN keys on both `web/i18n/locales/` and `parent-mobile/i18n/locales/`. |
| V. Test with Real Infrastructure | Pass | Backend tests for menu CRUD/publish state, request duplicate-pending rejection, approve-writes-through-to-MealPreference, and authorization/tenant-scoping negative flows all run against TestContainers PostgreSQL. |
| VI. Secure Configuration & Storage | Pass | Migration authored as a normal EF Core migration file; SQL script generated and run manually, no auto-apply. No new secret/storage surface. |
| VII. Monolith-First Simplicity | Pass | No new project or service — extends the existing five-project backend, `web`, and `parent-mobile`. |

No violations — Complexity Tracking is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/013e-monthly-menu/
├── plan.md               # This file
├── research.md            # Phase 0 output
├── data-model.md          # Phase 1 output
├── quickstart.md          # Phase 1 output
├── contracts/
│   └── monthly-menu-api.md
└── tasks.md               # Phase 2 output (/speckit-tasks — not yet created)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/Entities/
│   ├── MonthlyMenu.cs                                              # NEW
│   ├── MonthlyMenuDay.cs                                           # NEW
│   └── MealPreferenceChangeRequest.cs                              # NEW
├── ChildCare.Domain/Enums/
│   └── MealPreferenceChangeRequestStatus.cs                        # NEW
│   # (NotificationType.cs — MODIFIED, + MealPreferenceRequestDecided)
├── ChildCare.Application/MonthlyMenus/
│   ├── GetMonthlyMenuQuery.cs                                      # NEW — director read
│   ├── UpsertMonthlyMenuCommand.cs                                 # NEW — director write
│   ├── PublishMonthlyMenuCommand.cs                                # NEW
│   ├── UnpublishMonthlyMenuCommand.cs                               # NEW
│   └── GetParentMonthlyMenuQuery.cs                                # NEW — parent read (research.md R4/R5)
├── ChildCare.Application/MealPreferenceRequests/
│   ├── SubmitMealPreferenceChangeRequestCommand.cs                 # NEW — parent write (research.md R6)
│   ├── GetParentChildMealPreferenceQuery.cs                        # NEW — parent read
│   ├── ListMealPreferenceChangeRequestsQuery.cs                    # NEW — director queue (+ 013c health-record join)
│   ├── ApproveMealPreferenceChangeRequestCommand.cs                # NEW — sends UpsertMealPreferenceCommand (research.md R1)
│   ├── RejectMealPreferenceChangeRequestCommand.cs                 # NEW
│   └── MealPreferenceRequestNotificationService.cs                 # NEW — mirrors DayReservationNotificationService (research.md R3)
├── ChildCare.Contracts/
│   ├── Requests/MonthlyMenuRequests.cs                             # NEW
│   ├── Requests/MealPreferenceRequestRequests.cs                   # NEW
│   ├── Responses/MonthlyMenuResponse.cs                            # NEW
│   └── Responses/MealPreferenceChangeRequestResponse.cs            # NEW
├── ChildCare.Api/Endpoints/
│   ├── MonthlyMenuEndpoints.cs                                     # NEW — director group + parent group
│   └── MealPreferenceRequestEndpoints.cs                           # NEW — director group + parent group
├── ChildCare.Infrastructure/Persistence/
│   ├── TenantDbContext.cs                                          # + 3 new DbSets
│   └── Migrations/Tenant/<timestamp>_AddMonthlyMenuAndMealPreferenceRequests.cs
└── ChildCare.Api.Tests/
    ├── MonthlyMenus/MonthlyMenuTests.cs                            # draft/publish/unpublish, parent-visibility negative flows
    ├── MealPreferenceRequests/MealPreferenceRequestTests.cs        # duplicate-pending, approve-writes-through, reject-unchanged, authorization
    └── TenantMigrationRolloutTests.cs                               # MODIFIED — revert-helper (research.md R7)

web/
├── app/(app)/menu/page.tsx                                         # NEW — director authoring, mirrors meal-list/page.tsx
├── components/menu/
│   ├── MonthlyMenuDayGrid.tsx                                      # NEW
│   └── MealPreferenceRequestQueue.tsx                              # NEW — review queue + approve/reject
├── components/Sidebar.tsx                                          # + "Menu" nav entry
├── i18n/locales/{en,fr,nl}.json                                    # + menu.* / mealPreferenceRequests.* keys
├── lib/generated/api-types.ts                                      # regenerated (mechanical)
└── __tests__/ — MonthlyMenuDayGrid, MealPreferenceRequestQueue

parent-mobile/
├── app/(app)/_layout.tsx                                           # MODIFIED — + "menu" Tabs.Screen entry
├── app/(app)/menu/index.tsx                                        # NEW — month grid + child preference indicator
├── app/(app)/menu/request-preference-change.tsx                    # NEW — preference-change form
├── services/menu.ts                                                # NEW — fetch + cache-fallback, mirrors healthSummary.ts (013c)
├── services/mealPreferenceRequests.ts                               # NEW — submit, mirrors dayReservations.ts
├── i18n/locales/{en,fr,nl}.json                                    # + menu.* keys
└── __tests__/ — menu screen rendering, offline-cache-fallback case, request-submission form
```

**Structure Decision**: No new top-level project. Backend adds two new vertical slices
(`MonthlyMenus/`, `MealPreferenceRequests/` in `Application`, mirroring the existing
one-folder-per-feature convention) rather than folding preference-requests into `MealPreferences/`
(013d) — kept separate because a *request* is a distinct workflow object (has its own lifecycle,
authorization surface, and director queue) from the *preference* it may eventually write to; only
the write-through (R1) couples them, not the folder structure. Web adds one new flat route
(`menu/`) plus a `components/menu/` directory (mirrors `components/meal-list/`) and one sidebar
entry. Parent-mobile adds a genuine new tab (unlike 013a's tab-less `requests/`) since the spec's
UX requirements treat the menu as a persistent, frequently-revisited destination, not an
occasional request flow — plus one new service module per read/write concern, following
`healthSummary.ts`/`dayReservations.ts`'s established shapes exactly.
