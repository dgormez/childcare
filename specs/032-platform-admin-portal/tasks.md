---

description: "Task list for feature 032-platform-admin-portal"
---

# Tasks: Platform-Admin Portal — Invitations, Registration & Organisation Directory

**Input**: Design documents from `/specs/032-platform-admin-portal/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included — constitution Principle V requires integration tests against TestContainers
PostgreSQL for the happy path plus key negative flows per feature (here: the `PlatformAdminOnly`
policy boundary on every new endpoint, and the generic not-found handling on the registration
page's invalid-token states, since a leaky distinction there would be a real security regression
per FR-011).

**Organization**: Tasks are grouped by user story (spec.md) to enable independent implementation
and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US5)

## Path Conventions

Existing monorepo: `backend/ChildCare.*`, `web/` (see plan.md's Project Structure). No mobile
changes — this feature is director-web only (plus one public, unauthenticated web page).

---

## Phase 1: Setup

**Purpose**: Nothing to remove or initialize — this feature extends feature 001's existing
`Invitation` entity and `Invitations/` MediatR folder, and feature 013h's existing
`PlatformAdminVaccineTypeEndpoints`/`platform-admin/` route pattern. No new projects
(constitution Principle VII). Proceed directly to Foundational.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The extended `Invitation` entity/migration, the locale-aware invitation email, and
the registration link builder — every user story depends on all of these existing first.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T001 Add `OrganisationNameNote (string?)`, `Locale (string, default "nl")`, `CreatedByUserId (Guid?)`, `CreatedByEmail (string?)`, `RevokedByUserId (Guid?)`, `RevokedByEmail (string?)`, `RevokedAt (DateTime?)` to `Invitation` per data-model.md (research.md R12), in `backend/ChildCare.Domain/Entities/Invitation.cs`
- [X] T002 Add EF model configuration for the seven new `Invitation` columns to `PublicDbContext` — `OrganisationNameNote` max 200, `Locale` max 2 with a check constraint (`'nl'|'fr'|'en'`), no DB-level FK on `CreatedByUserId`/`RevokedByUserId` (research.md R4/R12) (depends on T001), in `backend/ChildCare.Infrastructure/Persistence/PublicDbContext.cs`
- [X] T003 Generate the Public-schema EF Core migration `AddPlatformAdminInvitationFields` (depends on T002), in `backend/ChildCare.Infrastructure/Persistence/Migrations/Public/`
- [X] T004 [P] Add `SendOrganisationInvitationAsync(string toEmail, string locale, string? organisationNameNote, string registerUrl)` to `IEmailSender` per research.md R9, in `backend/ChildCare.Application/Common/IEmailSender.cs`
- [X] T005 Implement `SendOrganisationInvitationAsync` (locale-aware template, 3 locales) in `backend/ChildCare.Api/Services/EmailService.cs`, and add the matching method to `backend/ChildCare.Api.Tests/FakeEmailSender.cs` (depends on T004)
- [X] T006 [P] Create `OrganisationInvitationLinkBuilder.BuildRegisterUrl(config, token)` → `{App:OrganisationRegisterBaseUrl}?token={token}` per research.md R8, in `backend/ChildCare.Application/Invitations/OrganisationInvitationLinkBuilder.cs`
- [X] T007 Add `App:OrganisationRegisterBaseUrl` config key (default `http://localhost:3000/register`) to `backend/ChildCare.Api/appsettings.json` and `appsettings.Development.example.json` (depends on T006) — done via the code-level fallback only: confirmed `App:PublicEnrollmentBaseUrl` (the equivalent key for feature 023) isn't present in either appsettings file either, only in `EnrollmentLinkBuilder`'s own `??` default; production sets this class of key via Terraform/env var, never committed
- [X] T007a **Found during implementation** (spec.md User Story 2, AC1/AC3 — not in the original task breakdown): `GetInvitationInfoByTokenQuery`/`GetInvitationInfoByTokenQueryHandler`, `InvitationInfoResponse` contract, and `GET /api/organisations/register/{token}` (rate-limited, tenant-exempt) in `OrganisationEndpoints.cs` — feature 001's `POST /api/organisations/register` alone only validates the token at final submission, so the registration page had no way to pre-fill/lock the email or show an invalid-link state before that; this closes that gap. Files: `backend/ChildCare.Application/Invitations/GetInvitationInfoByTokenQuery.cs`, `backend/ChildCare.Contracts/Responses/InvitationInfoResponse.cs`, `backend/ChildCare.Api/Endpoints/OrganisationEndpoints.cs`
- [X] T007b [P] Integration tests for `GET /api/organisations/register/{token}`: valid token returns the email; unknown/expired/revoked tokens all return the same generic 404 (research.md R5's posture extended here), in `backend/ChildCare.Api.Tests/PlatformAdmin/GetInvitationInfoByTokenTests.cs` (depends on T007a)
- [X] T008 [P] Integration test: the new `Invitation` columns persist and round-trip correctly (defaults, nullability) through `PublicDbContext`, in `backend/ChildCare.Api.Tests/PlatformAdmin/InvitationSchemaTests.cs` (depends on T003)
- [X] T008a [P] Add an `RateLimiterPolicies.OrganisationRegister` options class and register a `"organisation-register"` sliding-window policy in `Program.cs`'s `AddRateLimiter` block, mirroring `RateLimiterPolicies.PublicEnrollment`'s existing shape (research.md R13), in `backend/ChildCare.Api/RateLimiting/RateLimiterPolicies.cs` and `backend/ChildCare.Api/Program.cs`
- [X] T008b Apply `.RequireRateLimiting("organisation-register")` to `POST /api/organisations/register` (depends on T008a), in `backend/ChildCare.Api/Endpoints/OrganisationEndpoints.cs`
- [X] T008c [P] Unit test exercising the real `SlidingWindowRateLimiter` against `RateLimiterPolicies.OrganisationRegister`'s options directly (mirrors feature 023's equivalent test, since `AddRateLimiter` is disabled codebase-wide in the Testing environment), in `backend/ChildCare.Api.Tests/PlatformAdmin/OrganisationRegisterRateLimitTests.cs` (depends on T008a)

