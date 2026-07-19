# Tasks: Photo Lifecycle & Governance

**Input**: Design documents from `/specs/031-photo-lifecycle-governance/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/photo-lifecycle-api.md, quickstart.md

**Tests**: Included — spec.md's Technical Requirements explicitly requests RBAC regression,
purge-cascade, and archiving-eligibility tests; Constitution V (NON-NEGOTIABLE) requires
TestContainers-backed integration tests, not InMemory.

**Organization**: Tasks are grouped by user story per spec.md's priorities: US1/US2 = P1,
US3/US4 = P2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 (purge), US2 (parent download), US3 (archive/tiering), US4 (RBAC parity)

---

## Phase 1: Setup

- [ ] T001 Confirm dev GCS bucket/credentials are configured locally per `Storage:ProfilePhotosBucketName` (README/appsettings check only — no code change) so storage-class assertions in later tests are runnable.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared building blocks that US1, US2, and US3 all depend on.

**⚠️ CRITICAL**: Complete this phase before starting US1, US2, or US3. US4 has no dependency on this phase and may be done at any point.

- [ ] T002 [P] Add `DeleteAsync(string objectPath, CancellationToken ct)` to `IProfilePhotoStorage` in `backend/ChildCare.Application/Common/IProfilePhotoStorage.cs`, implement in `backend/ChildCare.Infrastructure/Storage/GcsProfilePhotoStorage.cs` (best-effort catch/log, mirroring `IGroupActivityPhotoStorage.DeleteAsync`'s existing semantics).
- [ ] T003 [P] Add `DeleteAsync(string objectPath, CancellationToken ct)` to `IHealthAttachmentStorage` in `backend/ChildCare.Application/Common/IHealthAttachmentStorage.cs`, implement in `backend/ChildCare.Infrastructure/Storage/GcsHealthAttachmentStorage.cs` (same best-effort semantics).
- [ ] T004 [P] Add `CreateAttachmentDownloadUrlAsync(string objectPath, string downloadFileName, CancellationToken ct)` to `IProfilePhotoStorage`, implement in `GcsProfilePhotoStorage.cs` (V4 signed URL with `responseDisposition: attachment; filename="..."`, 15-minute TTL — same signer as existing `CreateDownloadUrlAsync`).
- [ ] T005 [P] Add `CreateAttachmentDownloadUrlAsync(...)` to `IGroupActivityPhotoStorage` (`backend/ChildCare.Application/Common/IGroupActivityPhotoStorage.cs`), implement in `backend/ChildCare.Infrastructure/Storage/GcsGroupActivityPhotoStorage.cs`.
- [ ] T006 [P] Add `CreateAttachmentDownloadUrlAsync(...)` to `IHealthAttachmentStorage`, implement in `GcsHealthAttachmentStorage.cs`.
- [ ] T007 [P] Add `SetStorageClassAsync(string objectPath, string storageClass, CancellationToken ct)` to all three storage interfaces (`IProfilePhotoStorage`, `IGroupActivityPhotoStorage`, `IHealthAttachmentStorage`) and their Gcs implementations, using `Google.Cloud.Storage.V1`'s in-place object-update to set `Object.StorageClass` (research.md R2) — no-op if the object is already on the target class.
- [ ] T008 Create `IGroupActivityChildDerivationService` in `backend/ChildCare.Application/Common/IGroupActivityChildDerivationService.cs` (`GetDepictedChildIdsAsync(Guid groupActivityId, CancellationToken ct)`), implement `GroupActivityChildDerivationService` in `backend/ChildCare.Infrastructure/GroupActivities/GroupActivityChildDerivationService.cs`, extracting the existing group-membership/date-matching logic verbatim from `backend/ChildCare.Application/GroupActivities/GetParentGroupActivityGalleryQuery.cs` (research.md R3 — behavior must not change).
- [ ] T009 Refactor `GetParentGroupActivityGalleryQuery.cs` to call `IGroupActivityChildDerivationService` instead of its inline derivation logic (depends on T008); confirm existing gallery tests still pass unchanged.
- [ ] T010 [P] Register the new storage-port methods' DI wiring and `IGroupActivityChildDerivationService` in the Infrastructure DI composition root (wherever `IProfilePhotoStorage` etc. are currently registered).

**Checkpoint**: Delete capability, attachment-download URLs, storage-class transitions, and the shared child-derivation service all exist and are unit-testable — US1/US2/US3 implementation can now begin.

---

## Phase 3: User Story 1 - Director purges a departed child's photos (Priority: P1) 🎯 MVP

**Goal**: A director can permanently delete a deactivated child's profile photo, health/vaccine attachments, and any group-activity photo where they're the sole depicted child, in one action, without touching photos still depicting other children.

**Independent Test**: Deactivate a child with a profile photo, a solely-depicted group photo, a shared group photo, and a health attachment; purge; verify exactly the solely-owned objects are gone and the shared photo survives untouched.

### Tests for User Story 1

- [ ] T011 [P] [US1] TestContainers integration test: purge on a deactivated child deletes profile photo + health/vaccine attachments + solely-depicted group photo, preserves a group photo shared with an active child, in `backend/ChildCare.Application.Tests/Children/PurgeChildPhotosCommandTests.cs`.
- [ ] T012 [P] [US1] Test: purge on an **active** (non-deactivated) child is rejected with `ChildStillActive` and deletes nothing, same test file.
- [ ] T013 [P] [US1] Test: a partial failure (mock one storage port's `DeleteAsync` to throw/log-fail) is surfaced via `FailedObjectPaths`, never reported as a clean success, same test file.

### Implementation for User Story 1

- [ ] T014 [US1] Create `PurgeChildPhotosCommand` + `PurgePhotosResult`/`PurgePhotosFailure` in `backend/ChildCare.Application/Children/PurgeChildPhotosCommand.cs` per data-model.md's shape (depends on T002, T003, T008).
- [ ] T015 [US1] Implement the handler: reject if `Child.DeactivatedAt is null`; else delete profile photo, every health/vaccine attachment, and every group-activity photo where `GetDepictedChildIdsAsync` resolves to exactly `[ChildId]`; aggregate deleted/failed paths; count preserved shared photos.
- [ ] T016 [US1] Add the structured audit log entry (`ILogger.LogInformation`/`LogWarning`) to the handler per research.md R4 — `{TenantId}`, `{ActorUserId}`, `{ActorRole}`, `{ChildId}`, `{DeletedObjectCount}`, `{FailedObjectCount}`.
- [ ] T017 [US1] Add `POST /api/children/{childId}/purge-photos` route (`DirectorOnly`) to `backend/ChildCare.Api/Endpoints/ChildrenEndpoints.cs` per contracts/photo-lifecycle-api.md (400 `errors.children.still_active`, 404 `errors.children.not_found`, 200 with `deletedObjectPaths`/`failedObjectPaths`/`preservedGroupPhotoCount`).
- [ ] T018 [P] [US1] Add `children.purgePhotos.{action,confirmTitle,confirmBody,success,partialFailure,blockedActiveChild}` i18n keys to `web/i18n/locales/{en,fr,nl}.json`.
- [ ] T019 [US1] Add the "Purge photos" destructive text-style action (design-system.md's Destructive button pattern — never filled) to the deactivated-child state of `web/app/(app)/children/[id]/page.tsx`, gated on `children.statusDeactivated` (existing key/state), with a confirmation dialog naming exactly what will be deleted vs. preserved, and a loading/success/partial-failure result state (depends on T017, T018).

**Checkpoint**: A director can purge a departed child's photos end-to-end; group photos with other depicted children are provably safe.

---

## Phase 4: User Story 2 - Parent downloads an original-resolution photo (Priority: P1)

**Goal**: A parent can download the full-resolution original of any photo their child appears in, including group photos, as a file (not inline view).

**Independent Test**: A parent opens a group-activity photo their child is derived as depicted in and downloads it; verify the file is full-resolution with an attachment content-disposition, and that a parent with no relation to the child is denied.

### Tests for User Story 2

- [ ] T020 [P] [US2] Integration test: parent can download original for their own child's profile photo and a group photo they're derived as depicted in; response has attachment content-disposition and points at the full-resolution object, in `backend/ChildCare.Api.Tests/ParentPhotoDownloadTests.cs`.
- [ ] T021 [P] [US2] Test: parent is denied (403) downloading a photo of a child that is not theirs, and denied a group photo their child has no consent/membership basis to see, same test file.

### Implementation for User Story 2

- [ ] T022 [US2] Add `GET /api/parent/photos/{photoType}/{objectRef}/download` route (`ParentOnly`) — new file or extension of the existing parent-facing photo endpoints — reusing the same ownership/consent gate `GetParentGroupActivityGalleryQuery` already applies, calling the new `CreateAttachmentDownloadUrlAsync` (T004/T005/T006) per `photoType` (depends on T004, T005, T006).
- [ ] T023 [P] [US2] Add `gallery.downloadOriginal` / `gallery.downloadFailed` i18n keys to `parent-mobile/i18n/locales/{en,nl,fr}.json`, matching the existing `gallery.*` namespace convention.
- [ ] T024 [US2] Add a "Download original" action (48pt touch target, per platform-rules.md) to `parent-mobile/app/(app)/gallery.tsx`, wired through `parent-mobile/services/groupActivityGallery.ts` (or a new sibling service function) to call T022's endpoint, with a loading spinner on tap and a toast on failure (no internal error detail) — hidden entirely when offline (depends on T022, T023).

**Checkpoint**: A parent can download an original photo end-to-end from the existing gallery screen.

---

## Phase 5: User Story 3 - Archive-on-departure and general cost tiering (Priority: P2)

**Goal**: A deactivated child's photos automatically move to Coldline 30 days after deactivation (once every derived child on a group photo is inactive); all photo types get a general 90-day-no-activity → Nearline tier; none of this is visible to any user.

**Independent Test**: Deactivate a child, back-date `DeactivatedAt` 31 days, run the job, confirm the storage class transitions and the object remains resolvable; confirm a group photo with one remaining active derived child stays on Standard.

### Tests for User Story 3

- [ ] T025 [P] [US3] Unit test: archival eligibility computation — all-derived-children-inactive vs. mixed-active case — for the job's eligibility-selection logic, in `backend/ChildCare.Api.Tests/Cli/EvaluatePhotoArchivalCommandTests.cs` (or an extracted eligibility-service test file if the logic is factored out for testability).
- [ ] T026 [P] [US3] TestContainers integration test: a child deactivated >30 days ago has profile photo + health/vaccine attachments transitioned to Coldline; a group photo with one still-active derived child is not transitioned; re-running the job after that child also becomes inactive (>30d) does transition it.
- [ ] T027 [P] [US3] Test: reactivating an archived child requires no explicit un-archive step — the object remains resolvable via the existing signed-URL flow (storage class alone does not block `CreateDownloadUrlAsync`).

### Implementation for User Story 3

- [ ] T028 [US3] Create `EvaluatePhotoArchivalCommand` in `backend/ChildCare.Api/Cli/EvaluatePhotoArchivalCommand.cs`, mirroring `SendPaymentRemindersCommand.cs`'s shape exactly (tenant loop via `ITenantDbContextResolver`, per-tenant try/catch, exit code) (depends on T007, T008).
- [ ] T029 [US3] Implement per-tenant logic: find children with `DeactivatedAt <= UtcNow.AddDays(-30)`; transition their profile photo and health/vaccine attachments to Coldline (skip if already there) via T007's `SetStorageClassAsync`.
- [ ] T030 [US3] Implement group-activity photo archival: for photos where every `GetDepictedChildIdsAsync` result has been inactive ≥30 days, transition the full-resolution object only (never `-thumb.jpg`) to Coldline.
- [ ] T031 [US3] Implement the general 90-day-no-activity → Nearline tiering for `group-activities/` full-resolution objects only (research.md R5's exception — the other four prefixes are covered by the native Terraform rule in T033), based on object creation time, skipping any object already on Coldline (never downgrade Coldline → Nearline).
- [ ] T032 [US3] Wire `args[0] == "evaluate-photo-archival"` dispatch in `backend/ChildCare.Api/Program.cs`, mirroring the `send-payment-reminders`/`send-daily-reports` branches (depends on T028).
- [ ] T033 [US3] Add the Terraform `lifecycle_rule` to `google_storage_bucket.staff_profile_photos` in `infra/gcp/main.tf` per research.md R5 (`age = 90`, `matches_storage_class = ["STANDARD"]`, `matches_prefix = ["staff/", "children/", "health-records/", "vaccine-records/"]`, `SetStorageClass → NEARLINE`).
- [ ] T034 [US3] Add `google_cloud_run_v2_job` + `google_cloud_scheduler_job` for `evaluate-photo-archival` to `infra/gcp/main.tf`, mirroring the `send-payment-reminders` pair (`max_retries = 0`, daily cron, `Europe/Brussels`).

**Checkpoint**: Storage-class transitions run correctly and invisibly; `terraform plan` shows the new lifecycle rule and job wiring.

---

## Phase 6: User Story 4 - Staff and Director have consistent photo permissions (Priority: P2)

**Goal**: Staff (not just directors) can delete group-activity photos, health records, and vaccine records within their assigned location — closing the gap where upload was already staff-accessible but delete was director-only.

**Independent Test**: Authenticate as staff, delete a group-activity photo / health record / vaccine record at an assigned location — succeeds; a parent attempting any upload/edit/delete on any of the three photo types is still denied.

### Tests for User Story 4

- [ ] T035 [P] [US4] Integration test: staff account successfully deletes a group-activity photo (activity), a health record, and a vaccine record at their assigned location, in `backend/ChildCare.Api.Tests/PhotoRbacParityTests.cs`.
- [ ] T036 [P] [US4] Test: parent account is denied upload/edit/delete on all three photo types (regression — must remain true after the widening), same test file.
- [ ] T037 [P] [US4] Test: staff/director account is denied all three actions for a location they are not assigned to (regression on existing location-scoping).

### Implementation for User Story 4

- [ ] T038 [P] [US4] Widen `DELETE /api/group-activities/{id}` from `DirectorOnly` to `StaffOrDirector` in `backend/ChildCare.Api/Endpoints/GroupActivityEndpoints.cs`.
- [ ] T039 [P] [US4] Widen `DELETE /api/children/{childId}/health-records/{id}` from `DirectorOnly` to `StaffOrDirector` in `backend/ChildCare.Api/Endpoints/HealthRecordEndpoints.cs`.
- [ ] T040 [P] [US4] Widen `DELETE /api/children/{childId}/vaccine-records/{id}` and its sibling `DirectorOnly` route from `DirectorOnly` to `StaffOrDirector` in `backend/ChildCare.Api/Endpoints/VaccineRecordEndpoints.cs` (both occurrences).

**Checkpoint**: All three photo ports enforce identical Staff/Director authorization for upload, delete, and download.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T041 Run quickstart.md's six scenarios end-to-end against a dev environment with a real GCS bucket (storage-class assertions cannot run against the fake-gcs-server emulator).
- [ ] T042 [P] Confirm every new i18n key (web `children.purgePhotos.*`, parent-mobile `gallery.downloadOriginal`/`downloadFailed`) has non-empty NL/FR/EN values — no placeholder/English-only entries.
- [ ] T043 `terraform plan` against `infra/gcp/main.tf` — confirm only the expected lifecycle-rule and job/scheduler diffs appear, no unrelated drift.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: No dependencies — BLOCKS US1, US2, US3 (not US4).
- **US1 (Phase 3)**: Depends on Foundational T002, T003, T008.
- **US2 (Phase 4)**: Depends on Foundational T004, T005, T006.
- **US3 (Phase 5)**: Depends on Foundational T007, T008.
- **US4 (Phase 6)**: No dependency on Foundational — can start anytime, even in parallel with Phase 2.
- **Polish (Phase 7)**: Depends on US1–US4 all complete.

### Parallel Opportunities

- T002–T007 (storage-port interface additions) are all `[P]` — different files, no shared dependencies.
- Once Foundational completes, US1, US2, and US3 can proceed in parallel (different files); US4 can run in parallel with all of them from the start.
- Within US4, T038/T039/T040 touch three different endpoint files — fully parallel.

---

## Implementation Strategy

### MVP First

1. Phase 1 (Setup) → Phase 2 (Foundational) → Phase 3 (US1, purge) — the feature's core
   compliance capability and the one with the highest correctness stakes.
2. **STOP and VALIDATE**: run quickstart.md Scenario 5 independently.

### Incremental Delivery

Foundational → US1 (purge) → US2 (parent download) → US4 (RBAC parity, cheap and independent,
can slot in anywhere) → US3 (archive/tiering, background/invisible, safe to ship last) → Polish.
