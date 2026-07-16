# Tasks: Fiscal Attestations

**Input**: Design documents from `specs/015-fiscal-attestations/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Required by constitution Principle V (real PostgreSQL via TestContainers for backend
integration tests; component tests for web/parent-mobile), same standard every prior feature has
followed.

**Organization**: Tasks are grouped by user story to enable independent implementation and
testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Contracts DTOs and i18n scaffolding shared across all stories.

- [X] T001 [P] Add `FiscalAttestationResponse` (id, childId, childName, locationId,
  locationName, taxYear, totalAmountCents, status, generatedAt, periods[]),
  `GenerateFiscalAttestationsRequest` (`{ taxYear }`), and
  `GenerateFiscalAttestationsResultResponse` (per-item childId/locationId/status) per
  contracts/fiscal-attestations-api.md in
  `backend/ChildCare.Contracts/Responses/FiscalAttestationResponse.cs` and
  `backend/ChildCare.Contracts/Requests/FiscalAttestationRequests.cs`
- [X] T002 [P] Add director-web `fiscalAttestations.*` i18n keys (nav label, screen title, tax
  year selector, generate action, per-row status labels — generated/no paid invoices/failed,
  regenerate action, empty state) to `web/i18n/locales/en.json`, `web/i18n/locales/fr.json`,
  `web/i18n/locales/nl.json`
- [X] T003 [P] Add parent-mobile `fiscalAttestations.*` i18n keys (list, download action,
  not-available-yet empty state) to `parent-mobile/i18n/locales/en.json`,
  `parent-mobile/i18n/locales/fr.json`, `parent-mobile/i18n/locales/nl.json`
- [X] T004 [P] Add `parent.notifications.fiscal_attestation_ready.title`/`.body` i18n keys
  (mirrors `parent.notifications.invoice_sent.*`'s existing naming, 014) to the same three
  parent-mobile locale files

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The `FiscalAttestation` entity, storage/PDF ports, and shared aggregation logic
every user story depends on.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 Add `FiscalAttestation` entity (Id, ChildId, LocationId, TaxYear, Periods (JSON text),
  TotalAmountCents, PdfObjectPath, GeneratedAt, CreatedAt, UpdatedAt) in
  `backend/ChildCare.Domain/Entities/FiscalAttestation.cs` per data-model.md
- [X] T006 [P] Add `FiscalAttestationGenerated` to `NotificationType` in
  `backend/ChildCare.Domain/Enums/NotificationType.cs`
- [X] T007 Configure `FiscalAttestation` (unique index on `(ChildId, LocationId, TaxYear)`) in
  `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs` (depends on T005)
- [X] T008 Add tenant migration `AddFiscalAttestations` in
  `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/` (depends on T007)
- [X] T009 Extend `TenantMigrationRolloutTests`' schema-revert helper for the new table (the
  recurring pattern every migration-adding feature since 003 has needed — most recently 014/
  014a) in `backend/ChildCare.Api.Tests/TenantMigrationRolloutTests.cs` (depends on T008)
- [X] T010 [P] Define `FiscalAttestationPeriod` record (PeriodStart, PeriodEnd, Days,
  AmountCents, DailyRateCents?) and its JSON (de)serialization helper — mirrors
  `InvoiceLineItems`' raw-JSON-text precedent (014), not a JSONB EF value converter — in
  `backend/ChildCare.Application/FiscalAttestations/FiscalAttestationPeriods.cs`
- [X] T011 [P] Define `IFiscalAttestationPdfGenerator` port + `FiscalAttestationPdfModel` (mirrors
  `IInvoicePdfGenerator`'s shape — location/tenant identity fields, parent name, child
  name+DOB, tax year, periods, total, blank-NRN marker, locale) in
  `backend/ChildCare.Application/Common/IFiscalAttestationPdfGenerator.cs`
- [X] T012 [P] Define `IFiscalAttestationStorage` port (`UploadAsync(attestationId, pdfBytes) ->
  ObjectPath`, `CreateDownloadUrlAsync(objectPath) -> signed URL`) in
  `backend/ChildCare.Application/Common/IFiscalAttestationStorage.cs`
- [X] T013 [US-shared] Implement `QuestPdfFiscalAttestationGenerator` (per-locale `Labels`
  dictionary for the Opgroeien declaration/certification-code-1 text, mirrors
  `QuestPdfInvoiceGenerator`'s pattern exactly; the NRN field is always rendered blank — the
  model type has no field that could hold one, per FR-007) in
  `backend/ChildCare.Infrastructure/Pdf/QuestPdfFiscalAttestationGenerator.cs` (depends on T011)
- [X] T014 Implement `GcsFiscalAttestationStorage` (server-side direct `StorageClient
  .UploadObjectAsync` write to `fiscal-attestations/{attestationId}.pdf` using the API's own
  credentials — research.md R1, mirrors `GcsGroupActivityPhotoStorage`'s upload half; reads via
  `UrlSigner`, mirrors every existing `Gcs*Storage` port's download half; reuses the
  `Storage:ProfilePhotosBucketName` bucket, no new bucket/Terraform change) in
  `backend/ChildCare.Infrastructure/Storage/GcsFiscalAttestationStorage.cs` (depends on T012)
- [X] T015 Implement `FiscalAttestationAggregator` (research.md R3 — queries `Paid` invoices for
  a (ChildId, LocationId, TaxYear), reads each invoice's stored `LineItems.DailyRateCents` and
  `PresentDays + UnjustifiedAbsentDays`, merges consecutive same-rate months into periods, sums
  `Invoice.TotalCents` per period — never `Days * DailyRateCents` — and consolidates overflow
  beyond 4 periods per FR-004) in
  `backend/ChildCare.Application/FiscalAttestations/FiscalAttestationAggregator.cs` (depends on
  T010)
- [X] T016 Implement the shared `GenerateOrReplaceAttestationAsync` helper (calls the aggregator,
  renders via `IFiscalAttestationPdfGenerator`, uploads via `IFiscalAttestationStorage`, and
  upserts the `FiscalAttestation` row — the one shared "render + store + upsert" operation both
  bulk-generate (US1, skips existing rows) and regenerate (US3, always overwrites) build on) in
  `backend/ChildCare.Application/FiscalAttestations/FiscalAttestationWriter.cs` (depends on T013,
  T014, T015)
- [X] T017 [P] Implement `FiscalAttestationNotificationService` (mirrors
  `InvoiceNotificationService`'s in-app `Notification` row + best-effort push pattern exactly,
  `FiscalAttestationGenerated` type, `parent.notifications.fiscal_attestation_ready.*` keys —
  research.md R5, spec.md FR-016) in
  `backend/ChildCare.Application/FiscalAttestations/FiscalAttestationNotificationService.cs`
  (depends on T006)
- [X] T018 [P] Add `FiscalAttestationMapper` (entity → `FiscalAttestationResponse`) in
  `backend/ChildCare.Application/FiscalAttestations/FiscalAttestationMapper.cs` (depends on T001)
- [X] T019 Register `IFiscalAttestationPdfGenerator`→`QuestPdfFiscalAttestationGenerator` and
  `IFiscalAttestationStorage`→`GcsFiscalAttestationStorage` in
  `backend/ChildCare.Api/Program.cs` (depends on T013, T014)
- [X] T020 [P] Integration test: `FiscalAttestationAggregator` merges consecutive same-rate
  months into one period and splits on a mid-year `DailyRateCents` change into two periods with
  correct start/end/days/amount, in
  `backend/ChildCare.Api.Tests/FiscalAttestations/FiscalAttestationAggregatorTests.cs`
- [X] T021 [P] Integration test: `FiscalAttestationAggregator` consolidates more than 4 detected
  periods into 4, merging the oldest overflow periods and leaving `DailyRateCents` null on the
  merged period (spec.md Edge Cases/FR-004), in the same test file
- [X] T022 [P] Integration test: `FiscalAttestationAggregator` returns no periods for a
  child/location/year with zero `Paid` invoices, and periods sum to only that location's paid
  invoices when the child also has `Paid` invoices at a different location in the same year
  (research.md R6), in the same test file
- [X] T023 [P] Structural test: `FiscalAttestationPdfModel`/`FiscalAttestationResponse` contain
  no NRN/SSIN-shaped field anywhere (FR-007) — asserted via reflection over the record's
  properties, in
  `backend/ChildCare.Api.Tests/FiscalAttestations/FiscalAttestationNoNrnFieldTests.cs`

**Checkpoint**: Entity, storage/PDF ports, aggregation, and the shared write helper are ready —
user story implementation can now begin.

---

## Phase 3: User Story 1 - Director bulk-generates attestations for a tax year (Priority: P1) 🎯 MVP

**Goal**: A director triggers bulk generation for a tax year and gets one accurate attestation
per eligible child (and per location, for a transferred child), with failures isolated per child
and every generated child's contacts notified.

**Independent Test**: Seed several children with `Paid` invoices across a tax year (including one
with a mid-year rate change and one with zero paid invoices), call
`POST /api/fiscal-attestations/generate`, and confirm the correct set of attestations is produced
— independent of regeneration (Story 3) or the parent-facing download (Story 2).

### Tests for User Story 1

- [X] T024 [P] [US1] Integration test: `GenerateFiscalAttestationsCommand` produces one
  attestation per eligible child for the tax year, skips the child with zero paid invoices
  (status `alreadyExists`... i.e. `noPaidInvoices`, not an error), and produces two rows for the
  multi-location child (research.md R6), in
  `backend/ChildCare.Api.Tests/FiscalAttestations/GenerateFiscalAttestationsCommandTests.cs`
- [X] T025 [P] [US1] Integration test: re-running `GenerateFiscalAttestationsCommand` for a tax
  year that already has some attestations leaves existing rows completely untouched
  (`GeneratedAt`/`TotalAmountCents` unchanged) and only creates rows for children that didn't
  have one yet (FR-009), in the same test file
- [X] T026 [P] [US1] Integration test: every newly generated attestation triggers exactly one
  `FiscalAttestationNotificationService` call (in-app `Notification` row created, best-effort
  push attempted for a contact with a push token — mirrors `InvoiceNotificationService`'s
  existing test pattern, per the standing lesson that a service must actually be exercised by a
  test, not assumed), in the same test file
- [X] T027 [P] [US1] Integration test: when generation fails for one child within a bulk run
  (seed a condition that forces a failure for exactly one child, e.g. a pre-existing GCS
  object-path collision or a malformed pre-seeded row), the rest of the batch still completes
  and that child's result status is `failed` (FR-010) — do not leave this untested with a
  "not easily testable" comment; follow the same fault-injection approach this codebase already
  uses for cross-tenant failure isolation (`MigrateTenantsCommand`/`SendPaymentRemindersCommand`
  tests), in the same test file

### Implementation for User Story 1

- [X] T028 [US1] Implement `GenerateFiscalAttestationsCommand` (queries every child with a `Paid`
  invoice in the tax year across all of the organisation's locations, groups by
  (ChildId, LocationId), skips pairs that already have a row, calls
  `GenerateOrReplaceAttestationAsync` per pair inside a per-item try/catch, calls
  `FiscalAttestationNotificationService` on success) in
  `backend/ChildCare.Application/FiscalAttestations/GenerateFiscalAttestationsCommand.cs`
  (depends on T016, T017)
- [X] T029 [US1] Implement `ListFiscalAttestationsQuery` (every child with a `Paid` invoice that
  year, left-joined against existing `FiscalAttestation` rows to compute transient
  generated/notYetGenerated status per contracts/fiscal-attestations-api.md) in
  `backend/ChildCare.Application/FiscalAttestations/ListFiscalAttestationsQuery.cs` (depends on
  T018)
- [X] T030 [US1] Wire `POST /api/fiscal-attestations/generate` and
  `GET /api/fiscal-attestations?taxYear=` (`DirectorOnly`) in
  `backend/ChildCare.Api/Endpoints/FiscalAttestationEndpoints.cs` (depends on T028, T029)
- [X] T031 [US1] Regenerate and commit `web/lib/generated/api-types.ts` against the new endpoints
  (`npm run generate-api-client` in `web/`)
- [X] T032 [US1] Add `/fiscal-attestations` to `web/components/Sidebar.tsx`'s `REAL_NAV` (research.md
  R7 — flat top-level entry, `FileCheck2` icon, matching `invoices`' existing placement)
- [X] T033 [US1] Create `web/app/(app)/fiscal-attestations/page.tsx` (tax year selector, generate
  action, per-child status list) and `web/components/fiscal-attestations/FiscalAttestationTable.tsx`
  (depends on T031, T032)
- [X] T034 [P] [US1] Web component test: the generate action produces a per-row status list, and
  a child with `noPaidInvoices` status renders distinctly from `failed`, in
  `web/__tests__/fiscalAttestations.test.tsx`

**Checkpoint**: Directors can bulk-generate and see per-child status — User Story 1 is
independently demonstrable via the API/director-web screen.

---

## Phase 4: User Story 2 - Parent downloads their child's attestation (Priority: P1)

**Goal**: A parent can find and download their child's generated attestation(s) from the parent
app, scoped to children they're a linked contact of, with no NRN pre-filled.

**Independent Test**: Pre-seed a generated attestation (directly, or via Story 1's command) and
confirm the linked parent can list and download it — independent of how generation happened.

### Tests for User Story 2

- [X] T035 [P] [US2] Integration test: `GetParentFiscalAttestationsQuery` returns only
  attestations for children the requesting contact is linked to, and any linked contact (not
  only the primary one) sees the same list — mirrors `GetParentInvoicesQuery`'s established
  "every linked contact sees the same children's data" precedent (014), in
  `backend/ChildCare.Api.Tests/FiscalAttestations/GetParentFiscalAttestationsQueryTests.cs`
- [X] T036 [P] [US2] Integration test: the parent download-URL endpoint returns the identical
  not-found outcome for an attestation that doesn't exist and one that exists but belongs to a
  child the caller isn't linked to (enumeration-resistance, mirrors
  `GenerateParentInvoicePdfQuery`'s existing precedent), in
  `backend/ChildCare.Api.Tests/FiscalAttestations/FiscalAttestationDownloadUrlTests.cs`
- [X] T037 [P] [US2] Integration test: a parent with no generated attestation for a given tax
  year gets an empty list, not an error, in `GetParentFiscalAttestationsQueryTests.cs`

### Implementation for User Story 2

- [X] T038 [US2] Implement `GetParentFiscalAttestationsQuery` (mirrors `GetParentInvoicesQuery`'s
  contact-resolution shape) and `GetFiscalAttestationDownloadUrlQuery` (director + parent
  variants, tenant/ownership-scoped) in
  `backend/ChildCare.Application/FiscalAttestations/GetParentFiscalAttestationsQuery.cs` and
  `backend/ChildCare.Application/FiscalAttestations/GetFiscalAttestationDownloadUrlQuery.cs`
  (depends on T012, T018)
- [X] T039 [US2] Wire `GET /api/parent/fiscal-attestations`,
  `GET /api/parent/fiscal-attestations/{id}/download-url`, and
  `GET /api/fiscal-attestations/{id}/download-url` (director) in
  `backend/ChildCare.Api/Endpoints/FiscalAttestationEndpoints.cs` (depends on T038)
- [X] T040 [US2] Regenerate and commit `parent-mobile/services/generated/api-types.ts` against
  the new endpoints (`npm run generate-api-client` in `parent-mobile/`)
- [X] T041 [US2] Create `parent-mobile/services/fiscalAttestations.ts` (list + download, mirrors
  `services/invoices.ts`'s exact `expo-file-system`/`expo-sharing` download pattern, 014)
  (depends on T040)
- [X] T042 [US2] Create `parent-mobile/app/(app)/fiscal-attestations/index.tsx` (list per tax
  year, download action, not-available-yet empty state) (depends on T041)
- [X] T043 [P] [US2] Parent-mobile component test: the list shows only the caller's children's
  attestations and downloads successfully; an empty year shows the not-available state, in
  `parent-mobile/__tests__/fiscalAttestations.test.tsx`
- [X] T044 [US2] Integration test: the attestation PDF served via the download flow is a valid,
  non-empty PDF (`application/pdf` content-type, `%PDF` magic bytes — mirrors
  `InvoicePdfTests.GetPdf_ForSentInvoice_ReturnsValidPdf`'s exact assertion shape, 014) and its
  underlying model has no NRN field ever populated (FR-007/spec.md User Story 2 acceptance
  scenario 3), in `FiscalAttestationDownloadUrlTests.cs` (depends on T038)

**Checkpoint**: Parents can self-serve download — combined with User Story 1, the full
generate-then-retrieve loop works end to end.

---

## Phase 5: User Story 3 - Director corrects a single attestation (Priority: P2)

**Goal**: A director can regenerate one child's (and location's) attestation independently of the
full batch, re-aggregating current paid-invoice data and replacing the existing PDF in place.

**Independent Test**: Generate an attestation, change the underlying paid-invoice data, regenerate
that one child's attestation, and confirm the update is isolated to that row.

### Tests for User Story 3

- [X] T045 [P] [US3] Integration test: `RegenerateFiscalAttestationCommand` re-aggregates and
  replaces the existing row in place (same `Id`, updated `Periods`/`TotalAmountCents`/
  `GeneratedAt`, same GCS object path overwritten, not a new object) — no duplicate row for the
  same (ChildId, LocationId, TaxYear), in
  `backend/ChildCare.Api.Tests/FiscalAttestations/RegenerateFiscalAttestationCommandTests.cs`
- [X] T046 [P] [US3] Integration test: regenerating one child's attestation does not modify any
  other child's attestation for the same tax year, in the same test file
- [X] T047 [P] [US3] Integration test: regenerating a (child, location, year) with zero `Paid`
  invoices fails with `errors.fiscalAttestation.no_paid_invoices` and creates/overwrites nothing,
  in the same test file
- [X] T048 [P] [US3] Integration test: regenerating triggers a second
  `FiscalAttestationNotificationService` call (FR-016 applies to regeneration too), in the same
  test file
- [X] T049 [P] [US3] Integration test: after a director regenerates a child's attestation, a
  subsequent bulk `GenerateFiscalAttestationsCommand` run for the same tax year leaves the
  regenerated row untouched (FR-009 interaction with Story 1, quickstart.md Scenario 4), in
  `backend/ChildCare.Api.Tests/FiscalAttestations/GenerateFiscalAttestationsCommandTests.cs`

### Implementation for User Story 3

- [X] T050 [US3] Implement `RegenerateFiscalAttestationCommand` (calls
  `GenerateOrReplaceAttestationAsync` unconditionally for the given
  ChildId/LocationId/TaxYear — also serves as "generate one" if no row exists yet) in
  `backend/ChildCare.Application/FiscalAttestations/RegenerateFiscalAttestationCommand.cs`
  (depends on T016, T017)
- [X] T051 [US3] Wire
  `POST /api/fiscal-attestations/{childId}/{locationId}/{taxYear}/regenerate` (`DirectorOnly`)
  in `backend/ChildCare.Api/Endpoints/FiscalAttestationEndpoints.cs` (depends on T050)
- [X] T052 [US3] Add a per-row "Regenerate" action to
  `web/components/fiscal-attestations/FiscalAttestationTable.tsx` (depends on T031, T033, T051)
- [X] T053 [P] [US3] Web component test: the regenerate action updates only the targeted row's
  status/timestamp in the table, in `web/__tests__/fiscalAttestations.test.tsx`

**Checkpoint**: All three user stories complete — bulk generate, parent download, and per-child
correction are all demonstrable end to end.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Cross-story validation and accessibility.

- [X] T054 [P] Accessibility pass: the director-web Fiscal Attestations screen (table
  keyboard-navigable with visible focus rings, per `platform-rules.md`'s Director Web section)
  and the parent-mobile list/download screen (48pt touch targets, per `platform-rules.md`'s
  Parent Mobile section)
- [X] T055 Run quickstart.md's five scenarios manually/via integration tests and confirm each
  passes, including the multi-location split (Scenario 5) and the bulk-re-run-doesn't-clobber
  check (Scenario 4)
- [X] T056 Confirm FR-007/FR-015 explicitly: grep the full diff for this feature for any
  NRN/SSIN/`nationaalRegisternummer`/`rijksregisternummer`-named field, column, log statement, or
  DTO property — none should exist anywhere outside a comment explaining why the PDF field is
  left blank

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: T005-T009 (entity/migration) have no Setup dependency; T010-T019
  build on them sequentially per the listed `depends on` notes. BLOCKS all user stories.
- **User Stories (Phase 3-5)**: All depend on Foundational (Phase 2) completion.
  - US1 (P1) has no dependency on US2/US3 — bulk generation and its director-web screen are
    independently testable via seeded `Paid` invoices.
  - US2 (P1) depends on at least one attestation existing (from US1, or seeded directly per its
    own Independent Test) but not on US1's UI — the parent-facing query/download is independently
    testable against directly-seeded rows.
  - US3 (P2) depends on the shared `GenerateOrReplaceAttestationAsync` helper (Foundational) and
    conceptually follows US1 (there's nothing to regenerate until something exists), but its own
    command/tests seed a row directly rather than depending on US1's endpoint.
- **Polish (Phase 6)**: Depends on all three user stories being complete.

### Parallel Opportunities

- T001-T004 (Setup) can all run in parallel.
- T006, T010, T011, T012 (Foundational, independent files) can run in parallel.
- T020-T023 (Foundational tests) can run in parallel.
- T024-T027 (US1 tests) can run in parallel.
- T035-T037 (US2 tests) can run in parallel.
- T045-T048 (US3 tests) can run in parallel.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: run quickstart.md Scenario 1 against seeded test data
5. Demo if ready — bulk generation works technically, even before parents can self-serve download

### Incremental Delivery

1. Setup + Foundational → schema, storage/PDF ports, aggregation, shared writer ready
2. Add User Story 1 → validate independently (Scenario 1) → demo (directors can generate)
3. Add User Story 2 → validate independently (Scenario 2) → demo (parents can retrieve — combined
   with US1, the full generate-then-retrieve loop is real)
4. Add User Story 3 → validate independently (Scenario 3) → demo (directors can correct a single
   attestation without disturbing the batch)
5. Polish (Phase 6) → run all five `quickstart.md` scenarios end to end, including the
   multi-location split and NRN-never-persisted checks

---

## Notes

- [P] tasks touch different files, or the same file in a way that doesn't conflict with other
  in-flight [P] tasks in the same phase.
- [Story] label maps each task to its user story for traceability.
- T027 exists specifically to avoid the mistake 014a's own convergence pass caught elsewhere in
  this codebase: a per-item try/catch loop that exists in code but is never actually exercised by
  a test, with a comment incorrectly asserting it "isn't testable." Use the same fault-injection
  approach already proven for `MigrateTenantsCommand`/`SendPaymentRemindersCommand`.
- Commit after each task or logical group, per this repo's standing convention.

## Phase 7: Convergence

- [X] T057 Add integration test proving a child who left the KDV mid-year (contract with an
  `EndDate`, no further `Paid` invoices after departure) produces an attestation covering only
  the months they were actually enrolled and paid for per US1/AC4 (partial), in
  `backend/ChildCare.Api.Tests/FiscalAttestations/GenerateFiscalAttestationsCommandTests.cs`
- [X] T058 Add integration test proving that after a director regenerates an attestation, the
  parent-facing `GET /api/parent/fiscal-attestations` endpoint reflects the corrected totals
  (not a stale prior version) per US3/AC3 (partial), in
  `backend/ChildCare.Api.Tests/FiscalAttestations/RegenerateFiscalAttestationCommandTests.cs`
