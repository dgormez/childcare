---

description: "Task list for feature 023-digital-enrollment"
---

# Tasks: Digital Online Enrollment

**Input**: Design documents from `/specs/023-digital-enrollment/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/enrollment-api.md, quickstart.md

**Tests**: Included — constitution Principle V (Test with Real Infrastructure) requires
TestContainers-backed integration tests covering happy path plus key negative/regulatory flows;
spec.md's Technical Requirements explicitly calls out rate-limit, honeypot, duplicate-flagging,
disabled-location, reference-code, tour-invitation-token, conversion pre-fill, notification, and
locale-respecting-email coverage as required.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Maps to spec.md's User Story 1–4

## Path Conventions

Backend: `backend/ChildCare.{Domain,Application,Contracts,Infrastructure,Api}/...` (existing
five-project solution). Web: `web/{app,lib,i18n}/...` (existing Next.js app). Per plan.md's
Project Structure.

---

## Phase 1: Setup

- [X] T001 [P] Add empty `publicEnrollment` and `waitingList.tourInvitation` i18n key
  namespaces to `web/i18n/locales/{en,nl,fr}.json` (reused the existing `waitingList.duplicateBadge`
  key already present for US2, rather than adding a duplicate)
- [X] T002 [P] Add a `public-enrollment` rate-limit policy (sliding window,
  `RemoteIpAddress`-partitioned, `PermitLimit = 3`, `Window = 1 hour`, research.md R6) to the
  existing `AddRateLimiter` configuration in `backend/ChildCare.Api/Program.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: No user story work can begin until this phase is complete — every story reads
or writes through the `Location`/`WaitingListEntry` schema changes and/or the tour-invitation
token service.

- [X] T003 [P] Add `WaitingListEntrySource` enum (`DirectorEntered`, `SelfRegistered`) in
  `backend/ChildCare.Domain/Enums/WaitingListEntrySource.cs`
- [X] T004 [P] Add `TourInvitationStatus` enum (`NotSent`, `Sent`, `Accepted`, `Declined`) in
  `backend/ChildCare.Domain/Enums/TourInvitationStatus.cs`
- [X] T005 [P] Add `EnrollmentSubmitted` to `backend/ChildCare.Domain/Enums/NotificationType.cs`
- [X] T006 Add `PublicEnrollmentEnabled` (`bool`, default `false`), `PublicEnrollmentSlug`
  (`string`), `DefaultEnrollmentLocale` (`string`, default `"nl"`) to
  `backend/ChildCare.Domain/Entities/Location.cs` per data-model.md
- [X] T007 Add `Source` (default `DirectorEntered`), `ReferenceCode`, `SubmittedLocale`,
  `TourProposedAt`, `TourInvitationStatus` (default `NotSent`), `TourInvitationSentAt`,
  `TourOutcome` to `backend/ChildCare.Domain/Entities/WaitingListEntry.cs` per data-model.md
  (depends on T003, T004)
- [X] T008 Generate the EF Core tenant migration (`dotnet ef migrations add
  AddDigitalEnrollment --project backend/ChildCare.Infrastructure --context TenantDbContext
  --output-dir Persistence/Migrations/Tenant`) including a data-backfill step that assigns a
  unique `PublicEnrollmentSlug` to every pre-existing `Location` row (slugified `Name`, numeric
  suffix on collision, per research.md R1), verify it applies cleanly against a fresh dev
  schema, and generate the manually-run SQL script per this repo's EF-Core-never-auto-migrates-
  in-production convention (`.claude/CLAUDE.md`) (depends on T006, T007) — verified by running
  `migrate-tenants` against all 21 local dev tenant schemas (0 failures) and confirming zero
  empty/duplicate `PublicEnrollmentSlug` values afterward; the SQL script itself is a gitignored,
  dev-local artifact (`backend/**/*.sql`, `.gitignore:24`), not a repo deliverable, per this
  repo's existing convention (confirmed: `migrations.sql` has no git history)
