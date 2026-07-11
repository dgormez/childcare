---

description: "Task list for feature 009b-group-activities"
---

# Tasks: Group Activities

**Input**: Design documents from `/specs/009b-group-activities/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/group-activities-api.md, quickstart.md

**Tests**: Included — this codebase's standing convention (constitution Principle V, every prior
feature's tasks.md) is happy-path + key negative/regulatory flows, not full-path coverage.

**Organization**: Grouped by user story (spec.md), in priority order (US1/US2 = P1, US3/US4 = P2).

## Path Conventions

Three-client monorepo: `backend/`, `mobile/`, `web/` at repo root (see plan.md's Project Structure).

---

## Phase 1: Setup

- [x] T001 Add `SixLabors.ImageSharp` package reference to `backend/ChildCare.Infrastructure/ChildCare.Infrastructure.csproj` (research.md R2)
- [x] T002 [P] Create `GroupActivityType` enum in `backend/ChildCare.Domain/Enums/GroupActivityType.cs` (data-model.md)
- [x] T003 [P] Add `errors.group_activities.*` and `groupActivities.*` namespace stubs to `mobile/i18n/locales/{nl,en,fr}.json` (empty/placeholder keys, filled in per-task below)
- [x] T004 [P] Add `groups.*` namespace stub to `web/i18n/locales/{nl,en,fr}.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Data model, storage port, and migration that every user story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T005 Create `GroupActivity` entity in `backend/ChildCare.Domain/Entities/GroupActivity.cs` (data-model.md)
- [x] T006 Create `GroupActivityPhoto` entity in `backend/ChildCare.Domain/Entities/GroupActivityPhoto.cs` (data-model.md)
- [x] T007 Register both entities + owned `RecordedBy` JSONB list on `TenantDbContext` in `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`, add FK/cascade config (`GroupActivityPhoto.GroupActivityId` → `ON DELETE CASCADE`)
- [x] T008 Generate EF Core migration for `group_activities` + `group_activity_photos` tables (per-tenant migration convention, constitution Principle VI — SQL script generated, not auto-applied)
- [x] T009 [P] Extend `TenantMigrationRolloutTests`' schema-revert helper for the two new tables' FKs (to `groups` and `locations`) — every migration-adding feature since 003 needs this fix (per BACKLOG.md 012a's shipped-note)
- [x] T010 [P] Create `IGroupActivityPhotoStorage` port in `backend/ChildCare.Application/Common/IGroupActivityPhotoStorage.cs` (research.md R2/R3): resize+thumbnail+upload method, signed download-URL method
- [x] T011 [P] Implement `GcsGroupActivityPhotoStorage` in `backend/ChildCare.Infrastructure/Storage/GcsGroupActivityPhotoStorage.cs` — `SixLabors.ImageSharp` resize (max 1920px long edge) + 400px thumbnail, direct `UploadObjectAsync` writes, signed download URLs (15 min, mirrors `GcsProfilePhotoStorage`)
- [x] T012 Register `IGroupActivityPhotoStorage` → `GcsGroupActivityPhotoStorage` in DI (`backend/ChildCare.Api/Program.cs`)
- [x] T013 [P] Create `GroupActivityResponse`/`GroupActivityPhotoResponse`/`GroupTimelineResponse` in `backend/ChildCare.Contracts/Responses/`
- [x] T014 [P] Create `CreateGroupActivityRequest`/`UpdateGroupActivityPhotoRequest`-equivalent request contracts in `backend/ChildCare.Contracts/Requests/`
- [x] T015 [P] Create `GroupActivityTestSupport.cs` in `backend/ChildCare.Api.Tests/GroupActivities/` (helper: seed group/location/room-shift/device-token, mirrors `ChildEventTestSupport.cs`)

**Checkpoint**: Foundation ready — user story implementation can now begin.

---

## Phase 3: User Story 1 - Caregiver records a group activity (Priority: P1) 🎯 MVP

**Goal**: Caregiver creates a group activity (type/title/description/photos) from the tablet, online or offline; it appears in the group timeline.

**Independent Test**: `POST /api/group-activities` + `POST /api/group-activities/{id}/photos` (device token) → `GET /api/group-activities/timeline` shows it (quickstart.md Scenario 1); repeat offline via the mobile app.

### Tests for User Story 1

- [x] T016 [P] [US1] Integration test: create activity, `recorded_by` resolves from checked-in room-shift caregivers (0/1/2+ cases) in `backend/ChildCare.Api.Tests/GroupActivities/CreateGroupActivityTests.cs`
- [x] T017 [P] [US1] Integration test: idempotent create by client-generated id (retry returns existing record) in `backend/ChildCare.Api.Tests/GroupActivities/CreateGroupActivityTests.cs`
- [x] T018 [P] [US1] Integration test: title/description length validation, invalid `activityType` → `422` in `backend/ChildCare.Api.Tests/GroupActivities/CreateGroupActivityTests.cs`
- [x] T019 [P] [US1] Integration test: photo upload resizes to ≤1920px + generates 400px thumbnail, rejects >10MB (`413`) and an 11th photo (`409`) in `backend/ChildCare.Api.Tests/GroupActivities/GroupActivityPhotoUploadTests.cs`
- [x] T020 [P] [US1] Integration test: group timeline merges `ChildEvent` + `GroupActivity` rows chronologically for a group/date in `backend/ChildCare.Api.Tests/GroupActivities/GroupTimelineOrderingTests.cs`

### Backend Implementation for User Story 1

- [x] T021 [US1] Implement `CreateGroupActivityCommand` + validator + handler (reuses `IShiftAttributionService`, research.md R1) in `backend/ChildCare.Application/GroupActivities/CreateGroupActivityCommand.cs`
- [x] T022 [US1] Implement `UploadGroupActivityPhotoCommand` + validator + handler (10-photo/10MB limits, calls `IGroupActivityPhotoStorage`) in `backend/ChildCare.Application/GroupActivities/UploadGroupActivityPhotoCommand.cs`
- [x] T023 [US1] Implement `GetGroupTimelineQuery` (merges `ChildEvent` + `GroupActivity`, research.md R4) in `backend/ChildCare.Application/GroupActivities/GetGroupTimelineQuery.cs`
- [x] T024 [P] [US1] Create `GroupActivityMapper` in `backend/ChildCare.Application/GroupActivities/GroupActivityMapper.cs`
- [x] T025 [US1] Implement `GroupActivityEndpoints.cs` (device-authenticated group, mirrors `ChildEventEndpoints.cs` — `DeviceAuthenticated` + `DeviceTokenRotationFilter`): `POST /api/group-activities`, `POST /api/group-activities/{id}/photos`, `GET /api/group-activities/timeline` in `backend/ChildCare.Api/Endpoints/GroupActivityEndpoints.cs`
- [x] T026 [US1] Register `MapGroupActivityEndpoints()` in `backend/ChildCare.Api/Program.cs`
- [x] T027 [P] [US1] Add `errors.group_activities.{invalid_activity_type,not_found,photo_limit_reached,photo_too_large}` keys to `mobile/i18n/locales/{nl,en,fr}.json`

### Mobile Implementation for User Story 1

- [x] T028 [P] [US1] Regenerate `mobile/services/generated/api-types.ts` from the updated OpenAPI spec (per 007a's established "regenerate + commit the diff" convention) after backend endpoints are live
- [x] T029 [US1] Create `mobile/services/groupActivities.ts` — `createActivity()`, registers `'group_activity'` sync handler via `registerSyncHandler` (research.md R7)
- [x] T030 [US1] Create `mobile/services/photoUploadQueue.ts` — local `photo_upload_queue` table (SQLite), enqueue-by-local-URI, uploader routine triggered on reconnect/foreground, waits for parent activity's server id before uploading (research.md R7)
- [x] T031 [US1] Create `mobile/components/AddGroupActivitySheet.tsx` — bottom-sheet form (type picker, pre-filled editable title, description, photo attach up to 10via camera/gallery), mirrors `QuickActionSheet.tsx`'s Modal pattern, includes the "Foto's mogen enkel aanwezige kinderen tonen" reminder
- [x] T032 [US1] Create `mobile/components/GroupTimeline.tsx` — renders merged `ChildEvent`/`GroupActivity` entries with pending-sync/uploading indicators, reusing `EventTimeline.tsx`'s status-badge conventions
- [x] T033 [US1] Wire "Activiteit toevoegen" affordance + `AddGroupActivitySheet` + `GroupTimeline` into `mobile/app/(app)/index.tsx` group home screen
- [x] T034 [P] [US1] Add `groupActivities.*` UI-copy keys (type labels, form fields, upload indicator "Foto's worden geüpload…") to `mobile/i18n/locales/{nl,en,fr}.json`
- [x] T035 [P] [US1] Unit test `mobile/__tests__/services/groupActivities.test.ts` (create + sync handler registration)
- [x] T036 [P] [US1] Component test `mobile/__tests__/components/AddGroupActivitySheet.test.tsx` (10-photo cap, offline queue path)

**Checkpoint**: User Story 1 fully functional and independently testable (quickstart.md Scenario 1 + 5).

---

## Phase 4: User Story 2 - Parent sees group activities in the daily feed (Priority: P1)

**Goal**: Activities appear in the parent's existing daily report, photos gated by `photos_internal` consent.

**Independent Test**: `GET /api/parent/children/{childId}/daily-summary` includes the activity with consent-gated photos (quickstart.md Scenario 2).

### Tests for User Story 2

- [x] T037 [P] [US2] Integration test: daily summary includes today's group activities for the child's group as of `occurred_at` in `backend/ChildCare.Api.Tests/GroupActivities/GroupActivityConsentFilteringTests.cs`
- [x] T038 [P] [US2] Integration test: `photos_internal = true` → photos populated; `false`/no active contract → `photos: []`, title/description still present in `backend/ChildCare.Api.Tests/GroupActivities/GroupActivityConsentFilteringTests.cs`
- [x] T039 [P] [US2] Integration test: child's group reassignment mid-day — daily summary only shows activities for the group the child belonged to at `occurred_at` in `backend/ChildCare.Api.Tests/GroupActivities/GroupActivityConsentFilteringTests.cs`

### Backend Implementation for User Story 2

- [x] T040 [US2] Extend `GetDailySummaryQuery` in `backend/ChildCare.Application/ChildEvents/GetDailySummaryQuery.cs` — resolve child's `ChildGroupAssignment` as of the requested date, append that group's `GroupActivity` rows with consent-filtered photos (research.md R5/R6). This query is already per-child, so no dedup logic is needed here (spec.md Edge Cases' twins case only applies where activities are aggregated across multiple children of the same parent — see T048).
- [x] T041 [US2] Add active-contract + `PhotosInternal` lookup (mirrors `ClosureParentRecipientResolver`'s predicate shape, research.md R6) inline in the query/handler — no new shared abstraction (per research.md's explicit rejection of premature extraction)
- [x] T042 [P] [US2] Extend `DailySummaryResponse` contract with `activities: GroupActivitySummaryItem[]` in `backend/ChildCare.Contracts/Responses/`

### parent-mobile Implementation for User Story 2

**Note**: `parent-mobile/` is a separate Expo project from `mobile/` (caregiver app), with its own `theme/`, `i18n/`, `services/apiClient.ts`, and `__tests__/` — corrected during planning (plan.md's Project Structure). Its existing daily report (`DailySummaryCard`) is a card of aggregate counts + an unordered text list, not a chronological timeline — activities render as their own new section, not merged into a nonexistent mixed feed (spec.md Assumptions).

- [x] T042a [US2] Regenerate `parent-mobile/services/generated/api-types.ts` after the `DailySummaryResponse` contract extension (T042) is live
- [x] T043 [US2] Add a new "Activiteiten" section to `parent-mobile/components/DailySummaryCard.tsx` rendering the `activities` array (title, description, photos), each item chronologically ordered — warm NL/FR/EN copy per platform-rules.md's parent tone
- [x] T044 [P] [US2] Add `dailySummary.activities.*` parent-facing copy keys (e.g. activity type display names in natural language, section heading) to `parent-mobile/i18n/locales/{nl,en,fr}.json`
- [x] T045 [P] [US2] Extend `parent-mobile/__tests__/home.test.tsx` to cover the new activities section, including consent-gated photo rendering (full consent / no consent cases)

**Checkpoint**: User Stories 1 AND 2 both work independently.

---

## Phase 5: User Story 3 - Parent browses the monthly activity gallery (Priority: P2)

**Goal**: A "Galerij" tab shows all consented group-activity photos for the current month.

**Independent Test**: `GET /api/parent/group-activities/gallery` returns the month's consented photos, or `hasConsent: false` with an empty list (quickstart.md Scenario 3).

### Tests for User Story 3

- [x] T046 [P] [US3] Integration test: gallery aggregates photos across all groups the parent's children belong to, current month, most-recent-first, and de-duplicates an activity shared by two children in the same group (twins case) in `backend/ChildCare.Api.Tests/GroupActivities/GroupActivityConsentFilteringTests.cs`
- [x] T047 [P] [US3] Integration test: no consent → `{ items: [], hasConsent: false }`; text-only activities excluded from gallery results in `backend/ChildCare.Api.Tests/GroupActivities/GroupActivityConsentFilteringTests.cs`

### Backend Implementation for User Story 3

- [x] T048 [US3] Implement `GetParentGroupActivityGalleryQuery` in `backend/ChildCare.Application/GroupActivities/GetParentGroupActivityGalleryQuery.cs` (reuses R6's consent-filter shape); dedup by `GroupActivity.Id` before returning so an activity shared by two of the parent's children in the same group (e.g. twins) appears once, not once per child (spec.md Edge Cases)
- [x] T049 [US3] Add `GET /api/parent/group-activities/gallery` to `ParentEndpoints.cs` (`ParentOnly` policy)
- [x] T050 [P] [US3] Add `GalleryResponse`/`GalleryItemResponse` contracts in `backend/ChildCare.Contracts/Responses/`

### parent-mobile Implementation for User Story 3

**Note**: `parent-mobile/` is a separate Expo project from `mobile/` (caregiver app) — corrected during planning, see plan.md's Project Structure section.

- [x] T051a [P] [US3] Regenerate `parent-mobile/services/generated/api-types.ts` after backend endpoints are live (mirrors T028's convention for the other client)
- [x] T051 [US3] Create `parent-mobile/app/(app)/gallery.tsx` — "Galerij" tab, photo grid, explicit no-consent empty state (per spec.md User Story 3 Acceptance Scenario 2, not a blank grid); create `parent-mobile/services/groupActivityGallery.ts` for the API call
- [x] T051b [US3] Register the `gallery` tab in `parent-mobile/app/(app)/_layout.tsx` (`Tabs.Screen`, mirrors the existing four tabs' shape)
- [x] T052 [P] [US3] Add `gallery.*` copy keys (empty state, tab label) to `parent-mobile/i18n/locales/{nl,en,fr}.json`
- [x] T053 [P] [US3] Component test `parent-mobile/__tests__/gallery.test.tsx` (consent / no-consent / multi-child cases, flat file per this project's existing test convention)

**Checkpoint**: User Stories 1, 2, AND 3 all work independently.

---

## Phase 6: User Story 4 - Director moderates group activities (Priority: P2)

**Goal**: Director views a group timeline on web and deletes an activity; it disappears everywhere.

**Independent Test**: `DELETE /api/group-activities/{id}` (director JWT) → gone from caregiver timeline, parent feed, and gallery in one request (quickstart.md Scenario 4).

### Tests for User Story 4

- [x] T054 [P] [US4] Integration test: director delete removes activity + `GroupActivityPhoto` rows + GCS objects (full + thumbnail), no orphaned objects in `backend/ChildCare.Api.Tests/GroupActivities/DeleteGroupActivityTests.cs`
- [x] T055 [P] [US4] Integration test: deleted activity no longer appears in group timeline, daily summary, or gallery in `backend/ChildCare.Api.Tests/GroupActivities/DeleteGroupActivityTests.cs`
- [x] T056 [P] [US4] Integration test: director-timeline endpoint requires explicit `date` param (no implicit "today") and returns merged entries like the device-facing timeline in `backend/ChildCare.Api.Tests/GroupActivities/GroupTimelineOrderingTests.cs`

### Backend Implementation for User Story 4

- [x] T057 [US4] Implement `DeleteGroupActivityCommand` + handler (deletes GCS objects before DB rows) in `backend/ChildCare.Application/GroupActivities/DeleteGroupActivityCommand.cs`
- [x] T058 [US4] Add `DirectorOnly` group to `GroupActivityEndpoints.cs`: `DELETE /api/group-activities/{id}`, `GET /api/group-activities/director-timeline`
- [x] T059 [P] [US4] Add `groups.*` error-key mappings to `web/i18n/locales/{nl,en,fr}.json`

### Web Implementation for User Story 4

- [x] T060 [US4] Regenerate `web/lib/generated/api-types.ts` after backend endpoints are live (per 007a's established convention)
- [x] T061 [US4] Create `web/components/GroupTimeline.tsx` — merged event/activity list, per-row delete action (inline button, not a hidden menu, per 007a's "avoid hidden actions" precedent), full keyboard reachability + visible focus ring
- [x] T062 [US4] Create `web/app/(app)/groups/page.tsx` — group selector, date picker (defaults to today), renders `GroupTimeline`, follows `waiting-list/page.tsx`'s state/data-fetching shape (openapi-fetch `apiClient`)
- [x] T063 [P] [US4] Add `groups.*` UI-copy keys to `web/i18n/locales/{nl,en,fr}.json`
- [x] T064 [P] [US4] Test `web/__tests__/groups.test.tsx` (render timeline, delete confirmation flow, date picker)

**Checkpoint**: All four user stories independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [x] T065 [P] Run `quickstart.md` end-to-end against a local backend (all 5 scenarios)
- [x] T066 [P] Design-compliance pass across all three UI surfaces: spacing (4/8/12/16/24/32 only), no nested cards, icon+text pairing on `AddGroupActivitySheet`'s activity-type picker, 48pt touch targets (`mobile/`), keyboard reachability + focus ring on `web/app/(app)/groups/page.tsx`, motion under 250ms with no bounce on offline/uploading indicators, natural-language copy on `parent-mobile/`'s new activities section and Galerij tab (design-system.md, platform-rules.md)
- [x] T067 [P] Verify no hardcoded strings slipped into any new file across `backend/`, `mobile/`, `parent-mobile/`, `web/` (constitution Principle IV)
- [x] T068 Full backend/mobile/parent-mobile/web test suite run; fix any regressions

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (entities/migration/storage port needed by every story).
- **US1 (Phase 3, P1)**: Depends on Foundational only. This is the MVP — nothing is visible to parents/directors without it.
- **US2 (Phase 4, P1)**: Depends on Foundational + US1 existing (needs activities to actually exist to extend the daily summary against) — but is independently testable once US1's create endpoint exists, even before US1's mobile UI ships.
- **US3 (Phase 5, P2)**: Depends on Foundational + US2's consent-filter logic (R6, first implemented in US2) — reuses, does not duplicate, that filter shape.
- **US4 (Phase 6, P2)**: Depends on Foundational + US1 (needs activities to exist to delete). Independent of US2/US3.
- **Polish (Phase 7)**: Depends on all four stories.

### Parallel Opportunities

- All `[P]`-marked Setup and Foundational tasks run in parallel.
- Within US1: T016–T020 (tests) in parallel; T024/T027 in parallel with T021–T023; T034–T036 in parallel once T031–T033 land.
- US3 and US4 can be built in parallel by different sessions once US2's consent-filter code (T040/T041) exists, since neither touches the other's files.

---

## Implementation Strategy

### MVP First

1. Phase 1 (Setup) → Phase 2 (Foundational) → Phase 3 (US1).
2. **STOP and VALIDATE**: quickstart.md Scenario 1 + 5 (offline) work end-to-end on a real tablet build.
3. This alone proves the core capture loop; US2 is needed before it's genuinely useful (nobody sees it otherwise), so treat US1+US2 together as the realistic MVP for this feature specifically.

### Incremental Delivery

1. Setup + Foundational → Phase 3 (US1) → Phase 4 (US2): capture + parent visibility, the minimum viable version of "parents see group moments."
2. Phase 5 (US3): gallery enrichment.
3. Phase 6 (US4): director moderation safety valve.
4. Phase 7: polish, full quickstart validation.
