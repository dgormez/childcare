# Implementation Plan: Group Activities

**Branch**: `009b-group-activities` | **Date**: 2026-07-10 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/009b-group-activities/spec.md`

## Summary

Let caregivers record a shared group-level moment (activity type, title, description, up to 10
photos) once per group instead of duplicating it per child. Parents see it in their existing
daily-report feed (consent-gated photos) and a new monthly gallery; directors see it in a new
group-timeline view and can delete it. Backend: two new tenant-schema tables, a new
device-authenticated write surface mirroring `ChildEventEndpoints.cs`, a new group-timeline
aggregation query, and an extension to the existing parent daily-summary query. Mobile: a new
creation flow on the group home screen plus offline photo upload (genuinely new client
infrastructure — no precedent exists in this codebase, per research.md R7). Web: a new director
group-timeline screen (first of its kind — no group/child-event UI exists in `web/` today).

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript, Expo/React Native (mobile); TypeScript,
Next.js 15 App Router (web).

**Primary Dependencies**: MediatR + FluentValidation (backend CQRS, constitution Principle III);
`SixLabors.ImageSharp` (new — server-side photo resize/thumbnail, research.md R2/R3);
`Google.Cloud.Storage.V1` (existing, direct GCS writes for resized images); openapi-fetch
(existing, both clients); `expo-image-picker` (existing? verify at implementation time — used for
camera/gallery photo attach, same as any Expo app needs for this).

**Storage**: PostgreSQL 16, tenant schema — two new tables (`group_activities`,
`group_activity_photos`, see data-model.md). GCS for photo objects (new bucket path prefix
`group-activities/`, same bucket as `Storage:ProfilePhotosBucketName` unless a dedicated bucket is
warranted — default to reusing the existing bucket with the new path prefix, consistent with how
`children`/`staff` already share one bucket by category).

**Testing**: xUnit + TestContainers PostgreSQL (backend, constitution Principle V); Jest +
React Native Testing Library (mobile); Vitest + Testing Library (web) — all existing project
tooling, no new test infrastructure (research.md R9).

**Target Platform**: Cloud Run (backend); iOS/Android via Expo (caregiver tablet — landscape);
Web (director, desktop-first ≥1280px).

**Project Type**: Mobile + API + Web (three-client monorepo, existing structure).

**Performance Goals**: Activity creation interaction under 30s (SC-001); photo resize/thumbnail
generation fast enough not to block the caregiver's next action (target: perceived as instant on
a synchronous 1-2 photo upload, per platform-rules.md's "immediate feedback" principle for the
caregiver tablet).

**Constraints**: Offline-capable creation (FR-012); 10 photos max per activity, 10MB max per raw
photo before resize (FR-003); signed GCS URLs only, 15-minute validity (constitution Principle
VI); 48pt minimum touch targets on the caregiver tablet (platform-rules.md).

**Scale/Scope**: Single-tenant-schema-scoped, same order of magnitude as `child_events` (feature
009) — no new scale concerns beyond what that feature already handles.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
| --- | --- | --- |
| I. Multi-Tenant Isolation | **Pass** | Both new tables live in `TenantDbContext` (tenant schema), same as every entity since feature 002. No cross-tenant read path introduced. |
| II. Regulatory Compliance by Design | **N/A** | This feature touches no BKR ratio, contract-overlap, or closure-notification logic — it reads (never writes) `Contract.Consent`, an existing field, for display gating only. |
| III. CQRS via MediatR & Thin Endpoints | **Pass** | All writes (`CreateGroupActivityCommand`, `UploadGroupActivityPhotoCommand`, `DeleteGroupActivityCommand`) go through MediatR; all non-trivial reads (`GetGroupTimelineQuery`, extended `GetDailySummaryQuery`, `GetParentGroupActivityGalleryQuery`) go through MediatR queries; `GroupActivityEndpoints.cs` maps HTTP↔MediatR only, mirroring `ChildEventEndpoints.cs`. |
| IV. Internationalization First | **Pass** | All new UI copy (activity type labels, form fields, empty states, consent messaging, upload indicators) uses i18n keys across NL/FR/EN in both `mobile/i18n/locales/` and `web/i18n/locales/`, per research.md's mobile/web i18n-structure findings. No hardcoded strings. |
| V. Test with Real Infrastructure | **Pass** | Backend integration tests use `OrganisationOnboardingWebAppFactory`'s TestContainers Postgres fixture, same as every prior feature — no InMemory provider introduced (research.md R9). |
| VI. Secure Configuration & Storage | **Pass** | Photos served only via signed GCS URLs (15-minute validity, matching the existing `IProfilePhotoStorage` pattern); no secrets hardcoded; deleted activities remove their GCS objects explicitly (data-model.md). One deliberate, documented deviation: photo bytes pass through the API for resize (research.md R3) rather than a direct-to-GCS signed PUT — still never publicly accessible, still signed-URL-only for reads; the deviation is about where resize compute happens, not about storage security. |
| VII. Monolith-First Simplicity | **Pass** | No new service/deployable introduced. Server-side resize happens in-process in the existing `ChildCare.Api`/`ChildCare.Infrastructure` (research.md R3's explicit rationale for rejecting a separate resize worker). One new NuGet dependency (`SixLabors.ImageSharp`) for image processing — no existing project dependency covers this, and it's the standard, actively maintained choice for .NET (see research.md R2's licensing note, flagged the same way `BACKLOG.md` flags MediatR's licensing). |

No violations requiring Complexity Tracking justification.

## Project Structure

### Documentation (this feature)

```text
specs/009b-group-activities/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── group-activities-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/Entities/
│   ├── GroupActivity.cs                  # new
│   └── GroupActivityPhoto.cs             # new
├── ChildCare.Domain/Enums/
│   └── GroupActivityType.cs              # new
├── ChildCare.Application/
│   ├── Common/IGroupActivityPhotoStorage.cs         # new port (research.md R2)
│   ├── GroupActivities/
│   │   ├── CreateGroupActivityCommand.cs            # new
│   │   ├── UploadGroupActivityPhotoCommand.cs        # new
│   │   ├── DeleteGroupActivityCommand.cs              # new
│   │   ├── GetGroupTimelineQuery.cs                   # new (research.md R4)
│   │   ├── GetParentGroupActivityGalleryQuery.cs      # new
│   │   └── GroupActivityMapper.cs                    # new
│   └── ChildEvents/
│       └── GetDailySummaryQuery.cs                    # extended (research.md R5)
├── ChildCare.Infrastructure/
│   ├── Storage/GcsGroupActivityPhotoStorage.cs        # new (reuses UrlSigner pattern)
│   └── Persistence/TenantDbContext.cs                 # extended: two new DbSets + migration
├── ChildCare.Api/Endpoints/
│   └── GroupActivityEndpoints.cs                      # new (mirrors ChildEventEndpoints.cs)
├── ChildCare.Contracts/
│   ├── Requests/ (CreateGroupActivityRequest, etc.)   # new
│   └── Responses/ (GroupActivityResponse, etc.)       # new
└── ChildCare.Api.Tests/GroupActivities/
    ├── GroupActivityTestSupport.cs                    # new
    ├── CreateGroupActivityTests.cs                    # new
    ├── GroupActivityPhotoUploadTests.cs                # new
    ├── GroupActivityConsentFilteringTests.cs           # new
    ├── GroupTimelineOrderingTests.cs                   # new
    └── DeleteGroupActivityTests.cs                     # new