**Checkpoint**: Schema, email capability, link builder, and rate limiting exist; solution builds.
User story implementation can now begin.

---

## Phase 3: User Story 1 - Platform-admin invites a prospective director (Priority: P1) 🎯 MVP (half)

**Goal**: A platform-admin can create a director/organisation invitation (email + optional
org-name note + locale), triggering a locale-aware email with a working registration link.

**Independent Test**: Log in as a platform-admin, submit an email + note + locale on the
Invitations screen, confirm a real `Invitation` row is created and an email is sent; a director
without the flag gets `403` on the same endpoint.

### Tests for User Story 1

- [X] T009 [P] [US1] Integration test: `POST /api/platform-admin/invitations` creates a Pending invitation with `createdByEmail` set from the caller's own claims (never the request body, research.md R12), defaults `locale` to `"nl"` when omitted, sends the invitation email via `IEmailSender` (asserted through `FakeEmailSender`); rejects an invalid email with `422`; a director without the flag gets `403`, in `backend/ChildCare.Api.Tests/PlatformAdmin/CreatePlatformAdminInvitationTests.cs`
- [X] T010 [P] [US1] Integration test: creating a second invitation for an email with an existing Pending/Expired invitation marks the prior one Revoked (attributed to the acting platform-admin, per research.md R3) and only the new one is usable, in `backend/ChildCare.Api.Tests/PlatformAdmin/InvitationSupersedeTests.cs`

### Implementation for User Story 1

