---

description: "Task list for feature 022-id-verified-registration"
---

# Tasks: ID-Verified Registration

**Input**: Design documents from `/specs/022-id-verified-registration/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included — Constitution Principle V requires integration tests against
TestContainers PostgreSQL for the happy path plus key negative/regulatory flows; this repo's
convention also expects web component tests for new UI.

**Organization**: Tasks are grouped by user story (spec.md) to enable independent implementation
and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US5)

## Path Conventions

Existing monorepo: `backend/ChildCare.*`, `web/` (see plan.md's Project Structure). No `mobile/`
or `parent-mobile/` changes — this feature is director-web only.

---

## Phase 1: Setup

**Purpose**: No new dependencies (research.md — reuses `IPaymentTokenProtector`'s existing
`Microsoft.AspNetCore.DataProtection` package, existing `Tabs`/`Badge`/`Dialog` UI primitives).
Nothing to initialize.

- [ ] T001 Confirm `Microsoft.AspNetCore.DataProtection` is already referenced by
      `backend/ChildCare.Infrastructure/ChildCare.Infrastructure.csproj` (it is, since feature
      014a) and that no new NuGet/npm package is required for this feature

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The `IdDocumentType` enum, extended `Child`/`Contact` entities, the `INrnProtector`
port/adapter, and the tenant migration — every user story depends on all of these.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T002 [P] Create `IdDocumentType` enum (`BirthCertificate`, `KidsId`, `Eid`, `Passport`,
      `Other`) in `backend/ChildCare.Domain/Enums/IdDocumentType.cs` (data-model.md)
- [ ] T003 [P] Add `IdVerifiedAt` (`DateTime?`), `IdVerifiedByUserId` (`Guid?`),
      `IdVerifiedByEmail` (`string?`), `IdDocumentType` (`IdDocumentType?`), `IdDocumentNote`
      (`string?`), `FirstIdVerifiedAt` (`DateTime?`), `FirstIdVerifiedByUserId` (`Guid?`),
      `FirstIdVerifiedByEmail` (`string?`), `EncryptedNrn` (`string?`), `NrnLast4` (`string?`)
      properties to `Child` in `backend/ChildCare.Domain/Entities/Child.cs`, positioned after
      `Kindcode` (data-model.md)
- [ ] T004 [P] Add `IdVerifiedAt`, `IdVerifiedByUserId`, `IdVerifiedByEmail`, `IdDocumentType`,
      `IdDocumentNote`, `FirstIdVerifiedAt`, `FirstIdVerifiedByUserId`, `FirstIdVerifiedByEmail`
      properties to `Contact` in `backend/ChildCare.Domain/Entities/Contact.cs`, positioned after
      `DigestUnsubscribedAt` (data-model.md)
- [ ] T005 [P] Add `INrnProtector` interface (`string Protect(string plaintext)`, `string
      Unprotect(string ciphertext)`) to `backend/ChildCare.Application/Common/INrnProtector.cs`,
      mirroring `IPaymentTokenProtector.cs` exactly (research.md R3)
- [ ] T006 Add `NrnProtector` class implementing `INrnProtector` via `IDataProtectionProvider`
      (purpose string `"Child.NationalRegisterNumber"`) in
      `backend/ChildCare.Infrastructure/Children/NrnProtector.cs`, mirroring
      `PaymentTokenProtector.cs` exactly (depends on T005)
- [ ] T007 Register `INrnProtector` → `NrnProtector` in the DI container (same registration site
      as `IPaymentTokenProtector` → `PaymentTokenProtector` in
      `backend/ChildCare.Api/Program.cs`) (depends on T006)
- [ ] T008 Add EF Core configuration for the new `Child` properties in
      `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`'s `Entity<Child>` block
      (~line 371, after `PediatricianPhone`): `IdVerifiedByEmail`/`FirstIdVerifiedByEmail`
      `HasMaxLength(254)` (mirrors `Contact.Email`), `IdDocumentNote` `HasMaxLength(500)`,
      `IdDocumentType` `HasConversion(...)`/`HasMaxLength(20)` mirroring the existing
      `AllergySeverity` nullable-enum conversion at ~line 362, `NrnLast4` `HasMaxLength(4)`, no
      explicit config needed for the two `Guid?`/two `DateTime?` attribution fields or
      `EncryptedNrn` (depends on T003)
- [ ] T009 Add the equivalent EF Core configuration for the new `Contact` properties (same field
      shapes minus NRN) to `Entity<Contact>`'s block (~line 393) in the same file (depends on
      T004)
- [ ] T010 Generate the EF Core migration `AddIdentityVerificationAndNrn` (additive, all-nullable
      columns on `children` and `contacts`, no backfill) and its SQL script in
      `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/`, per this repo's
      manual-apply convention (CLAUDE.md, research.md R7) (depends on T008, T009)
- [ ] T011 Add explicit `DROP COLUMN` statements for all 10 new `children` columns and all 6 new
      `contacts` columns from T003/T004 to the legacy-schema revert SQL block in
      `backend/ChildCare.Api.Tests/VaccineRecords/LegacyVaccinationMigrationTests.cs` (this file
      never drops `children`/`contacts` wholesale, only ALTERs — every migration adding columns
      to either table has needed this same fix; see that file's own `AddPediatricianContactToChild`
      entry for the pattern to copy) (depends on T010). Confirm no equivalent change is needed in
      `backend/ChildCare.Api.Tests/TenantMigrationRolloutTests.cs` — it `DROP TABLE`s `children`
      and `contacts` wholesale, so new columns there need no separate revert step.

**Checkpoint**: Foundation ready — user story implementation can now begin.

---

## Phase 3: User Story 1 - Director confirms a child's identity (Priority: P1) 🎯 MVP

**Goal**: A director can record a child's identity verification (document type + optional note)
from the child's file, producing an auditable timestamp/verifier.

**Independent Test**: Open an unverified child's file, complete "Identiteit bevestigen", confirm
the record shows the verified state; attempting to confirm with no document type is blocked.

### Tests for User Story 1

- [ ] T012 [P] [US1] Backend test: `VerifyChildIdentityCommand` happy path (document type only,
      no note) sets `IdVerifiedAt`/`IdVerifiedByUserId`/`IdVerifiedByEmail`/`IdDocumentType` and
      also sets `FirstIdVerifiedAt`/`FirstIdVerifiedByUserId`/`FirstIdVerifiedByEmail` to the same
      values on a previously-unverified child, in
      `backend/ChildCare.Api.Tests/VerifyChildIdentityTests.cs`
- [ ] T013 [P] [US1] Backend test: missing/invalid `documentType` returns `400
      errors.child.document_type_required` and persists nothing, in the same file
- [ ] T014 [P] [US1] Backend test: verifying a child enrolled months ago sets `IdVerifiedAt` to
      the current server time, not any earlier date (spec.md Edge Cases — retroactive
      verification), in the same file
- [ ] T015 [P] [US1] Backend test: `404 errors.child.not_found` for a non-existent child ID, in
      the same file

### Implementation for User Story 1

- [ ] T016 [US1] Create `VerifyChildIdentityCommand(Guid ChildId, IdDocumentType DocumentType,
      string? Note, Guid VerifiedByUserId, string VerifiedByEmail) : IRequest<ChildResult>` in
      `backend/ChildCare.Application/Children/VerifyChildIdentityCommand.cs` (data-model.md)
      (depends on T002, T003)
- [ ] T017 [US1] Create `VerifyChildIdentityCommandValidator` (`DocumentType` required/valid
      enum → `errors.child.document_type_required`; `Note` `MaximumLength(500)` →
      `errors.child.identity_note_too_long`) in
      `backend/ChildCare.Application/Children/VerifyChildIdentityCommandValidator.cs` (depends on
      T016)
- [ ] T018 [US1] Create `VerifyChildIdentityCommandHandler`: load child (404 via
      `ChildResult.Fail(ChildFailure.NotFound)` if missing, mirrors
      `UpdateChildCommandHandler.cs`), set `IdVerifiedAt = DateTime.UtcNow`,
      `IdVerifiedByUserId`/`IdVerifiedByEmail` = request values, `IdDocumentType`/`IdDocumentNote`
      = request values; if `FirstIdVerifiedAt is null`, also set
      `FirstIdVerifiedAt`/`FirstIdVerifiedByUserId`/`FirstIdVerifiedByEmail` to the same
      values/time — otherwise leave them untouched; save; return via `ChildMapper.ToResponse`, in
      `backend/ChildCare.Application/Children/VerifyChildIdentityCommandHandler.cs` (depends on
      T016, T017)
- [ ] T019 [US1] Extend `ChildMapper.ToResponse` in
      `backend/ChildCare.Application/Children/ChildMapper.cs` to take a new
      `bool includeIdentityVerification = true` parameter; project `IdVerifiedAt`,
      `IdVerifiedByEmail`, `IdDocumentType` (`.ToString()`), `IdDocumentNote`,
      `FirstIdVerifiedAt`, `FirstIdVerifiedByEmail`, `NrnLast4` only when it's `true` — `null` for
      all seven fields when `false` (spec.md FR-015, research.md R8) (depends on T003)
- [ ] T019a [US1] Update `GetChildByIdQuery`'s and `ListChildrenQuery`'s handlers in
      `backend/ChildCare.Application/Children/GetChildByIdQuery.cs`/`ListChildrenQuery.cs` to pass
      `includeIdentityVerification: string.Equals(request.CallerRole, "director",
      StringComparison.OrdinalIgnoreCase)` into their `ChildMapper.ToResponse` calls — every other
      call site (`CreateChildCommandHandler`, `UpdateChildCommandHandler`,
      `VerifyChildIdentityCommandHandler`, `SetChildNrnCommandHandler`, all `DirectorOnly`-only)
      keeps the default `true` (depends on T019)
- [ ] T020 [US1] Add `IdVerifiedAt` (`DateTime?`), `IdVerifiedByEmail` (`string?`),
      `IdDocumentType` (`string?`), `IdDocumentNote` (`string?`), `FirstIdVerifiedAt`
      (`DateTime?`), `FirstIdVerifiedByEmail` (`string?`), `NrnLast4` (`string?`) fields to
      `ChildResponse` in `backend/ChildCare.Contracts/Responses/ChildResponse.cs` (depends on
      T019)
- [ ] T021 [US1] Add `VerifyChildIdentityRequest(string DocumentType, string? Note)` to
      `backend/ChildCare.Contracts/Requests/ChildRequests.cs`
- [ ] T022 [US1] Add `POST /api/children/{id:guid}/identity-verification` to the `DirectorOnly`
      group in `backend/ChildCare.Api/Endpoints/ChildrenEndpoints.cs`: resolve
      `(userId, email)` from `ClaimTypes.NameIdentifier`/`ClaimTypes.Email` (mirrors
      `PlatformAdminVaccineTypeEndpoints.cs`'s `ActingUserOf` helper), parse `documentType` via
      the existing `ParseEnum<IdDocumentType>` pattern, send `VerifyChildIdentityCommand`, map via
      the existing `MapResult` helper (depends on T018, T020, T021)
- [ ] T022a [P] [US1] Backend test: `GET /api/children`/`GET /api/children/{id}` return `null`
      for all seven verification/NRN fields when called with a Staff JWT or a device-token
      session, and return the real values when called with a Director JWT, on a verified child, in
      `backend/ChildCare.Api.Tests/VerifyChildIdentityTests.cs` (depends on T019a, T022)
- [ ] T023 [US1] [P] Create `ChildIdentityVerificationSection.tsx` in
      `web/components/children/`: read-only display (document type label, note, verifying
      director email, timestamp) when `child.idVerifiedAt` is set; a document-type `<select>` +
      optional note `<Textarea>` + "Bevestigen" button when unset or when the director chooses to
      correct it; submit disabled until a document type is chosen (FR-003); calls `onVerify`
      prop, mirrors `ChildProfileTab.tsx`'s controlled-component style (no react-hook-form)
      (depends on T020)
- [ ] T024 [US1] Wire `ChildIdentityVerificationSection` into the `profile` `TabsContent` on
      `web/app/(app)/children/[id]/page.tsx`, below `ChildProfileTab`/`ChildMealPreferenceForm`;
      add a `verifyChildIdentity` handler calling `POST
      /api/children/{id}/identity-verification` then `load()` (mirrors `submitChildEdit`'s
      shape) (depends on T022, T023)
- [ ] T025 [US1] [P] Add `children.identity.*` i18n keys (section title "Identiteit bevestigen",
      document-type option labels, note label, confirm button, validation message, "verified by
      X on Y" display strings) to `web/i18n/locales/{en,fr,nl}.json`
- [ ] T026 [US1] [P] Web test: `ChildIdentityVerificationSection` — confirm button disabled with
      no document type selected; submits `documentType`+`note` and shows the read-only state
      after a successful save, in `web/__tests__/components/children/ChildIdentityVerificationSection.test.tsx`
- [ ] T027 [US1] Regenerate `web/lib/generated/api-types.ts` via `npm run generate-api-client`
      against the backend running with T010's migration applied (depends on T022)

**Checkpoint**: User Story 1 is fully functional and independently testable — a director can
verify a child's identity end-to-end.

---

## Phase 4: User Story 2 - Director confirms a parent/guardian contact's identity (Priority: P1)

**Goal**: The same verification capability as US1, on `Contact` instead of `Child`, reachable
from the existing per-child Contacts tab.

**Independent Test**: Open an unverified contact's row in a child's Contacts tab, complete
verification, confirm the row shows verified; open a second child sharing that same contact and
confirm it's already verified there too.

### Tests for User Story 2

- [ ] T028 [P] [US2] Backend test: `VerifyContactIdentityCommand` happy path sets current +
      first-verification attribution on a previously-unverified contact, in
      `backend/ChildCare.Api.Tests/VerifyContactIdentityTests.cs`
- [ ] T029 [P] [US2] Backend test: missing `documentType` returns `400
      errors.contact.document_type_required`, in the same file
- [ ] T030 [P] [US2] Backend test: a contact linked to two children (feature 030 sibling setup)
      shows as verified via `GET /api/children/{childId}/contacts` for both children after a
      single verification call, in the same file
- [ ] T031 [P] [US2] Backend test: `404 errors.contact.not_found` for a non-existent contact ID,
      in the same file

### Implementation for User Story 2

- [ ] T032 [US2] Create `VerifyContactIdentityCommand(Guid ContactId, IdDocumentType
      DocumentType, string? Note, Guid VerifiedByUserId, string VerifiedByEmail) :
      IRequest<ContactResult>` in
      `backend/ChildCare.Application/Contacts/VerifyContactIdentityCommand.cs` (depends on T002,
      T004)
- [ ] T033 [US2] Create `VerifyContactIdentityCommandValidator` (same rules as
      `VerifyChildIdentityCommandValidator`, `errors.contact.*` keys) in
      `backend/ChildCare.Application/Contacts/VerifyContactIdentityCommandValidator.cs` (depends
      on T032)
- [ ] T034 [US2] Create `VerifyContactIdentityCommandHandler` — same first/current attribution
      logic as `VerifyChildIdentityCommandHandler` (T018), on `Contact`, in
      `backend/ChildCare.Application/Contacts/VerifyContactIdentityCommandHandler.cs` (depends on
      T032, T033)
- [ ] T035 [US2] Extend `ContactMapper` in
      `backend/ChildCare.Application/Contacts/ContactMapper.cs` to project the same six
      verification fields onto both `ContactResponse` and `ChildContactResponse` (depends on T004)
- [ ] T036 [US2] Add `IdVerifiedAt`, `IdVerifiedByEmail`, `IdDocumentType`, `IdDocumentNote`,
      `FirstIdVerifiedAt`, `FirstIdVerifiedByEmail` fields to both `ContactResponse` and
      `ChildContactResponse` in `backend/ChildCare.Contracts/Responses/ContactResponse.cs`
      (depends on T035)
- [ ] T037 [US2] Add `VerifyContactIdentityRequest(string DocumentType, string? Note)` to
      `backend/ChildCare.Contracts/Requests/ContactRequests.cs`
- [ ] T038 [US2] Add `POST /api/contacts/{id:guid}/identity-verification` to the `DirectorOnly`
      group in `backend/ChildCare.Api/Endpoints/ContactsEndpoints.cs`, same claim-resolution
      pattern as T022, using `MapContactResult`/`MapContactFailure` (add an
      `errors.contact.document_type_required`/`identity_note_too_long` case) (depends on T034,
      T036, T037)
- [ ] T039 [US2] [P] Create `ContactIdentityVerificationDialog.tsx` in
      `web/components/children/`, mirroring `LinkContactDialog.tsx`'s modal structure —
      document-type select, optional note, submit calls `POST
      /api/contacts/{contactId}/identity-verification` (depends on T036)
- [ ] T040 [US2] Add a per-row verify action (icon button, opens
      `ContactIdentityVerificationDialog`) and a verified/unverified `Badge` to each contact row
      in `web/components/children/ChildContactsTab.tsx`, alongside the existing "set
      primary"/"remove" actions (depends on T039)
- [ ] T041 [US2] [P] Add `children.contacts.identity.*` i18n keys (dialog title, verify action
      label, verified/unverified badge text) to `web/i18n/locales/{en,fr,nl}.json`
- [ ] T042 [US2] [P] Web test: `ContactIdentityVerificationDialog` submits and closes on success,
      in `web/__tests__/components/children/ContactIdentityVerificationDialog.test.tsx`
- [ ] T043 [US2] Regenerate `web/lib/generated/api-types.ts` (depends on T038)

**Checkpoint**: User Stories 1 and 2 both work independently — children and contacts can each be
verified.

---

## Phase 5: User Story 3 - Director corrects or updates a verification record (Priority: P2)

**Goal**: A director can re-run verification on an already-verified child/contact (e.g., a
document-type change) without losing the original verification's attribution.

**Independent Test**: Verify a child as "Birth certificate", then verify again as "eID"; confirm
the current record shows "eID" while the original verifier/date is still visible unchanged.

### Tests for User Story 3

- [ ] T044 [P] [US3] Backend test: calling `VerifyChildIdentityCommand` a second time with a
      different `DocumentType` updates `IdVerifiedAt`/`IdVerifiedByUserId`/`IdDocumentType` but
      leaves `FirstIdVerifiedAt`/`FirstIdVerifiedByUserId`/`FirstIdVerifiedByEmail` exactly as
      they were after the first call, in `backend/ChildCare.Api.Tests/VerifyChildIdentityTests.cs`
      (extends T012's file)
- [ ] T045 [P] [US3] Backend test: the same correction-preserves-first-attribution behavior for
      `VerifyContactIdentityCommand`, in
      `backend/ChildCare.Api.Tests/VerifyContactIdentityTests.cs` (extends T028's file)

### Implementation for User Story 3

- [ ] T046 [US3] Extend `ChildIdentityVerificationSection.tsx` to show both attribution pairs
      when `child.firstIdVerifiedAt !== child.idVerifiedAt` (e.g., "First verified by X on
      [date]" and "Most recently confirmed by Y on [date]"), and to allow re-opening the
      form from the read-only state to submit a correction (depends on T023)
- [ ] T047 [US3] Apply the same both-attribution display to
      `ContactIdentityVerificationDialog.tsx`/the `ChildContactsTab.tsx` row (depends on T039,
      T040)
- [ ] T048 [US3] [P] Add the "first verified" / "most recently confirmed" i18n label keys to
      `web/i18n/locales/{en,fr,nl}.json` (depends on T025, T041)
- [ ] T049 [US3] [P] Web test: `ChildIdentityVerificationSection` shows both attribution lines
      only when they differ, and shows just one when first == current, extending T026's test file

**Checkpoint**: All three identity-verification stories (US1–US3) are complete — verification,
per-entity coverage, and correction all work end-to-end.

---

## Phase 6: User Story 4 - Director sees which dossiers still need verification (Priority: P2)

**Goal**: An admin-home count plus a per-child list badge surface which actively-enrolled
children still lack identity verification.

**Independent Test**: With a mix of verified/unverified enrolled children (including one with no
attendance history yet), confirm the dashboard count and the `/children` list badges both match
exactly, and that deactivating an unverified child removes it from both.

### Tests for User Story 4

- [ ] T050 [P] [US4] Backend test: `GetDataCompletenessQuery` includes a
      `missing_identity_verification` flag for an active, never-attended child with no
      `IdVerifiedAt` (proving the new flag's scoping is independent of the query's existing
      attendance-linked child set — research.md R5), in
      `backend/ChildCare.Api.Tests/Reporting/DataCompletenessEndpointsTests.cs`
- [ ] T051 [P] [US4] Backend test: a deactivated unverified child produces no
      `missing_identity_verification` flag, in the same file
- [ ] T052 [P] [US4] Backend test: verifying a flagged child removes its flag on the next query,
      in the same file

### Implementation for User Story 4

- [ ] T053 [US4] Add a `missing_identity_verification`-producing block to
      `GetDataCompletenessQueryHandler` in
      `backend/ChildCare.Application/Reporting/GetDataCompletenessQuery.cs`: query all `Child`
      rows with `DeactivatedAt == null` and `IdVerifiedAt == null` (intersected with
      `locationIds` via `ChildGroupAssignment`, mirroring `ListChildrenQuery`'s location-scoping
      shape — not the handler's existing attendance-linked `childIds` variable), append one
      `DataCompletenessFlagResponse("missing_identity_verification", "child", ...)` per match
      (research.md R5) (depends on T003)
- [ ] T054 [US4] Add the `missing_identity_verification` label mapping to
      `web/components/reporting/DataCompletenessSection.tsx`'s `LABEL_KEY` record (depends on
      T053)
- [ ] T055 [US4] Add a "Niet geverifieerd" `Badge` (`variant="warning"`, clock icon per
      `design-system.md`'s status-indicator pairing) to unverified rows in the children table in
      `web/app/(app)/children/page.tsx`, reading `child.idVerifiedAt` from the already-returned
      list response (depends on T020)
- [ ] T056 [US4] [P] Add `dashboard.reporting.dataCompleteness.missingIdentityVerification` and
      `children.badgeUnverified` i18n keys to `web/i18n/locales/{en,fr,nl}.json`
- [ ] T057 [US4] [P] Web test: `/children` list renders the unverified badge only for children
      with no `idVerifiedAt`, in `web/__tests__/app/children/page.test.tsx` (or nearest existing
      convention for that screen)
- [ ] T058 [US4] [P] Web test: `DataCompletenessSection` renders the new flag type's label
      correctly, extending its existing test file

**Checkpoint**: The dashboard and list-page signals both accurately reflect verification state,
including for never-attended new enrolments.

---

## Phase 7: User Story 5 - Director records a child's National Register Number (Priority: P3)

**Goal**: A director can record, encrypt, and only ever see the last 4 digits of a child's NRN.

**Independent Test**: Enter a valid NRN, save, reload, confirm only the last 4 digits ever
display again and no log contains the plain-text value; an invalid-format NRN is rejected.

### Tests for User Story 5

- [ ] T059 [P] [US5] Backend test: `SetChildNrnCommand` with a validly-formatted NRN (both plain
      11-digit and dotted/dashed input) persists `EncryptedNrn` (ciphertext, not equal to the
      input) and `NrnLast4` (last 4 digits of the normalized input), in
      `backend/ChildCare.Api.Tests/SetChildNrnTests.cs`
- [ ] T060 [P] [US5] Backend test: an NRN that normalizes to other than 11 digits returns `400
      errors.child.nrn_invalid_format` and persists nothing, in the same file
- [ ] T061 [P] [US5] Backend test: the raw NRN never appears in `ChildResponse` (only
      `NrnLast4`) and never appears in the serialized request/response logged by the test's own
      HTTP capture, in the same file

### Implementation for User Story 5

- [ ] T062 [US5] Create `SetChildNrnCommand(Guid ChildId, string Nrn) : IRequest<ChildResult>` in
      `backend/ChildCare.Application/Children/SetChildNrnCommand.cs` (depends on T003)
- [ ] T063 [US5] Create `SetChildNrnCommandValidator`: strip non-digit characters, require
      exactly 11 digits remain → `errors.child.nrn_invalid_format` (research.md R4), in
      `backend/ChildCare.Application/Children/SetChildNrnCommandValidator.cs` (depends on T062)
- [ ] T064 [US5] Create `SetChildNrnCommandHandler`: load child (404 if missing), normalize the
      input (strip non-digits), compute `NrnLast4` from the normalized 11-digit string, encrypt
      the normalized string via `INrnProtector.Protect` into `EncryptedNrn`, save, return via
      `ChildMapper.ToResponse`, in
      `backend/ChildCare.Application/Children/SetChildNrnCommandHandler.cs` (depends on T005,
      T062, T063)
- [ ] T065 [US5] Add `SetChildNrnRequest(string Nrn)` to
      `backend/ChildCare.Contracts/Requests/ChildRequests.cs`
- [ ] T066 [US5] Add `PUT /api/children/{id:guid}/nrn` to the `DirectorOnly` group in
      `backend/ChildCare.Api/Endpoints/ChildrenEndpoints.cs` (depends on T064, T065)
- [ ] T067 [US5] Add an NRN entry field (masked display of `NrnLast4` when set, plain text input
      when unset/being changed) to `ChildIdentityVerificationSection.tsx` (depends on T023, T020)
- [ ] T068 [US5] [P] Add `children.identity.nrn*` i18n keys (label, masked-display format,
      invalid-format validation message) to `web/i18n/locales/{en,fr,nl}.json`
- [ ] T069 [US5] [P] Web test: `ChildIdentityVerificationSection`'s NRN field shows only a masked
      value after save and rejects a non-11-digit input client-side before submit, extending
      T026's test file
- [ ] T070 [US5] Regenerate `web/lib/generated/api-types.ts` (depends on T066)

**Checkpoint**: All five user stories are independently functional. Full feature scope complete.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Design-compliance pass, converge findings, and final validation across all stories.

- [ ] T071 [P] Verify all new UI (badge, dialog, section) uses only design-system.md's spacing
      scale (4/8/12/16/24/32), the existing `Badge` variant set (no new colors), and no nested
      cards — fix any deviation found
- [ ] T072 [P] Verify every new user-facing string across `ChildIdentityVerificationSection`,
      `ContactIdentityVerificationDialog`, `ChildContactsTab`, `DataCompletenessSection`, and the
      `/children` list page resolves through `web/i18n/locales/{en,fr,nl}.json` — no hardcoded
      strings (Constitution IV)
- [ ] T073 Run `.specify` `speckit-converge` against this feature once T001–T070 are implemented
      and fix every finding it surfaces (standing pipeline rule — no LOW-severity items left as
      debt)
- [ ] T074 Run the full backend test suite (`dotnet test`) and the full web test suite (`npm
      test` in `web/`) as blocking foreground calls; fix any failure before proceeding
- [ ] T075 Execute quickstart.md's five scenarios manually against a locally running stack and
      confirm each "Expect" outcome

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories.
- **User Stories (Phase 3–7)**: All depend on Foundational completion.
  - US1 (child verification) and US2 (contact verification) are independent of each other and
    can proceed in parallel.
  - US3 (correction display) depends on US1's and US2's components existing (T023, T039, T040)
    — it extends them rather than creating new files.
  - US4 (dashboard/badge) depends on US1's `ChildResponse.IdVerifiedAt` (T020) existing.
  - US5 (NRN) depends on US1's `ChildIdentityVerificationSection.tsx` (T023) existing as the
    place to add the NRN field, and on Foundational's `INrnProtector` (T005–T007).
  - Practical order: US1 → US2 → US3 → US4 → US5 (matches priority order and each story's actual
    file dependencies), though US1/US2 could run in parallel with separate developers.
- **Polish (Phase 8)**: Depends on all five user stories being complete.

### Parallel Opportunities

- All Foundational `[P]` tasks (T002–T005, T008–T009 pairs) can run in parallel once their own
  direct dependency is met.
- US1 and US2's backend command/handler/contract tasks touch entirely separate files
  (`Children/` vs `Contacts/`) and can be built in parallel by different developers once
  Foundational is done.
- Within each story, all test tasks marked `[P]` can run in parallel (same file, independent
  test methods) before their implementation tasks begin.

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "Backend test: VerifyChildIdentityCommand happy path in backend/ChildCare.Api.Tests/VerifyChildIdentityTests.cs"
Task: "Backend test: missing documentType returns 400 in backend/ChildCare.Api.Tests/VerifyChildIdentityTests.cs"
Task: "Backend test: retroactive verification timestamp in backend/ChildCare.Api.Tests/VerifyChildIdentityTests.cs"
Task: "Backend test: 404 for missing child in backend/ChildCare.Api.Tests/VerifyChildIdentityTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories).
3. Complete Phase 3: User Story 1 (child identity verification).
4. **STOP and VALIDATE**: run quickstart.md Scenario 1 against a local stack.
5. Deploy/demo if ready — this alone already delivers the core Opgroeien/GDPR compliance value
   for children (contacts, correction display, dashboard signal, and NRN can follow
   incrementally).

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. US1 (child verification) → independently testable → demo (MVP).
3. US2 (contact verification) → independently testable → demo.
4. US3 (correction attribution display) → independently testable → demo.
5. US4 (dashboard count + list badge) → independently testable → demo.
6. US5 (NRN) → independently testable → demo.
7. Polish (Phase 8) → converge, full suite, quickstart validation → ready to merge.

---

## Notes

- `[P]` tasks = different files, no dependencies.
- `[Story]` label maps task to specific user story for traceability.
- No `mobile/`/`parent-mobile/` tasks anywhere in this list — this feature is director-web only
  (spec.md Assumptions).
- Commit after each task or logical group; stop at any checkpoint to validate a story
  independently.