- [X] T009 Create `ITourInvitationTokenService` (`CreateToken(Guid waitingListEntryId)` /
  `TryParseToken(string) : Guid?`, fails closed) + `DataProtectionTourInvitationTokenService` —
  corrected during implementation: this codebase's actual token-signing precedent
  (`DataProtectionUnsubscribeTokenService`, feature 020) uses ASP.NET Core's built-in Data
  Protection API via `IDataProtectionProvider.CreateProtector(purpose)`, not a hand-rolled HMAC
  scheme as research.md R5 assumed before this file was inspected — in
  `backend/ChildCare.Application/Common/ITourInvitationTokenService.cs` and
  `backend/ChildCare.Infrastructure/Email/DataProtectionTourInvitationTokenService.cs`
- [X] ~~T010~~ Not needed — Data Protection API reuses the existing `AddDataProtection()`
  registration (already called once in `Program.cs` for feature 014a/020); no separate signing
  key configuration exists to add, per T009's correction
- [X] T011 Register `ITourInvitationTokenService` in DI in `backend/ChildCare.Api/Program.cs`
  (depends on T009)
- [X] T012 Create `EnrollmentLinkBuilder` (mirrors `EmailLinkBuilder`'s exact
  `BuildXxxUrl(IConfiguration, ...)` shape) with `BuildPublicEnrollmentUrl(orgSlug,
  locationSlug)` (points at the `web/` app, per research.md R1) and
  `BuildTourResponseUrl(token, orgSlug, response)` (points at the API's own server-rendered
  page, per research.md R4) in `backend/ChildCare.Application/WaitingList/EnrollmentLinkBuilder.cs`

**Checkpoint**: Foundation ready — user story implementation can now begin.

---

## Phase 3: User Story 1 - Parent submits the public enrollment form (Priority: P1) 🎯 MVP gate

**Goal**: A prospective parent can submit the public form for an opted-in location and receive a
confirmation with a reference code; the entry is created, the director is notified, and
anti-spam protections hold.

**Independent Test**: Submit the public form for an opted-in location and verify a
`waiting`-status, self-registered entry appears, with a confirmation email sent to the address
provided.

### Tests for User Story 1

- [X] T013 [P] [US1] Integration test: `GET /api/public/enrollment/{orgSlug}/{locationSlug}`
  returns `{locationName, enabled, defaultLocale}` correctly and collapses a bad org/location
  slug to a single `404 errors.public_enrollment.not_found`, in
  `backend/ChildCare.Api.Tests/WaitingList/PublicEnrollmentTests.cs`
- [X] T014 [P] [US1] Integration test: a valid `POST` creates a `WaitingListEntry` with
  `Source = SelfRegistered`, `Status = Waiting`, a unique `ReferenceCode`, and the submitted
  `SubmittedLocale` (FR-007/FR-008), in the same test file as T013
- [X] T015 [P] [US1] Integration test: a submission with the honeypot field filled returns `200`
  with a `referenceCode`-shaped response but creates **no** entry and sends **no** email
  (FR-005), in the same test file as T013
- [X] T016 [P] [US1] Integration test: missing required fields and a future `dateOfBirth` are
  rejected `422` with per-field, locale-aware `errorKey`s (FR-004), in the same test file as
  T013
