# Implementation Plan: QR Contactless Check-In

**Branch**: `021-qr-checkin` | **Date**: 2026-07-20 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/021-qr-checkin/spec.md`

## Summary

Add a per-location, director-controlled, default-disabled setting that lets parents check a
child in/out by showing a 30-second-lived, server-issued, tamper-evident QR code in the parent
app for a caregiver's tablet to scan. A successful scan drives the exact same attendance
state-transition logic (`CheckInCommand`/`CheckOutCommand`) a manual tap already uses, so
`AttendanceRecord`, BKR ratio, and reporting are byte-identical regardless of origin. Manual tap
remains fully available everywhere, always, as the required fallback.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / React Native (Expo) for
`mobile/` (caregiver tablet) and `parent-mobile/` (parent app).

**Primary Dependencies**: ASP.NET Core Minimal APIs, MediatR, FluentValidation, EF Core 9,
PostgreSQL 16 (backend); Expo, `expo-camera` (new — caregiver-tablet scan), a QR-rendering
library (new — parent-mobile code display), existing `apiClient`/offline-queue/sync-engine
(both mobile apps).

**Storage**: PostgreSQL (new `Location.QrCheckInEnabled` column). The check-in code itself is
**not** persisted as a business entity — see research.md R1 for the issuance/verification
mechanism decision.

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, per Constitution
Principle V); existing Jest suites for `mobile/` and `parent-mobile/`.

**Target Platform**: director-web (Next.js), mobile/caregiver tablet (Expo, landscape,
existing kiosk session from feature 008a), parent-mobile (Expo, portrait).

**Project Type**: Mixed — backend API extension + three client UI additions (web
settings tab, tablet scan mode, parent code-display screen), per spec.md's Product Context.

**Performance Goals**: Scan-to-confirmation under 10 seconds (SC-003) — code verification must
be a lightweight signature/TTL/cooldown check plus the existing attendance-write path, no new
heavyweight computation.

**Constraints**: Code TTL 30s (refresh at ~20s, per spec.md Clarifications); post-consumption
cooldown (FR-019); zero behavior change for any location that hasn't opted in (SC-002); offline
scans reuse feature 008's existing queue/reconciliation mechanism unchanged (FR-012).

**Scale/Scope**: One new `Location` boolean column, ~2 new backend endpoints (issue code,
verify/consume code), one new director-web settings section, one new tablet scan screen, one
new parent-mobile code-display screen. No new persisted entity for the code itself.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|---|---|
| I. Multi-Tenant Isolation | Pass. Code issuance/verification endpoints run through the existing `TenantMiddleware`/`ITenantDbContext` path like every other attendance/location endpoint — no new cross-tenant surface. |
| II. Regulatory Compliance by Design | Pass. FR-008/FR-014 require the BKR ratio and event/incident attribution to be computed identically regardless of check-in origin — enforced by reusing `CheckInCommand`/`CheckOutCommand` rather than duplicating attendance-toggle logic, so the same Application-layer ratio computation from feature 010 applies unconditionally. |
| III. CQRS via MediatR & Thin Endpoints | Pass. Code issuance and verification are new MediatR commands; endpoints stay thin (map HTTP ↔ command, resolve device/parent claims). |
| IV. Internationalization First | Pass. FR-017 requires all new strings in NL/FR/EN via each platform's existing i18n mechanism — tracked explicitly in tasks.md. |
| V. Test with Real Infrastructure | Pass. Backend tests run against TestContainers PostgreSQL per existing convention; no InMemory provider introduced. |
| VI. Secure Configuration & Storage | Pass. New `Location.QrCheckInEnabled` column ships as an EF Core migration with a manually-run SQL script (no auto-apply in production), per this repo's convention. No secrets involved; code signing key (research.md R1) is server-side configuration, not client-exposed. |
| VII. Monolith-First Simplicity | Pass. No new project/service — code issuance/verification live in `ChildCare.Application`/`ChildCare.Api` alongside existing Attendance code. |

No violations. Complexity Tracking section is empty.

## Project Structure

### Documentation (this feature)

```text
specs/021-qr-checkin/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── qr-checkin-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/Entities/Location.cs                       # + QrCheckInEnabled column
├── ChildCare.Application/
│   ├── Locations/
│   │   ├── UpdateLocationQrCheckInSettingCommand.cs             # new
│   │   └── UpdateLocationQrCheckInSettingCommandHandler.cs      # new
│   └── Attendance/
│       ├── IssueCheckInCodeCommand.cs                           # new
│       ├── IssueCheckInCodeCommandHandler.cs                    # new
│       ├── VerifyCheckInCodeCommand.cs                          # new — delegates to existing CheckInCommand/CheckOutCommand
│       ├── VerifyCheckInCodeCommandHandler.cs                   # new
│       └── ICheckInCodeService.cs / CheckInCodeService.cs       # new — sign/verify/cooldown (research.md R1)
├── ChildCare.Contracts/
│   ├── Requests/LocationRequests.cs                             # + QR setting request
│   ├── Requests/AttendanceRequests.cs                           # + code issue/verify requests
│   └── Responses/AttendanceResponses.cs                         # + code issue/verify responses
├── ChildCare.Api/Endpoints/
│   ├── LocationEndpoints.cs                                     # + PUT qr-checkin-setting
│   └── AttendanceEndpoints.cs                                   # + POST code/issue (ParentOnly), POST code/verify (DeviceAuthenticated)
└── ChildCare.Infrastructure/Persistence/Migrations/Tenant/
    └── <timestamp>_AddLocationQrCheckInEnabled.cs                # new

web/
├── app/(dashboard)/locations/[id]/settings/...                  # + QR check-in toggle section
└── i18n/locales/{en,nl,fr}.json                                 # + new keys

mobile/                                                           # caregiver tablet
├── services/attendance.ts                                       # + scanCheckIn (offline-queue aware, mirrors checkIn/checkOut)
├── app/.../scan.tsx (or equivalent screen)                       # new scan-mode screen
└── i18n/locales/{en,nl,fr}.json                                  # + new keys

parent-mobile/
├── services/attendance.ts                                        # new — issue/refresh code
├── app/.../qr-checkin.tsx (or equivalent screen)                  # new code-display screen
└── i18n/locales/{en,nl,fr}.json                                   # + new keys
```

**Structure Decision**: Follows the existing monolith-first layout (Constitution VII) —
extends `ChildCare.Application`'s `Attendance` and `Locations` folders rather than a new
project, and adds one screen per existing client app rather than a new app.

## Complexity Tracking

*No Constitution Check violations — table intentionally empty.*