mobile/
├── services/groupActivities.ts           # new (registers 'group_activity' sync handler)
├── services/photoUploadQueue.ts          # new (research.md R7)
├── components/
│   ├── AddGroupActivitySheet.tsx         # new (bottom-sheet form, mirrors QuickActionSheet)
│   └── GroupTimeline.tsx                 # new (merges child events + activities for group view)
├── app/(app)/index.tsx                    # extended: "Activiteit toevoegen" affordance
├── i18n/locales/{nl,en,fr}.json           # extended: errors.group_activities.*, groupActivities.*
└── __tests__/
    ├── services/groupActivities.test.ts   # new
    └── components/AddGroupActivitySheet.test.tsx  # new

web/
├── app/(app)/groups/page.tsx              # new — director group timeline (research.md item 5)
├── components/GroupTimeline.tsx           # new
├── lib/apiClient.ts                        # unchanged (existing openapi-fetch client)
├── i18n/locales/{nl,en,fr}.json            # extended: groups.* namespace
└── __tests__/groups.test.tsx               # new

parent-mobile/                              # separate Expo project from mobile/ (caregiver app)
├── components/DailySummaryCard.tsx          # extended: new "Activiteiten" section from the
│                                             # daily-summary response's new `activities` array
├── app/(app)/gallery.tsx                    # new — "Galerij" tab
├── app/(app)/_layout.tsx                    # extended: register the gallery Tabs.Screen
├── services/groupActivityGallery.ts         # new — calls the gallery endpoint
├── i18n/locales/{nl,en,fr}.json             # extended: gallery.*, dailySummary.activities.*
└── __tests__/gallery.test.tsx               # new (flat, matches home.test.tsx convention)
```

**Correction found during planning** (same class of premise-check every prior feature in this
loop has done — see BACKLOG.md's 009/012/012a shipped-notes): the original plan draft assumed
parent screens live under `mobile/app/(parent)/...`. The parent app is actually a wholly separate
Expo project, `parent-mobile/` (own `package.json`, own `theme/colors.js`, own `i18n/`, own
`services/apiClient.ts`, `Tabs`-based nav) — `mobile/` is caregiver-tablet only. Corrected above.
Also: `parent-mobile/`'s existing daily report (`DailySummaryCard`) is a card of aggregate counts
plus an unordered text list, not a chronological per-event timeline (unlike the caregiver app's
`EventTimeline`) — group activities are added as their own ordered section within that card, not
merged into a mixed feed that doesn't exist (spec.md's Assumptions section documents this).

**Structure Decision**: Follows the existing four-client monorepo layout exactly (`backend/`,
`mobile/` (caregiver), `parent-mobile/` (parent), `web/` (director)) — no new top-level project.
Backend changes are additive (`GroupActivities/` namespace mirrors `ChildEvents/`); the only
extended existing file on the read side is `GetDailySummaryQuery.cs` (research.md R5's deliberate
choice to extend rather than duplicate). `mobile/` introduces one genuinely new subsystem
(`photoUploadQueue.ts`, research.md R7) since no prior feature uploads photos at all.
`parent-mobile/` extends its existing `DailySummaryCard` and adds a new `gallery` tab to its
existing `Tabs` nav shell. Web introduces its first `groups/` route and its first consumer of
`ChildEvent`/activity types (previously unused generated types, per research finding 5/8) —
confirmed via research that no group-timeline or child-event UI exists in `web/` today.

## Complexity Tracking

*No Constitution Check violations — table not needed.*