- [X] T011 [US1] `CreatePlatformAdminInvitationRequest`/`PlatformAdminInvitationResponse` contracts per contracts/platform-admin-portal-api.md, in `backend/ChildCare.Contracts/Requests/PlatformAdminInvitationRequests.cs` and `backend/ChildCare.Contracts/Responses/PlatformAdminInvitationResponse.cs`
- [X] T012 [US1] `CreatePlatformAdminInvitationCommand` + handler (extends `CreateInvitationCommandHandler`'s existing supersede loop to also populate `RevokedByUserId`/`Email`/`At` on the superseded row, per research.md R3; sets `CreatedByUserId`/`CreatedByEmail` on the new row from the acting user passed in by the endpoint, per research.md R12; sends the email via `OrganisationInvitationLinkBuilder` + `IEmailSender.SendOrganisationInvitationAsync`) + FluentValidation validator, in `backend/ChildCare.Application/Invitations/CreatePlatformAdminInvitationCommand.cs` (depends on T001, T004, T006, T011)
- [X] T013 [US1] `PlatformAdminInvitationEndpoints.cs`: map `POST /api/platform-admin/invitations`, `.RequireAuthorization("PlatformAdminOnly")`, acting-user resolved via the same `ActingUserOf(HttpContext)` pattern as `PlatformAdminVaccineTypeEndpoints.cs`, in `backend/ChildCare.Api/Endpoints/PlatformAdminInvitationEndpoints.cs` (depends on T012)
- [X] T014 [US1] Register `PlatformAdminInvitationEndpoints` in the endpoint-mapping startup code, in `backend/ChildCare.Api/Program.cs` (depends on T013)
- [X] T015 [US1] Regenerate `web/lib/generated/api-types.ts` (depends on T014) — do this once after US1+US3's endpoints both exist, to avoid regenerating twice
- [X] T016 [P] [US1] `InvitationFormDialog.tsx` (email + optional org-name note + locale select) in `web/components/platform-admin/InvitationFormDialog.tsx` (depends on T015)
- [X] T017 [P] [US1] NL/FR/EN locale keys for the invitation-creation form, in `web/i18n/locales/*.json`

**Checkpoint**: A platform-admin can create invitations end-to-end and an email is sent; this
alone is not yet independently *useful* (the emailed link has nowhere to go until US2 exists),
but is independently *testable* per spec.md's own framing.

---

## Phase 4: User Story 2 - Prospective director completes registration (Priority: P1) 🎯 MVP (other half)

**Goal**: A prospective director can open an invitation link and complete registration on a new
public web page — closing the loop US1 opens.

**Independent Test**: Take any valid invitation (created directly via `CreatePlatformAdminInvitationCommand`
or existing test infrastructure, independent of US1's own UI), open `/register?token=...`,
submit the form, confirm the organisation is created and immediately usable.

### Tests for User Story 2

- [X] T018 [P] [US2] Component test: the registration page shows the pre-filled, non-editable email for a valid token, submits to the existing `POST /api/organisations/register`, and shows a success state on `201`, in `web/components/__tests__/RegisterPage.test.tsx` (or the project's established web test location for page-level tests)
- [X] T019 [P] [US2] Component test: the registration page shows one generic "link no longer valid" message for a `404` response (covering expired/revoked/already-used/never-existed, per FR-011 — never a reason-specific message), and a field-level error for a `422` email-mismatch response, in the same test file as T018

### Implementation for User Story 2

- [X] T020 [US2] `web/app/register/page.tsx` — public, unauthenticated page outside `(app)`/`(auth)` route groups, mirroring `web/app/enroll/[orgSlug]/[locationSlug]/page.tsx`'s own-`NextIntlClientProvider` + in-page language-toggle pattern (research.md R7); reads `token` from the query string, looks it up via `GET /api/organisations/register/{token}` (T007a) to pre-fill/lock the email and detect an invalid link before the user types anything, then calls `POST /api/organisations/register` via `publicApiClient` on submit; renders loading/invalid/form/submitted states (depends on T007a, not just the pre-existing POST endpoint as originally planned — see T007a's note)
- [X] T021 [US2] NL/FR/EN locale JSON entries for the registration page's own nested message set (mirrors the enroll page's `enMessages`/`nlMessages`/`frMessages` pattern, not the app-wide `next-intl` namespace), in `web/i18n/locales/*.json`

**Checkpoint**: US1 + US2 together deliver the feature's core MVP — a platform-admin can invite,
and a prospective director can self-register, end-to-end, with no manual/ops-assisted step.

---

## Phase 5: User Story 3 - Platform-admin tracks and manages invitation status (Priority: P2)

**Goal**: A platform-admin can list invitations with derived status, resend a Pending/Expired
one, and revoke a Pending one.

**Independent Test**: Create invitations in different states (fresh, expired via a backdated
`ExpiresAt`, accepted via completing US2's flow); confirm the list shows correct status for
each; resend and revoke a Pending one and confirm status changes.

### Tests for User Story 3

- [X] T022 [P] [US3] Integration test: `GET /api/platform-admin/invitations` returns correct derived status (Pending/Accepted/Expired/Revoked) for each case per data-model.md's derivation rules; a director without the flag gets `403`, in `backend/ChildCare.Api.Tests/PlatformAdmin/ListPlatformAdminInvitationsTests.cs`
- [X] T023 [P] [US3] Integration test: `POST /api/platform-admin/invitations/{id}/resend` creates a fresh invitation with `createdByEmail` set from the caller's claims, marks the prior Revoked with `revokedByEmail`/`revokedAt` also set from the caller's claims (FR-008 applies to resend, not only plain create/revoke), sends a new email; returns `409` if `{id}` is already Accepted; unknown id → `404`, in `backend/ChildCare.Api.Tests/PlatformAdmin/ResendPlatformAdminInvitationTests.cs`
- [X] T024 [P] [US3] Integration test: `POST /api/platform-admin/invitations/{id}/revoke` sets the revoke-attribution fields from the caller's claims (never the request body); is idempotent on an already-Revoked invitation; returns `409` if already Accepted; unknown id → `404`, in `backend/ChildCare.Api.Tests/PlatformAdmin/RevokePlatformAdminInvitationTests.cs`

### Implementation for User Story 3

- [X] T025 [US3] `ListPlatformAdminInvitationsQuery` + handler (status derivation per data-model.md, left-join against `Tenant.CreatedFromInvitationId`) + mapper, in `backend/ChildCare.Application/Invitations/ListPlatformAdminInvitationsQuery.cs` (depends on T001, T011)
- [X] T026 [US3] `ResendPlatformAdminInvitationCommand` + handler (mirrors `ResendStaffInvitationCommand`'s shape — new token/expiry, supersede-prior, send email; sets `CreatedByUserId`/`CreatedByEmail` on the new row AND `RevokedByUserId`/`Email`/`At` on the superseded row, both from the acting user passed in by the endpoint — FR-008 applies here too, per research.md R12/`/speckit-analyze` finding F2), in `backend/ChildCare.Application/Invitations/ResendPlatformAdminInvitationCommand.cs` (depends on T001, T004, T006)
- [X] T027 [US3] `RevokePlatformAdminInvitationCommand` + handler (sets `RevokedByUserId`/`Email`/`At` from the acting user passed in by the endpoint, idempotent no-op if already set, `409` result if Accepted), in `backend/ChildCare.Application/Invitations/RevokePlatformAdminInvitationCommand.cs` (depends on T001)
- [X] T028 [US3] Map `GET /api/platform-admin/invitations`, `POST .../{id}/resend`, `POST .../{id}/revoke` in `PlatformAdminInvitationEndpoints.cs`, all `.RequireAuthorization("PlatformAdminOnly")` (depends on T013, T025, T026, T027)
- [X] T029 [US3] Regenerate `web/lib/generated/api-types.ts` (depends on T028) — the single regeneration pass mentioned at T015
- [X] T030 [P] [US3] `InvitationTable.tsx` (list view: email, note, locale, status badge with semantic color+icon per design-system.md, resend/revoke row actions gated by status) in `web/components/platform-admin/InvitationTable.tsx` (depends on T029)
- [X] T031 [US3] `web/app/(app)/platform-admin/invitations/page.tsx` — gated route rendering `InvitationTable` + the `InvitationFormDialog` from T016 (depends on T016, T030)
- [X] T032 [P] [US3] NL/FR/EN locale keys for the invitations list/status/resend/revoke UI, in `web/i18n/locales/*.json`

**Checkpoint**: Invitations can be created, listed with accurate status, resent, and revoked,
end-to-end.

---

## Phase 6: User Story 4 - Platform-admin views the organisation directory (Priority: P2)

**Goal**: A platform-admin can see every organisation on the platform — name, plan, provisioning
status, KBO number, created date, registering email — read-only.

**Independent Test**: View the directory against organisations that already exist from prior
features' test/seed data; confirm no suspend/deactivate/edit action exists anywhere on the screen.

### Tests for User Story 4

- [X] T033 [P] [US4] Integration test: `GET /api/platform-admin/organisations` returns every `Tenant` with `registeredByEmail` correctly joined via `CreatedFromInvitationId` per research.md R5, ordered by `createdAt` descending; a director without the flag gets `403`, in `backend/ChildCare.Api.Tests/PlatformAdmin/ListPlatformAdminOrganisationsTests.cs`

### Implementation for User Story 4

- [X] T034 [US4] `PlatformAdminOrganisationResponse` contract per data-model.md, in `backend/ChildCare.Contracts/Responses/PlatformAdminOrganisationResponse.cs`
- [X] T035 [US4] `ListPlatformAdminOrganisationsQuery` + handler (single query, `Tenant` left-joined to `Invitation`, no per-tenant-schema fan-out per research.md R5) + mapper, in `backend/ChildCare.Application/Organisations/ListPlatformAdminOrganisationsQuery.cs` (depends on T034)
- [X] T036 [US4] `PlatformAdminOrganisationEndpoints.cs`: map `GET /api/platform-admin/organisations`, `.RequireAuthorization("PlatformAdminOnly")` — read-only, no POST/PATCH/DELETE (FR-013), in `backend/ChildCare.Api/Endpoints/PlatformAdminOrganisationEndpoints.cs` (depends on T035)
- [X] T037 [US4] Register `PlatformAdminOrganisationEndpoints` in the endpoint-mapping startup code, in `backend/ChildCare.Api/Program.cs` (depends on T036)
- [X] T038 [US4] Regenerate `web/lib/generated/api-types.ts` (depends on T037)
- [X] T039 [P] [US4] `OrganisationTable.tsx` (read-only table: name, plan, provisioning-status badge — labeled honestly per research.md R6, not as active/inactive — KBO number, created date, registered-by email) in `web/components/platform-admin/OrganisationTable.tsx` (depends on T038)
- [X] T040 [US4] `web/app/(app)/platform-admin/organisations/page.tsx` — gated route rendering `OrganisationTable` (depends on T039)
- [X] T041 [P] [US4] NL/FR/EN locale keys for the organisation directory, in `web/i18n/locales/*.json`

**Checkpoint**: A platform-admin can view every organisation on the platform, read-only.

---

## Phase 7: User Story 5 - Platform-admin navigates a unified portal shell (Priority: P3)

**Goal**: One shared platform-admin shell/nav lists Invitations, Organisations, and the existing
Vaccine Types as sibling entries.

**Independent Test**: Log in as a platform-admin, confirm the sidebar's Platform Administration
section lists all three entries; log in as a non-platform-admin director, confirm the section
is entirely absent.

### Implementation for User Story 5

(No new tests — this phase is a pure UI reorganization of already-tested screens; existing
`session.user.isPlatformAdmin` gating tests already cover the access-control behavior this phase
reuses unchanged.)

- [X] T042 [US5] `web/app/(app)/platform-admin/layout.tsx` — shared section layout rendering a small in-section nav (Invitations / Organisations / Vaccine Types) around `{children}` per research.md R10 (depends on T031, T040)
- [X] T043 [US5] Change `PLATFORM_ADMIN_NAV` in `web/components/Sidebar.tsx` from a single object to an array of three entries (Invitations, Organisations, Vaccine Types), rendered via the existing `.map(...)` pattern already used for `REAL_NAV`, still gated by `session.user.isPlatformAdmin`; update 013h's existing Sidebar test asserting the platform-admin section is hidden for `isPlatformAdmin: false` so it passes against the new array shape rather than the old single-object one (`/speckit-analyze` finding F3 — the refactor changes the exact code that test exercises) (depends on T042)
- [X] T044 [US5] Confirm `web/app/(app)/platform-admin/vaccine-types/page.tsx` renders correctly nested inside the new shared layout — no content change to that file itself (depends on T042)
- [X] T045 [P] [US5] NL/FR/EN locale keys for the new nav entries ("invitations", "organisations" — "vaccineTypes" already exists), in `web/i18n/locales/*.json`

**Checkpoint**: All three platform-admin capabilities are reachable from one consistent shell.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final end-to-end validation and design compliance across all five user stories.

- [ ] T046 [P] Run quickstart.md's full validation sequence end-to-end (invite → email → register → accepted status → directory entry → revoke → resend → non-admin denial → shared shell) manually against a locally-started API + local Postgres + web dev server
- [X] T047 [P] Design-compliance pass on `InvitationTable.tsx`/`InvitationFormDialog.tsx`/`OrganisationTable.tsx`/`web/app/register/page.tsx`/the new `platform-admin/layout.tsx` against design-system.md (spacing scale, no nested cards, shared component reuse, status-badge semantic color+icon pairing) and platform-rules.md (director-web density/keyboard-focus requirements) — no fixes needed: every spacing utility across all new/changed files (`gap-1/2/3/4`, `p-3`, `px-3/4`, `py-2/8`, `mt-1/2`, `mb-4/6/8`, `space-y-4/6`) maps to the 4/8/12/16/24/32 scale; no nested cards or gradients introduced; every table reuses the shared `Table`/`TableRow`/`TableCell` components' existing `40px`/`8px`/`12px` density unmodified (same conclusion as 013h's T051); every status badge pairs its semantic color with a distinct icon (`InvitationStatusBadge`, `OrganisationTable`'s provisioning-status badge); every interactive element is the shared `Button`/`Input`/`Badge`/`Dialog` component, which already carry `focus-visible:ring-2 focus-visible:ring-primary`; the register page's locale-toggle buttons intentionally match the pre-existing `enroll`/`sign` pages' identical unstyled-focus pattern rather than introducing a new one.
- [ ] T048 Confirm no `TenantMigrationRolloutTests`/`LegacyVaccinationMigrationTests` revert-helper update is needed — this feature's only migration is Public-schema (per data-model.md), and that recurring pattern applies only to tenant-schema migrations; run the full backend suite to verify no unrelated regression

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No-op, proceed directly to Foundational.
- **Foundational (Phase 2)**: BLOCKS all user stories — the extended `Invitation` schema, the
  locale-aware email method, and the link builder are needed by every subsequent story.
- **User Stories (Phase 3-7)**: All depend on Foundational.
  - US1 and US2 together form the feature's actual MVP (spec.md: "co-equal... an invitation
    with nowhere to go delivers no value") — US1 alone is testable but not independently
    *useful* without US2.
  - US3 depends on US1's endpoint file/contracts existing (shares
    `PlatformAdminInvitationEndpoints.cs`) but not on US1's *behavior* — could be built in
    parallel by a second engineer once T011-T013 land, at the cost of file-conflict churn.
  - US4 has no dependency on US1/US2/US3 at all beyond Foundational — could be built fully in
    parallel with any of them.
  - US5 depends on US3's and US4's pages existing (`invitations/page.tsx`,
    `organisations/page.tsx`) since it wraps them in a shared layout.
- **Polish (Phase 8)**: Depends on all five user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Foundational only.
- **User Story 2 (P1)**: Foundational only (consumes feature 001's existing, unchanged
  registration endpoint — no dependency on US1's UI, only on *some* invitation existing, which
  test infrastructure can create directly).
- **User Story 3 (P2)**: Foundational + shares files with US1 (`PlatformAdminInvitationEndpoints.cs`,
  `InvitationFormDialog.tsx`'s sibling `InvitationTable.tsx`) — land after US1 in practice.
- **User Story 4 (P2)**: Foundational only. Fully independent of US1/US2/US3.
- **User Story 5 (P3)**: Depends on US3 (`invitations/page.tsx`) and US4
  (`organisations/page.tsx`) existing to wrap.

### Parallel Opportunities

- T004/T006 (Foundational: email method signature, link builder) in parallel.
- T009/T010 (US1 tests) in parallel.
- T018/T019 (US2 tests) in parallel.
- T022/T023/T024 (US3 tests) in parallel.
- US4 (T033-T041) can proceed fully in parallel with US1/US2/US3 once Foundational is done.
- T046/T047 (Polish) in parallel.

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 Only)

1. Complete Phase 1 (no-op) + Phase 2 (Foundational).
2. Complete Phase 3 (US1: send invitation) + Phase 4 (US2: complete registration).
3. **STOP and VALIDATE**: run T009/T010/T018/T019's tests plus quickstart.md's Scenario 1
   end-to-end (invite → email → register → immediately usable account).
4. This alone closes the feature's core gap: self-service onboarding, no manual/ops-assisted
   step, per spec.md's own framing of US1+US2 as co-equal P1s.

### Incremental Delivery

1. Foundational → schema/email/link-builder ready.
2. US1 + US2 → invite + complete registration, the real MVP, independently testable together.
3. US3 → status tracking + resend/revoke, independently testable, no regression to US1/US2.
4. US4 → organisation directory, independently testable, fully parallel-buildable with US3.
5. US5 → unified shell wrapping US3's and US4's screens plus 013h's existing one.
6. Polish → full quickstart validation, design compliance, confirm no migration-revert-helper
   gap.

---

## Parallel Example: User Story 1 + User Story 4 (fully independent)

```bash
# US1 and US4 share no files and no logical dependency beyond Foundational — a second engineer
# could pick up US4 immediately after Phase 2 while US1/US2/US3 proceed:
Task: "Integration test for GET /api/platform-admin/organisations in backend/ChildCare.Api.Tests/PlatformAdmin/ListPlatformAdminOrganisationsTests.cs"
Task: "ListPlatformAdminOrganisationsQuery + handler in backend/ChildCare.Application/Organisations/ListPlatformAdminOrganisationsQuery.cs"
Task: "PlatformAdminOrganisationEndpoints.cs in backend/ChildCare.Api/Endpoints/PlatformAdminOrganisationEndpoints.cs"
```

---

## Notes

- [P] tasks = different files, no dependencies.
- [Story] label maps task to specific user story for traceability.
- Every write/read endpoint in this feature (except the registration page's existing endpoint)
  is gated by `PlatformAdminOnly` — there is no user story that can skip that dependency.
- US1 and US2 are BOTH P1 and BOTH required for a usable MVP — do not treat US1 alone as
  shippable, per spec.md's explicit "co-equal" framing.
- Commit after each task or logical group.
- Stop at any checkpoint to validate story independently.

---

## Phase 9: Convergence

- [X] T049 Add the missing `RevokedAt` check to `RegisterOrganisationCommandHandler`'s invitation-validity guard, and a regression test proving a revoked invitation cannot complete registration via a direct `POST /api/organisations/register` call (not just via the `GET` pre-check) per FR-006/FR-010 (missing) — `backend/ChildCare.Application/Organisations/RegisterOrganisationCommandHandler.cs`, `backend/ChildCare.Api.Tests/PlatformAdmin/RevokedInvitationCannotRegisterTests.cs`
