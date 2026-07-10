# Implementation Plan: Parent Communication

**Branch**: `013-parent-communication` | **Date**: 2026-07-10 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/013-parent-communication/spec.md`

## Summary

Build director-invited parent accounts, thread-based two-way messaging (shared per-child family threads), one-to-many director announcements, a generic in-app notification centre, push notifications, and a parent-facing daily summary aggregated from feature 009's `child_events` — all behind a new, greenfield parent-facing surface: the first real `ParentOnly` API endpoints, and the first real parent mobile app. Approach: maximize reuse of existing infrastructure (`IExpoPushSender`, `GetDailySummaryQuery`, the `StaffInvitation`/`AcceptStaffInvitationCommand` pattern) rather than parallel-building any of it a second time; the genuinely new work is the account-linking model (`Contact.TenantUserId`), the messaging/announcement/notification tables, and the new `parent-mobile/` Expo project plus new `web/app/(app)/messages` and `/announcements` screens.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript (web, Next.js 15; parent-mobile, Expo/React Native)

**Primary Dependencies**: MediatR, FluentValidation, EF Core 9 (backend); existing `IExpoPushSender`/Expo Push Notification Service; openapi-typescript + openapi-fetch (client generation, both web and mobile); `expo-localization` + `react-i18next` (parent-mobile i18n, mirrors `mobile/`); `next-intl` (web i18n, existing).

**Storage**: PostgreSQL 16, tenant schema (schema-per-tenant, existing `TenantDbContext`). New tables: `parent_invitations`, `message_threads`, `message_thread_participants`, `messages`, `announcements`, `announcement_recipients`, `notifications`. Modified: `contacts` (+`tenant_user_id`).

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, constitution Principle V); Vitest + `@testing-library/react` (web, established by 007a); Jest + `@testing-library/react-native` (parent-mobile, mirrors `mobile/`'s existing setup from 008/008a).

**Target Platform**: Cloud Run (backend); Next.js web (director); new Expo project, portrait/phone (parent) — separate from the existing landscape-locked caregiver `mobile/` project (research.md R9).

**Project Type**: Web application + two mobile apps (existing `mobile/` untouched; new `parent-mobile/` added) — this feature is the first to touch four surfaces at once (backend, web, and a brand-new mobile project).

**Performance Goals**: No new domain-specific throughput target; daily-summary aggregation stays query-time (no materialized view), consistent with feature 009's existing precedent, since typical event-per-child-per-day volume is small (tens, not thousands).

**Constraints**: Parent app has no offline read/write infrastructure in v1 (spec.md Assumptions) — online-only. Tenant isolation and `visible_to_parent` filtering must hold structurally across every new parent-facing endpoint (constitution Principle I; spec.md FR-002/017/018).

**Scale/Scope**: New backend: 7 tables, ~14 endpoints across 3 auth policies (`DirectorOnly`, `StaffOrDirector`, `ParentOnly`). New web: 2 route groups (`messages`, `announcements`). New mobile: 1 full Expo project (auth, home/summary, threads, notifications, push-token registration) — comparable in scope to feature 008's caregiver scaffold, but portrait/phone instead of landscape/tablet.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
| --- | --- | --- |
| I. Multi-Tenant Isolation (NON-NEGOTIABLE) | **Pass** | Every new table lives in the tenant schema via `TenantDbContext`; every new endpoint runs behind `TenantMiddleware` (no exemption needed — this is not a provisioning-only feature). `ParentOnly` endpoints additionally scope by the caller's linked `Contact` → `ChildContact`, never trusting a client-supplied child/thread id without an ownership check (FR-006, FR-017). |
| II. Regulatory Compliance by Design (NON-NEGOTIABLE) | **N/A** | This feature touches no BKR ratio, split-location overlap, or closure-calendar logic. |
| III. CQRS via MediatR & Thin Endpoints | **Pass** | Every write (invite, accept, send message, send announcement, mark-read, register push token) is a MediatR command with a FluentValidation validator; `*Endpoints.cs` files map HTTP↔MediatR only, per every prior feature's pattern. |
| IV. Internationalization First (NON-NEGOTIABLE) | **Pass** | All new UI strings (parent-mobile, web `/messages` and `/announcements`) use i18n keys from the start (NL/FR/EN); notification titles/bodies use the existing `TitleKey`/`BodyKey`/`ArgumentsJson` pattern (`ParentClosureMessage`'s precedent) rendered in the recipient's locale, not hardcoded strings (FR-016). |
| V. Test with Real Infrastructure (NON-NEGOTIABLE) | **Pass** | Backend integration tests run against TestContainers Postgres, covering happy path plus the key negative/security flows named in spec.md (SC-003 visible_to_parent leakage, SC-007 cross-family access, invitation replay). |
| VI. Secure Configuration & Storage | **Pass** | No new secrets; push tokens and invitation tokens follow existing hashing/storage conventions (`InvitationTokenCodec`, `Contact.PushToken`). No new file storage. |
| VII. Monolith-First Simplicity | **Pass** | No new backend project — all new code lives in the existing five `ChildCare.*` projects. Two new client projects (parent-mobile) are additive surfaces the constitution already anticipates ("three clients... from five projects" — the five backend projects are unchanged; client app count was never capped at three literal apps, and `web`/`mobile`/`parent-mobile` matches "web admin, caregiver app, parent app" exactly). |

**Result**: No violations. No Complexity Tracking entries needed.

## Project Structure

### Documentation (this feature)

```text
specs/013-parent-communication/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/api.md      # Phase 1 output
└── tasks.md              # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/Entities/
│   ├── Contact.cs                       # modified: + TenantUserId
│   ├── ParentInvitation.cs              # new
│   ├── MessageThread.cs                 # new
│   ├── MessageThreadParticipant.cs      # new
│   ├── Message.cs                       # new
│   ├── Announcement.cs                  # new
│   ├── AnnouncementRecipient.cs         # new
│   └── Notification.cs                  # new
├── ChildCare.Application/
│   ├── ParentInvitations/               # new: Create/AcceptParentInvitationCommand(+Handler+Validator)
│   ├── Messaging/                       # new: CreateThread/SendMessage/ListThreads/GetThread commands+queries
│   ├── Announcements/                   # new: SendAnnouncement/ListAnnouncements
│   ├── Notifications/                   # new: ListNotifications/MarkNotificationRead
│   ├── Parent/                          # new: GetParentChildrenQuery, GetParentDailySummaryQuery (wraps 009's query), RegisterPushTokenCommand
│   └── ChildEvents/TemperatureAlertService.cs   # modified: + Notification row on send (research.md R4)
├── ChildCare.Api/Endpoints/
│   ├── ParentInvitationEndpoints.cs     # new
│   ├── ParentEndpoints.cs               # new (daily-summary, children, push-token)
│   ├── MessageThreadEndpoints.cs        # new (parent + director/staff routes)
│   ├── AnnouncementEndpoints.cs         # new
│   └── NotificationEndpoints.cs         # new
└── ChildCare.Api.Tests/                 # new test files per area above

web/app/(app)/
├── messages/             # new: thread list + detail/reply
└── announcements/        # new: compose + sent history

parent-mobile/             # new Expo project (portrait), mirrors mobile/'s 008 scaffold shape:
├── app/(auth)/            # invitation-accept + login
├── app/(app)/             # home (daily summary), threads, notifications, settings (push-token registration)
├── services/generated/    # openapi-fetch client (own copy, mirrors mobile/services/generated)
└── theme/colors.js        # own copy of the color-token file (design-decisions.md's existing per-platform-copy precedent)
```

**Structure Decision**: Web application (Next.js) + backend (ASP.NET Core, unchanged project count) + two independent Expo mobile projects (existing `mobile/` untouched, new `parent-mobile/` added). This is "Option 2/3 combined" relative to the template's generic options — matches the monorepo's existing established shape (`backend/`, `web/`, `mobile/` as three top-level siblings), extended with a fourth top-level sibling for the parent app rather than nesting it inside `mobile/` (research.md R9).

## Complexity Tracking

No Constitution Check violations — this section is not needed.