- [X] T017 [P] [US1] Integration test: a missing `contactEmail` is rejected (required
  specifically for self-registered entries, data-model.md's validation delta from 012a), in the
  same test file as T013
- [X] T018 [P] [US1] Integration test: submitting to a location with `PublicEnrollmentEnabled =
  false` returns `403 errors.public_enrollment.disabled` (FR-013), in the same test file as T013
- [X] T019 [P] [US1] Integration test: a 4th submission from the same source IP within a rolling
  hour is rejected `429`, while the first 3 valid submissions succeed (FR-006/SC-004), in the
  same test file as T013
- [X] T020 [P] [US1] Integration test: a successful submission sends a confirmation email (via
  `FakeEmailSender`, this repo's existing test convention) in the submitted locale, containing
  the reference code (FR-009), in the same test file as T013
- [X] T021 [P] [US1] Integration test: a successful submission creates a `Notification`
  (`Type = EnrollmentSubmitted`) for every director `TenantUser` in the tenant (FR-010,
  data-model.md), in the same test file as T013
- [X] T021a [P] [US1] Integration test (FR-020): a successful submission creates exactly one
  `WaitingListEntry` and writes **zero** `Child`/`Contact` rows — self-registration never
  touches the tenant's authoritative records until a director explicitly converts the entry
  (012a's existing constraint, re-affirmed for this path), in the same test file as T013

### Implementation for User Story 1

- [X] T022 [US1] Create `SubmitPublicEnrollmentCommand` + `SubmitPublicEnrollmentCommandHandler`
  (resolves `orgSlug` → tenant via `OrganisationSlugResolver`, resolves the location by
  `PublicEnrollmentSlug` within that schema, checks `PublicEnrollmentEnabled`, generates a
  unique reference code per research.md R5, creates the `WaitingListEntry`) in
  `backend/ChildCare.Application/WaitingList/SubmitPublicEnrollmentCommand.cs` and
  `SubmitPublicEnrollmentCommandHandler.cs` (depends on T006–T008)
- [X] T023 [US1] Create `GetPublicEnrollmentLocationInfoQuery` + handler in
  `backend/ChildCare.Application/WaitingList/GetPublicEnrollmentLocationInfoQuery.cs` (same
  org/location resolution as T022)
- [X] T024 [US1] Add `SendEnrollmentConfirmationAsync` to `IEmailSender`/`EmailService`
  (`backend/ChildCare.Application/Common/IEmailSender.cs`,
  `backend/ChildCare.Api/Services/EmailService.cs`), a new
  `enrollment-confirmation.scriban` template
  (`backend/ChildCare.Infrastructure/Email/Templates/`), and an `EnrollmentEmailLabels.For(locale)`
  provider (`backend/ChildCare.Api/Services/EnrollmentEmailLabels.cs`), mirroring
  `DailyReportEmailLabels`'s exact shape (depends on T022)
- [X] T025 [US1] Create `EnrollmentNotificationService` (one `Notification` row per director
  `TenantUser` in the tenant schema, per data-model.md) in
  `backend/ChildCare.Application/WaitingList/EnrollmentNotificationService.cs`
- [X] T026 [P] [US1] Add `SubmitPublicEnrollmentRequest`/`Response` and
  `GetPublicEnrollmentLocationInfoResponse` to
  `backend/ChildCare.Contracts/Requests/WaitingListRequests.cs` and
  `Responses/WaitingListResponses.cs` per contracts/enrollment-api.md
- [X] T027 [US1] Create `backend/ChildCare.Api/Endpoints/PublicEnrollmentEndpoints.cs`
  (`MapGroup("/api/public/enrollment")`, `.AllowAnonymous()` + `.RequireTenantExempt()`; `GET
  /{orgSlug}/{locationSlug}`; `POST /{orgSlug}/{locationSlug}` with
  `.RequireRateLimiting("public-enrollment")` and the honeypot short-circuit per contracts/
  enrollment-api.md) (depends on T022, T023, T026)
- [X] T028 [US1] Register `app.MapPublicEnrollmentEndpoints()` in
  `backend/ChildCare.Api/Program.cs` (depends on T027)
- [X] T029 [US1] Create `web/lib/publicApiClient.ts` — an unauthenticated fetch wrapper (no
  session/JWT attached, distinct from the existing `apiClient` which assumes a director session)
- [X] T030 [US1] Create the public enrollment page
  `web/app/enroll/[orgSlug]/[locationSlug]/page.tsx` — form per spec.md's UX Requirements (child
  first/last name, date of birth, requested start date, parent/guardian name, email, phone,
  notes, NL/FR/EN language toggle defaulting to the fetched `defaultLocale`, hidden honeypot
  field), loading/inline-validation/rate-limited/disabled-location states, and a confirmation
  screen showing the reference code (depends on T029)
- [X] T031 [P] [US1] Populate `publicEnrollment` i18n keys (form labels, validation messages,
  confirmation screen, disabled-location message, rate-limit message) in
  `web/i18n/locales/{en,nl,fr}.json`

**Checkpoint**: User Story 1 fully functional and testable independently — a parent can submit
and get confirmed, even though nothing yet consumes the entry on the director side.

---

## Phase 4: User Story 2 - Director reviews and converts a self-registered entry (Priority: P1)

**Goal**: A director sees a new self-registered entry (tagged, duplicate-flagged if applicable)
in the existing waiting-list view and can convert it to an enrolled child with zero retyped
fields.

**Independent Test**: Submit a self-registered entry (User Story 1), then, as a director,
convert it to `enrolled` and verify the resulting child profile and contact record match the
submitted data with no manual retyping.

### Tests for User Story 2

- [X] T032 [P] [US2] Integration test: `ListWaitingListEntriesQuery`'s response includes
  `Source`/`ReferenceCode` and flags a self-registered entry as a possible duplicate when its
  child name + date of birth match another entry at the same location, regardless of that other
  entry's status (FR-011, research.md R3), in
  `backend/ChildCare.Api.Tests/WaitingList/WaitingListDuplicateFlagTests.cs`
- [X] T033 [P] [US2] Integration test: two entries with matching name + date of birth are both
  created and both remain independently visible/actionable — neither is auto-rejected, in the
  same test file as T032
- [X] T034 [P] [US2] Integration test: converting a self-registered entry via the existing
  `TransitionWaitingListStatusCommand` → `LinkChildToWaitingListEntryCommand` flow pre-fills the
  created child's name/date of birth and the created contact's name/email/phone from the entry
  (FR-014/SC-003), in `backend/ChildCare.Api.Tests/WaitingList/PublicEnrollmentConversionTests.cs`

### Implementation for User Story 2

- [X] T035 [US2] Extend `ListWaitingListEntriesQuery`/`WaitingListResult` to include `Source`,
  `ReferenceCode`, and a computed `IsPossibleDuplicate` flag (self-join on
  `ChildFirstName`/`ChildLastName`/`DateOfBirth` within `LocationId`, research.md R3) in
  `backend/ChildCare.Application/WaitingList/ListWaitingListEntriesQuery.cs` — discovered during
  implementation that this duplicate-detection self-join already exists in 012a's
  `WaitingListQueries.BuildFilteredList` (source-agnostic, keyed only on name+DOB+location), so
  no query-level change was needed; T036's `WaitingListMapper.ToResponse` extension alone
  threads `Source`/`ReferenceCode`/tour fields through automatically for every existing caller
- [X] T036 [P] [US2] Extend `WaitingListEntryResponse` in
  `backend/ChildCare.Contracts/Responses/WaitingListResponses.cs` with `Source`,
  `ReferenceCode`, `IsPossibleDuplicate`, and the tour-invitation fields (`TourProposedAt`,
  `TourInvitationStatus`, `TourInvitationSentAt`, `TourOutcome`)
- [X] T037 [US2] Verify (extend if needed) that `LinkChildToWaitingListEntryCommandHandler`'s
  existing pre-fill-on-create-new-child path (012a) correctly sources
  `ChildFirstName`/`ChildLastName`/`DateOfBirth` for a self-registered entry identically to a
  director-entered one, in
  `backend/ChildCare.Application/WaitingList/LinkChildToWaitingListEntryCommand.cs`
- [X] T038 [US2] Add contact-creation pre-fill (`ContactName`/`ContactEmail`/`ContactPhone`) to
  the same conversion flow so the director-facing contact-creation step opens pre-populated from
  the entry (FR-014) — discovered during implementation that no integration point existed at
  all: the backend's `link-child` endpoint only ever created a `Child` (012a), and
  `web/components/children/LinkContactDialog.tsx` (feature 030) was a fully separate,
  child-detail-page-only flow with no caller passing initial values. Added optional
  `initialFirstName`/`initialLastName`/`initialPhone`/`initialEmail`/`initialRelationship` props
  to `LinkContactDialog` (backward-compatible — feature 030's existing caller in
  `ChildContactsTab.tsx` is unaffected) and wired `web/app/(app)/waiting-list/page.tsx` to open
  it, pre-filled from the entry (best-effort first/last split of `ContactName`, since the entry
  stores a single field), immediately after a successful child link/create. Contact creation
  itself remains a director-confirmed web action, not an automatic backend side effect — no FR
  requires the latter, and this stays consistent with 012a's separation of concerns. No backend
  API surface exists for this pre-fill, so it's covered by code review, not an integration test
  (see PublicEnrollmentConversionTests.cs's doc comment)
- [X] T039 [US2] Update `web/app/(app)/waiting-list/page.tsx`: show a "self-registered" tag and
  a "possible duplicate" badge (design-system.md's badge pattern — pill, paired icon, no color
  alone) on flagged rows, and pass the entry's data into the existing child/contact creation
  flow's pre-fill props on conversion
- [X] T040 [P] [US2] Populate `waitingList` i18n keys (self-registered tag, duplicate badge
  label) in `web/i18n/locales/{en,nl,fr}.json`

**Checkpoint**: User Stories 1 AND 2 both work independently — the full submit-to-enrolled loop
is functional.

---

## Phase 5: User Story 3 - Director sends a tour invitation and records the outcome (Priority: P2)

**Goal**: A director can send a tour invitation with a no-login accept/decline link and
separately record the tour's real-world outcome.

**Independent Test**: Send a tour invitation from an entry, follow the accept/decline link as
the recipient, and verify the entry reflects that response; separately record a manual outcome
and verify it's saved independent of any link response.

### Tests for User Story 3

- [X] T041 [P] [US3] Integration test: `POST /api/waiting-list/{id}/tour-invitation` sends an
  email containing accept/decline links and sets `TourInvitationStatus = Sent`,
  `TourProposedAt`, `TourInvitationSentAt`; returns `422
  errors.waiting_list.no_contact_email` when the entry has no contact email, in
  `backend/ChildCare.Api.Tests/WaitingList/TourInvitationTests.cs`
- [X] T042 [P] [US3] Integration test: `GET /api/public/enrollment/tour-response` with a valid
  token and `response=accepted` sets `TourInvitationStatus = Accepted` and renders an HTML
  confirmation page in the entry's `SubmittedLocale`; `response=declined` sets `Declined`, in
  the same test file as T041
- [X] T043 [P] [US3] Integration test: an invalid, tampered, or unparseable token renders the
  generic "invalid or expired link" HTML page and writes nothing (fails closed, mirrors
  `DigestUnsubscribeLinkResolver`), in the same test file as T041
- [X] T044 [P] [US3] Integration test: a tour-response for an entry already `Enrolled` or
  `Withdrawn` renders a "no longer active" page and does **not** alter `TourInvitationStatus`
  (FR-018), in the same test file as T041
- [X] T045 [P] [US3] Integration test: `POST /api/waiting-list/{id}/tour-outcome` saves a
  free-text outcome independent of `TourInvitationStatus` (callable with or without a prior
  invitation), in the same test file as T041
- [X] T045a [P] [US3] Integration test (FR-015): sending a second tour invitation for an entry
  that already has one (e.g. after a `Declined` response) overwrites `TourProposedAt`/
  `TourInvitationSentAt` and resets `TourInvitationStatus` to `Sent` — an entry has at most one
  active invitation, not a history of past ones, in the same test file as T041

### Implementation for User Story 3

- [X] T046 [US3] Create `SendTourInvitationCommand` + handler (generates a token via
  `ITourInvitationTokenService`, builds accept/decline URLs via `EnrollmentLinkBuilder`, sends
  the tour-invitation email, sets `TourInvitationStatus`/`TourProposedAt`/
  `TourInvitationSentAt`, overwriting on re-send per research.md R2) in
  `backend/ChildCare.Application/WaitingList/SendTourInvitationCommand.cs` and
  `SendTourInvitationCommandHandler.cs` (depends on T009, T012)
- [X] T047 [US3] Add `SendTourInvitationAsync` to `IEmailSender`/`EmailService`, a new
  `tour-invitation.scriban` template, and a `TourInvitationEmailLabels.For(locale)` provider,
  mirroring T024's pattern
- [X] T048 [US3] Create `RecordTourOutcomeCommand` + handler in
  `backend/ChildCare.Application/WaitingList/RecordTourOutcomeCommand.cs` and
  `RecordTourOutcomeCommandHandler.cs`
- [X] T049 [US3] Create `RespondTourInvitationCommand` + handler (terminal-status guard per
  FR-018/data-model.md, idempotent per contracts/enrollment-api.md) in
  `backend/ChildCare.Application/WaitingList/RespondTourInvitationCommand.cs` and
  `RespondTourInvitationCommandHandler.cs` (depends on T009)
- [X] T050 [P] [US3] Add `SendTourInvitationRequest` and `RecordTourOutcomeRequest` to
  `backend/ChildCare.Contracts/Requests/WaitingListRequests.cs`
- [X] T051 [US3] Add `POST /api/waiting-list/{id}/tour-invitation` and `POST
  /api/waiting-list/{id}/tour-outcome` (`DirectorOnly`) to
  `backend/ChildCare.Api/Endpoints/WaitingListEndpoints.cs` per contracts/enrollment-api.md
  (depends on T046, T048, T050)
- [X] T052 [US3] Add `GET /api/public/enrollment/tour-response` (`.AllowAnonymous()` +
  `.RequireTenantExempt()`, server-rendered HTML mirroring `EmailEndpoints.RenderUnsubscribePage`)
  to `PublicEnrollmentEndpoints.cs` per contracts/enrollment-api.md (depends on T049)
- [X] T053 [US3] Add a tour-invitation send action (proposed date/time picker) and an
  outcome-entry action to `web/app/(app)/waiting-list/page.tsx`
- [X] T054 [P] [US3] Populate `waitingList.tourInvitation` i18n keys (send action, outcome
  entry, status labels) in `web/i18n/locales/{en,nl,fr}.json`

**Checkpoint**: User Stories 1, 2, AND 3 all work independently.

---

## Phase 6: User Story 4 - Director temporarily disables public enrollment for a location (Priority: P3)

**Goal**: A director can disable/re-enable public enrollment per location; disabling is enforced
server-side, not just hidden in the UI.

**Independent Test**: Disable the setting for a location and verify the public page shows the
disabled message and a direct submission attempt is rejected; re-enable and verify the form
works again with all prior entries intact.

### Tests for User Story 4

- [ ] T055 [P] [US4] Integration test: `PUT /api/locations/{id}/public-enrollment-setting`
  persists the new value and leaves other locations' settings untouched (mirrors feature 021's
  equivalent test), in `backend/ChildCare.Api.Tests/Locations/PublicEnrollmentSettingTests.cs`
- [ ] T056 [P] [US4] Integration test: a submission attempted directly against the endpoint
  immediately after the director disables the setting is rejected `403` even if the client
  hadn't refreshed its own cached state (FR-013's mid-session edge case), in the same test file
  as T055
- [ ] T057 [P] [US4] Integration test: re-enabling the setting leaves every previously submitted
  entry unchanged, in the same test file as T055

### Implementation for User Story 4

- [ ] T058 [US4] Create `UpdateLocationPublicEnrollmentSettingCommand` + handler (mirrors
  `UpdateLocationQrCheckInSettingCommandHandler` exactly — log-only-on-change, per contracts/
  enrollment-api.md) in
  `backend/ChildCare.Application/Locations/UpdateLocationPublicEnrollmentSettingCommand.cs` and
  `UpdateLocationPublicEnrollmentSettingCommandHandler.cs`
- [ ] T059 [P] [US4] Add `UpdateLocationPublicEnrollmentSettingRequest` to
  `backend/ChildCare.Contracts/Requests/LocationRequests.cs`
- [ ] T060 [US4] Add `PUT /api/locations/{locationId}/public-enrollment-setting`
  (`DirectorOnly`) to `backend/ChildCare.Api/Endpoints/LocationEndpoints.cs` (depends on T058,
  T059)
- [ ] T061 [US4] Add the public-enrollment toggle section (label + explanatory copy + the
  location's shareable public URL, shown with a copy action) to
  `web/app/(app)/locations/[id]/page.tsx`
- [ ] T062 [P] [US4] Populate `locations` i18n keys (toggle label, explanatory copy, "copy link"
  action) in `web/i18n/locales/{en,nl,fr}.json`

**Checkpoint**: All four user stories independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T063 [P] Run quickstart.md's six scenarios end-to-end against local dev
- [ ] T064 [P] Confirm no spacing/radius/motion/nested-card deviations from design-system.md on
  the new public enrollment page and the waiting-list/location-settings additions (static
  review, no simulator)
- [ ] T065 Verify the public form's inputs have associated labels, meet WCAG AA contrast, and
  that validation errors are `aria-live`-announced rather than color-only (design-system.md
  Accessibility, FR per spec.md UX Requirements)
- [ ] T066 [P] Test: the migration's slug-backfill (T008) assigns a unique
  `PublicEnrollmentSlug` to every pre-existing `Location` row with no collisions, in
  `backend/ChildCare.Api.Tests` (a migration/slug-generator-focused test)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational only.
- **User Story 2 (Phase 4)**: Depends on Foundational; functionally builds on User Story 1's
  entries existing (T032–T034 need a self-registered entry to test against) but its own code
  changes (list-query extension, conversion pre-fill) touch different files than US1 and are
  independently testable once at least one self-registered entry exists.
- **User Story 3 (Phase 5)**: Depends on Foundational (specifically T009/T012's token service
  and link builder); independent of US2's conversion-pre-fill work.
- **User Story 4 (Phase 6)**: Depends on Foundational (T006's `PublicEnrollmentEnabled` column)
  only — mirrors feature 021's settings-toggle shape closely enough to be built in parallel with
  US1–US3.
- **Polish (Phase 7)**: Depends on all four user stories being complete.

### Parallel Opportunities

- Setup tasks T001–T002 in parallel.
- Foundational T003–T005 (enums) in parallel; T010 in parallel with T006/T007.
- All US1 test tasks (T013–T021) in parallel; T026 in parallel with T022–T025.
- All US2 test tasks (T032–T034) in parallel; T036/T040 in parallel with T035/T037/T038.
- All US3 test tasks (T041–T045) in parallel; T050/T054 in parallel with T046–T049.
- All US4 test tasks (T055–T057) in parallel; T059/T062 in parallel with T058.
- User Stories 1, 3, and 4 can be built in parallel by different developers once Foundational is
  done; User Story 2 is easiest to validate once at least one US1 entry exists but doesn't share
  files with US1's own tasks.

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "Integration test: GET location-info endpoint in PublicEnrollmentTests.cs"
Task: "Integration test: valid submission creates a self-registered entry in PublicEnrollmentTests.cs"
Task: "Integration test: honeypot submission creates no entry in PublicEnrollmentTests.cs"
Task: "Integration test: validation errors are per-field in PublicEnrollmentTests.cs"
Task: "Integration test: missing contact email rejected in PublicEnrollmentTests.cs"
Task: "Integration test: disabled location rejected in PublicEnrollmentTests.cs"
Task: "Integration test: rate limit enforced in PublicEnrollmentTests.cs"
Task: "Integration test: confirmation email sent in submitted locale in PublicEnrollmentTests.cs"
Task: "Integration test: director notification created in PublicEnrollmentTests.cs"

# Then implement:
Task: "Create SubmitPublicEnrollmentCommand + handler"
Task: "Create GetPublicEnrollmentLocationInfoQuery + handler"
```

---

## Implementation Strategy

### MVP First

1. Setup + Foundational.
2. User Story 1 — parents can submit and get confirmed (safe to ship alone; the director simply
   sees new entries appear in the existing 012a waiting-list view, untagged until US2 ships).
3. **STOP and VALIDATE**: confirm SC-002 (default-disabled) and SC-004 (rate limit) via
   T013–T021.
4. User Story 2 — the conversion loop that makes self-registration actually save director time.
5. User Story 3 — tour-invitation tracking (additive value, not required for the core loop).
6. User Story 4 — the capacity/disable safety valve (can also ship earlier in parallel with 2/3,
   since it only depends on Foundational).
7. Polish.

### Incremental Delivery

1. Setup + Foundational → Foundation ready.
2. Add User Story 1 → validate independently → deploy (MVP).
3. Add User Story 2 → validate independently → deploy.
4. Add User Story 3 → validate independently → deploy.
5. Add User Story 4 → validate independently → deploy.
6. Polish.

---

## Notes

- [P] tasks = different files, no dependencies.
- [Story] label maps task to specific user story for traceability.
- Verify tests fail before implementing.
- Commit after each task or logical group.
- Stop at any checkpoint to validate a story independently.
