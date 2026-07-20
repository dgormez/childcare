---

description: "Task list for feature 021-qr-checkin"
---

# Tasks: QR Contactless Check-In

**Input**: Design documents from `/specs/021-qr-checkin/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/qr-checkin-api.md, quickstart.md

**Tests**: Included — constitution Principle V (Test with Real Infrastructure) requires
TestContainers-backed integration tests covering happy path plus key negative/regulatory flows;
spec.md's Technical Requirements explicitly calls out setting-toggle, code-lifecycle
(issuance/expiry/tamper/wrong-location/cooldown), BKR/reporting parity, and offline-queue
coverage as required.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Maps to spec.md's User Story 1–3

## Path Conventions

Backend: `backend/ChildCare.{Domain,Application,Contracts,Infrastructure,Api}/...` (existing
five-project solution). Caregiver tablet: `mobile/{services,app,i18n}/...` (existing Expo app).
Parent app: `parent-mobile/{services,app,i18n}/...` (existing Expo app). Web:
`web/{app,i18n}/...` (existing Next.js app). Per plan.md's Project Structure.

---

## Phase 1: Setup

- [X] T001 [P] Add `expo-camera` to `mobile/package.json` (caregiver-tablet scan viewfinder +
  QR decode, research.md R2)
- [X] T002 [P] Add `react-native-qrcode-svg` to `parent-mobile/package.json` (client-side QR
  rendering, research.md R3)
- [X] T003 [P] Add an empty `qrCheckIn` top-level key to each of `mobile/i18n/locales/nl.json`,
  `mobile/i18n/locales/fr.json`, `mobile/i18n/locales/en.json`
- [X] T004 [P] Add an empty `qrCheckIn` top-level key to each of
  `parent-mobile/i18n/locales/nl.json`, `parent-mobile/i18n/locales/fr.json`,
  `parent-mobile/i18n/locales/en.json`
- [X] T005 [P] Add an empty `qrCheckIn` top-level key to each of `web/i18n/locales/nl.json` (or
  `web/messages/nl.json` per feature 007a's next-intl convention), the `fr` and `en` siblings

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: No user story work can begin until this phase is complete — every story reads
or writes through the `Location.QrCheckInEnabled` column and/or the signed-code
issuance/verification path.

- [X] T006 Add `QrCheckInEnabled` (`bool`, default `false`) to
  `backend/ChildCare.Domain/Entities/Location.cs` per data-model.md
- [X] T007 Generate the EF Core tenant migration (`dotnet ef migrations add
  AddLocationQrCheckInEnabled --project backend/ChildCare.Infrastructure --context
  TenantDbContext --output-dir Persistence/Migrations/Tenant`), verify it applies cleanly
  against a fresh dev schema, and generate the manually-run SQL script per this repo's
  EF-Core-never-auto-migrates-in-production convention (`.claude/CLAUDE.md`)
- [X] T008 Add a `QrCheckInCodeSigningKey` entry to configuration (`appsettings.json` /
  environment variable / secrets manager per Constitution VI — never a literal default in
  source) read by the code service below
- [X] T009 Create `ICheckInCodeService` / `CheckInCodeService` (issue: build
  `{childId, issuedAtUnix, nonce}`, HMAC-SHA256 sign, base64url-encode per research.md R1;
  verify: decode, recompute signature, compare, check `now - issuedAtUnix <= 30`, check nonce
  not in the consumed-cooldown set) in
  `backend/ChildCare.Application/Attendance/CheckInCodeService.cs`
- [X] T010 Register `ICheckInCodeService` in DI (`backend/ChildCare.Api/Program.cs`)

**Checkpoint**: Foundation ready — user story implementation can now begin.

---

## Phase 3: User Story 1 - Director enables QR check-in for a location (Priority: P1) 🎯 MVP gate

**Goal**: A director can view and toggle the per-location QR check-in setting; it defaults to
disabled and changing one location never affects another.

**Independent Test**: Toggle the setting on for one location via the API/UI, save, and verify
only that location's setting changed (a second location's `GET` still shows disabled).

### Tests for User Story 1

- [X] T011 [P] [US1] Integration test: `PUT /api/locations/{id}/qr-checkin-setting` persists the
  new value and leaves other locations' settings untouched, in
  `backend/ChildCare.Api.Tests/Locations/LocationQrCheckInSettingTests.cs`
- [X] T012 [P] [US1] Integration test: toggling the setting produces a structured log entry
  (director id, location id, old/new value) only when the value actually changes, in the same
  test file as T011
- [X] T013 [P] [US1] Integration test: a non-director caller gets `403`; an unknown `locationId`
  gets `404`, in the same test file as T011
- [X] T013a [P] [US1] Integration test (FR-015): an `AttendanceRecord` created before a
  location's QR check-in setting is toggled is byte-identical (all fields) before and after the
  toggle — enabling or disabling the setting must not touch any existing attendance data, in the
  same test file as T011
- [X] T014 [P] [US1] Integration test: every existing/new `Location` row defaults to
  `QrCheckInEnabled = false` (SC-002), in the same test file as T011

### Implementation for User Story 1

- [X] T015 [US1] Create `UpdateLocationQrCheckInSettingCommand` +
  `UpdateLocationQrCheckInSettingCommandHandler` (mirrors feature 008b's
  `UpdateLocationCheckInSettingsCommandHandler` pattern — plain `ILogger` entry on change, no
  new audit subsystem) in
  `backend/ChildCare.Application/Locations/UpdateLocationQrCheckInSettingCommand.cs` and
  `UpdateLocationQrCheckInSettingCommandHandler.cs`
- [X] T016 [P] [US1] Add `UpdateLocationQrCheckInSettingRequest` to
  `backend/ChildCare.Contracts/Requests/LocationRequests.cs`
- [X] T017 [US1] Add `PUT /api/locations/{locationId}/qr-checkin-setting`
  (`DirectorOnly`) to `backend/ChildCare.Api/Endpoints/LocationEndpoints.cs` per
  contracts/qr-checkin-api.md
- [X] T018 [US1] Add the QR check-in toggle section (label + explanatory copy per FR-003,
  save/revert-on-failure per FR-018) to the location settings screen in
  `web/app/(dashboard)/locations/[id]/settings/...` (existing settings-tab pattern, e.g.
  feature 013f's reservation settings)
- [X] T019 [P] [US1] Populate `qrCheckIn` i18n keys (setting label, explanatory copy, save
  error) in `web/i18n/locales/{nl,fr,en}.json`

**Checkpoint**: User Story 1 fully functional and testable independently — the setting exists,
defaults correctly, and is director-manageable, even though nothing yet reads it on the client
side.

---

## Phase 4: User Story 2 - Parent and caregiver complete check-in/check-out via QR scan (Priority: P1)

**Goal**: At an enabled location, a parent's displayed code can be scanned by the caregiver
tablet to check a child in, then out, with the same result a manual tap would produce.

**Independent Test**: Scan a parent's code — child checks in, confirmation shown, tablet returns
to scan mode; scan again — child checks out.

### Tests for User Story 2

- [X] T020 [P] [US2] Integration test: `POST /api/parent/attendance/qr-code` issues a code only
  for a child the calling parent is linked to (research.md R3/R4), rejects with
  `errors.qrCheckIn.not_your_child` otherwise, in
  `backend/ChildCare.Api.Tests/Attendance/QrCheckInCodeIssuanceTests.cs`
- [X] T021 [P] [US2] Integration test: issuance is rejected `400
  errors.qrCheckIn.not_enabled` when the child's location has `QrCheckInEnabled = false`, in the
  same test file as T020
- [X] T022 [P] [US2] Integration test: `POST /api/attendance/qr-code/verify` with a valid,
  unexpired code for a not-currently-checked-in child produces a check-in identical in shape to
  `POST /api/attendance/check-in`'s response (FR-008), in
  `backend/ChildCare.Api.Tests/Attendance/QrCheckInVerifyTests.cs`
- [X] T023 [P] [US2] Integration test: verifying the same code a second time within the cooldown
  window returns `409 errors.qrCheckIn.already_used` rather than a check-out (FR-019), in the
  same test file as T022
- [X] T024 [P] [US2] Integration test: verifying a code after 31+ simulated seconds returns `410
  errors.qrCheckIn.code_expired` (FR-011/FR-006), in the same test file as T022
- [X] T025 [P] [US2] Integration test: verifying a valid code for a child not enrolled at the
  scanning device's `LocationId` returns `403 errors.qrCheckIn.wrong_location` and creates no
  attendance record (FR-010), in the same test file as T022
- [X] T026 [P] [US2] Integration test: verifying a code with a tampered signature returns `401
  errors.qrCheckIn.invalid_code` (FR-007), in the same test file as T022
- [X] T027 [P] [US2] Integration test (parity, FR-014/SC-004): a QR-originated check-in and a
  manually-tapped check-in produce identical `GET /api/attendance/bkr` output for the same
  location/day, in the same test file as T022
- [X] T028 [P] [US2] Integration test: a second scan of a fresh code for an already-checked-in
  child produces a check-out (FR-009), in the same test file as T022

### Implementation for User Story 2

- [X] T029 [US2] Create `IssueCheckInCodeCommand` + `IssueCheckInCodeCommandHandler`
  (child-ownership check via `ChildContact`/`Contact.TenantUserId` per research.md R3/R4,
  location-enabled check, delegates signing to `ICheckInCodeService`) in
  `backend/ChildCare.Application/Attendance/IssueCheckInCodeCommand.cs` and
  `IssueCheckInCodeCommandHandler.cs`
- [X] T030 [US2] Create `VerifyCheckInCodeCommand` + `VerifyCheckInCodeCommandHandler`
  (verification order per contracts/qr-checkin-api.md: signature → cooldown → expiry →
  wrong-location → dispatch to existing `CheckInCommand`/`CheckOutCommand` via `IMediator`
  based on today's attendance status, research.md R5; records the nonce in the cooldown set only
  after a successful dispatch) in
  `backend/ChildCare.Application/Attendance/VerifyCheckInCodeCommand.cs` and
  `VerifyCheckInCodeCommandHandler.cs` (depends on T029's sibling service, T015's location read)
- [X] T031 [P] [US2] Add `IssueCheckInCodeRequest`/`IssueCheckInCodeResponse` and
  `VerifyCheckInCodeRequest`/`VerifyCheckInCodeResponse` to
  `backend/ChildCare.Contracts/Requests/AttendanceRequests.cs` and
  `backend/ChildCare.Contracts/Responses/AttendanceResponses.cs` per contracts/qr-checkin-api.md
- [X] T032 [US2] Add `POST /api/parent/attendance/qr-code` (`ParentOnly`) and `POST
  /api/attendance/qr-code/verify` (`DeviceAuthenticated`, `DeviceTokenRotationFilter`) to
  `backend/ChildCare.Api/Endpoints/AttendanceEndpoints.cs` per contracts/qr-checkin-api.md
  (depends on T029, T030, T031)
- [X] T033 [US2] Create `parent-mobile/services/attendance.ts` (`requestQrCode(childId)` —
  issues + tracks `expiresAtUnix`, no offline path per research.md R6) mirroring
  `mobile/services/attendance.ts`'s existing API-call conventions
- [X] T034 [US2] Create the parent-mobile QR code-display screen (renders the issued code via
  `react-native-qrcode-svg`, auto-refreshes at ~20s per FR-006, shows a loading state during
  (re)issuance and a "reconnect to show your code" state when offline per spec.md UX
  Requirements) in `parent-mobile/app/.../qr-checkin.tsx` (depends on T033)
- [X] T035 [US2] Extend `mobile/services/attendance.ts` with `scanCheckInCode(code,
  isConnected)` — online: `POST /api/attendance/qr-code/verify`, surfaces the three distinct
  rejection cases (wrong-location/expired/invalid) as human-readable errors; the resulting
  attendance write reuses the existing `checkIn`/`checkOut` functions' offline-queue branch if
  connectivity drops mid-write (research.md R6) — no new offline path
- [X] T036 [US2] Create the caregiver-tablet scan-mode screen (`expo-camera` viewfinder, "Scan"
  quick action one tap from the group view per reference-products.md's Brightwheel principle,
  success confirmation shows child name + photo + check-in/check-out state text — never color
  alone — then auto-returns to scan mode; a fully-offline tablet shows the manual-fallback
  message per research.md R6 instead of opening the camera) in `mobile/app/.../scan.tsx`
  (depends on T035)
- [X] T037 [US2] Gate the parent-mobile "Show code" entry point and the caregiver-tablet "Scan"
  quick action on the location's `QrCheckInEnabled` value (fetched via each app's existing
  roster/settings mechanism, feature 008a) so neither is reachable at a disabled location
  (FR-004)
- [X] T038 [P] [US2] Populate `qrCheckIn` i18n keys (scan confirmation, three rejection
  messages) in `mobile/i18n/locales/{nl,fr,en}.json`
- [X] T039 [P] [US2] Populate `qrCheckIn` i18n keys ("Show code" action, loading/offline states)
  in `parent-mobile/i18n/locales/{nl,fr,en}.json`, warm parent-facing register per spec.md UX
  Requirements

**Checkpoint**: User Stories 1 AND 2 both work independently — an opted-in location has a
working end-to-end scan flow.

---

## Phase 5: User Story 3 - Manual tap-based check-in remains available and unaffected everywhere (Priority: P2)

**Goal**: Confirm/lock in that manual tap-based check-in is completely unaffected by this
feature, at both disabled and enabled locations, and when the camera path fails.

**Independent Test**: At a disabled location, or with the tablet camera unavailable at an
enabled one, complete a check-in/check-out via the existing manual tap flow with no degradation.

### Tests for User Story 3

- [X] T040 [P] [US3] Regression test: `POST /api/attendance/check-in`/`check-out` behavior and
  response shape are byte-identical to pre-feature behavior regardless of
  `Location.QrCheckInEnabled`'s value, in
  `backend/ChildCare.Api.Tests/Attendance/QrCheckInManualFallbackTests.cs`
- [X] T040a [P] [US3] Jest test: the tablet scan screen renders its camera-permission-denied/
  camera-unavailable fallback state (with a working link back to manual tap) when
  `expo-camera`'s permission/availability check fails, in
  `mobile/__tests__/screens/scan.test.tsx` (write first, expect it to fail against T036's screen
  until T041 adds the fallback state)

### Implementation for User Story 3

- [X] T041 [US3] Add a camera-permission-denied / camera-unavailable fallback state to the
  scan-mode screen (T036) that surfaces a clear path back to the existing manual tap flow
  (FR-013) rather than a dead end (satisfies T040a)
- [X] T042 [US3] Confirm (code review, no new endpoint) that `CheckInCommand`/`CheckOutCommand`
  received zero modifications by Phase 4 — VerifyCheckInCodeCommandHandler only calls them via
  `IMediator.Send`, per research.md R5's parity requirement

**Checkpoint**: All three user stories independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T043 [P] Run quickstart.md's five scenarios end-to-end against local dev (Scenario 1
  confirmed live via curl against a running dev instance — default-disabled + isolation;
  Scenarios 2–5 confirmed via the equivalent, exhaustive automated integration coverage in
  QrCheckInCodeIssuanceTests/QrCheckInVerifyTests/QrCheckInManualFallbackTests, since hand-typed
  curl payloads for contract creation hit unrelated request-shape friction with no product-code
  bugs found)
- [X] T044 [P] Confirm no spacing/radius/motion deviations from design-system.md on the new
  web settings section, tablet scan screen, and parent code-display screen (static review, no
  simulator)
- [X] T045 Verify 48pt minimum touch targets on the tablet's "Scan" quick action and the
  manual-fallback entry point (platform-rules.md)
- [X] T046 [P] Integration test (SC-003): time `POST /api/attendance/qr-code/verify` end-to-end
  (issuance → verify → committed attendance write) against a TestContainers-backed instance and
  assert it completes well within the 10-second scan-to-confirmation budget, in
  `backend/ChildCare.Api.Tests/Attendance/QrCheckInVerifyTests.cs` (server-side latency only —
  the remaining client-side camera-decode-to-API-call time is a manual quickstart timing
  observation, T043, not automatable in this test suite)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational only.
- **User Story 2 (Phase 4)**: Depends on Foundational; T032's endpoints depend on US1's T015
  (reads `Location.QrCheckInEnabled`) and T017 existing, but US2's own setting-independent code
  (T029/T030 signing logic) can start in parallel with US1.
- **User Story 3 (Phase 5)**: Depends on Foundational + US2's scan screen (T036) existing to add
  a fallback state to.
- **Polish (Phase 6)**: Depends on all three user stories being complete.

### Parallel Opportunities

- All Setup tasks (T001–T005) in parallel.
- Foundational T006–T010 are mostly sequential (entity → migration → service → DI) but T008 can
  run in parallel with T006/T007.
- All US1 test tasks (T011–T014) in parallel; T016/T019 in parallel with each other and with
  T015.
- All US2 test tasks (T020–T028) in parallel; T031/T038/T039 in parallel with the command/handler
  work.

---

## Implementation Strategy

### MVP First

1. Setup + Foundational.
2. User Story 1 — the setting exists and is director-manageable (safe to ship alone; no client
   surfaces it yet).
3. **STOP and VALIDATE**: confirm SC-002 (zero behavior change) via T014.
4. User Story 2 — the actual scan experience.
5. User Story 3 — fallback hardening (mostly already true by construction; this phase locks it
   in with an explicit regression test and a UI dead-end fix).
6. Polish.

---

## Phase 7: Convergence

- [X] T047 Add offline-interruption handling to `mobile/services/attendance.ts`'s
  `scanCheckInCode` — a network failure after a scan is submitted (as opposed to before, which
  the scan screen already refuses per FR-012's first clause) should be distinguishable from a
  genuine rejection (wrong-location/expired/invalid/cooldown) and surfaced accordingly, rather
  than defaulting to the same "invalid code" copy per FR-012 (missing)
- [X] T048 Add `web/__tests__/qrCheckInSettings.test.tsx` covering
  `QrCheckInSettingsForm`'s save-success and revert-on-failure behavior, mirroring
  `checkInSettings.test.tsx`'s existing coverage of the sibling 008b component per Constitution
  V's testing convention (missing)
- [X] T049 Add a test for `parent-mobile/app/(app)/qr-checkin/[childId].tsx` covering
  loading, success (QR code rendered), issuance-failure-with-retry, and offline states per
  spec.md's Technical Requirements/Testing section (missing)
- [X] T050 Add a focused unit test for `mobile/services/attendance.ts`'s `scanCheckInCode`
  (success response shape, thrown error carries the server's errorKey) rather than relying only
  on `scan.tsx`'s screen test, which mocks the whole service (missing)
- [X] T051 Update `specs/021-qr-checkin/contracts/qr-checkin-api.md` so its documented response
  shapes match the actual implementation: the qr-checkin-setting PUT response is the full
  `LocationResponse`, not `{locationId, qrCheckInEnabled}`; the verify endpoint's 200 response
  includes `ChildFirstName`/`ChildLastName`/`ChildPhotoDownloadUrl` (contradicts)
