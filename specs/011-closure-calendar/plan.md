# Implementation Plan: Closure Calendar

**Branch**: `011-closure-calendar` | **Date**: 2026-07-09 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/011-closure-calendar/spec.md`

## Summary

Add tenant-scoped, per-location KDV closure days with a director-web year calendar, draft/publish/cancel lifecycle, immediate parent push plus one-way in-app closure messages, attendance `closure` record generation, and a query surface for future invoicing exclusions. Reuse existing Minimal API + MediatR + EF Core tenant patterns and the existing Next.js director-web shell/components.

## Technical Context

**Language/Version**: C# / .NET 10 backend; TypeScript / Next.js director web.

**Primary Dependencies**: ASP.NET Core Minimal APIs, MediatR, FluentValidation, EF Core 10, Npgsql, Next.js, next-intl, openapi-fetch, lucide-react, existing `IExpoPushSender`.

**Storage**: PostgreSQL tenant schema via `TenantDbContext`.

**Testing**: xUnit + ASP.NET Core integration tests/Testcontainers for backend; Vitest + React Testing Library for web.

**Target Platform**: Backend API plus director web. Caregiver tablet observes existing attendance behavior; no mobile UI changes planned.

**Project Type**: Web application with backend API and director web frontend.

**Performance Goals**: Location-year closure query under 500ms for up to 20 locations and 250 closure records per tenant; same-day publish starts push processing within 10 seconds.

**Constraints**: Tenant isolation through `TenantDbContext`; `DirectorOnly` for closure management; all system-authored user-facing strings use i18n keys; no email fallback/calendar export/reminder rules; director web remains desktop-first/dense with semantic tokens and no hardcoded colors.

**Scale/Scope**: One tenant, one selected location/year per calendar page; parent recipients deduplicated by contact/parent account across children.

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Multi-Tenant Isolation | PASS | All closure data lives in tenant schema and endpoints validate location ownership in the current tenant. |
| II. Regulatory Compliance by Design | PASS | Closure-calendar notification and attendance-blocking rules are enforced in backend Application/Domain logic, not UI only. |
| III. Technology Stack Constraints | PASS | Uses existing .NET/EF/MediatR/Next.js stack. |
| IV. Internationalization | PASS | API error keys, web UI, and parent notification/message templates use i18n keys; director-entered labels remain user content. |
| V. Testable Workflow Slices | PASS | User stories are independently testable via backend integration and web component tests. |
| VI. Secure Configuration & Storage | PASS | No new secrets; migrations are normal reviewed tenant migrations. |

## Project Structure

### Documentation (this feature)

```text
specs/011-closure-calendar/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── closure-calendar-api.md
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/
│   │   ├── KdvClosureDay.cs
│   │   ├── ClosureNotificationDelivery.cs
│   │   └── ParentClosureMessage.cs
│   └── Enums/
│       ├── ClosureType.cs
│       ├── ClosureStatus.cs
│       └── ClosureNotificationKind.cs
├── ChildCare.Application/
│   ├── ClosureCalendar/
│   │   ├── CreateClosureDayCommand.cs
│   │   ├── UpdateClosureDayCommand.cs
│   │   ├── PublishClosureDayCommand.cs
│   │   ├── CancelClosureDayCommand.cs
│   │   ├── ListClosureDaysQuery.cs
│   │   ├── ClosureAttendanceService.cs
│   │   ├── ClosureNotificationService.cs
│   │   └── ClosureCalendarResult.cs
│   └── Common/
│       └── IClosureCalendarReader.cs
├── ChildCare.Contracts/
│   ├── Requests/ClosureCalendarRequests.cs
│   └── Responses/ClosureCalendarResponses.cs
├── ChildCare.Api/
│   └── Endpoints/ClosureCalendarEndpoints.cs
└── ChildCare.Api.Tests/
    └── ClosureCalendarTests.cs

web/
├── app/(app)/closures/page.tsx
├── components/ClosureCalendar.tsx
├── components/ClosureDialog.tsx
├── components/ClosureList.tsx
├── __tests__/closures.test.tsx
└── i18n/locales/{en,fr,nl}.json
```

**Structure Decision**: Use a dedicated backend `ClosureCalendar` application folder following the `Attendance` and `ChildEvents` command/query pattern, plus a dedicated director web route under the existing app shell. Add no new project/package boundary.

## Complexity Tracking

No constitution violations.
