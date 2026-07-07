---

description: "Task list for feature implementation"
---

# Tasks: Child File Management

**Input**: Design documents from `/specs/006-children/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md (all present)

**Tests**: Included. Constitution Principle V (NON-NEGOTIABLE) requires integration tests against TestContainers-provisioned PostgreSQL. Scope follows the project convention (global CLAUDE.md): happy path + key negative flows, not exhaustive per-path coverage.

**Organization**: Tasks are grouped by user story (spec.md: US1 P1 create profile + medical, US2 P1 contacts, US3 P2 group assignment, US4 P2 vaccinations, US5 P3 deactivate/reactivate). `Child`/`Contact`/`ChildContact`/`Group`/`ChildGroupAssignment`/`VaccinationRecord`, their EF configuration/migration, the generalized `IProfilePhotoStorage` port, the new `IChildDeactivationGuard` port, the shared result/response shapes, and the endpoint groups' policy wiring are shared prerequisites every story depends on, so they live in Foundational.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1-US5) — omitted for Setup/Foundational/Polish tasks
- File paths are exact and repository-root-relative

## Path Conventions

Backend-only feature (no frontend/mobile changes). All paths are under `backend/`.

---

## Phase 1: Setup

**Purpose**: Confirm baseline before touching a previously-shipped shared port.

- [X] T001 Confirm `dotnet build backend/ChildCare.sln` succeeds on this branch before starting

**Checkpoint**: Baseline confirmed.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The six new entities, their EF Core configuration and migration, the generalized `IProfilePhotoStorage` port (and its feature-005 call-site updates), the new `IChildDeactivationGuard` port, the shared result/response shapes, and the three endpoint groups. Every user story's commands, queries, and tests depend on this being complete.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Entities

- [X] T002 [P] Create `Gender` enum (`Male`, `Female`, `Other`) in `backend/ChildCare.Domain/Enums/Gender.cs` (data-model.md)
- [X] T003 [P] Create `AllergySeverity` enum (`Mild`, `Moderate`, `Severe`) in `backend/ChildCare.Domain/Enums/AllergySeverity.cs` (data-model.md, research.md R5)
- [X] T004 [P] Create `ContactRelationship` enum (`Mother`, `Father`, `Guardian`, `EmergencyContact`, `AuthorisedPickup`) in `backend/ChildCare.Domain/Enums/ContactRelationship.cs` (data-model.md)
- [X] T005 Create `Child` entity in `backend/ChildCare.Domain/Entities/Child.cs` per data-model.md's full field list — no `OrganisationId`/contract FK (FR-002) — depends on T002, T003
- [X] T006 [P] Create `Contact` entity in `backend/ChildCare.Domain/Entities/Contact.cs`: `Id`, `FirstName`, `LastName`, `Phone`, `Email?`, `Locale`, `CreatedAt`, `UpdatedAt` — no `TenantUserId` link (spec.md Assumptions)
- [X] T007 Create `ChildContact` join entity in `backend/ChildCare.Domain/Entities/ChildContact.cs`: `ChildId`, `ContactId`, `Relationship`, `CanPickup`, `IsPrimary`, `CreatedAt` — composite key `(ChildId, ContactId)`, no surrogate `Id` (data-model.md, research.md R3, revised during `/speckit-analyze` to avoid an ambiguous route) — depends on T004, T005, T006
- [X] T008 [P] Create `Group` entity in `backend/ChildCare.Domain/Entities/Group.cs`: `Id`, `LocationId`, `Name`, `CreatedAt` (data-model.md, research.md R2)
- [X] T009 Create `ChildGroupAssignment` entity in `backend/ChildCare.Domain/Entities/ChildGroupAssignment.cs`: `Id`, `ChildId`, `GroupId`, `StartDate`, `EndDate?`, `CreatedAt` — depends on T005, T008
- [X] T010 Create `VaccinationRecord` entity in `backend/ChildCare.Domain/Entities/VaccinationRecord.cs`: `Id`, `ChildId`, `VaccineName`, `DateAdministered`, `NextDueDate?`, `CreatedAt` — depends on T005
- [X] T011 Add `DbSet<Child> Children`, `DbSet<Contact> Contacts`, `DbSet<ChildContact> ChildContacts`, `DbSet<Group> Groups`, `DbSet<ChildGroupAssignment> ChildGroupAssignments`, `DbSet<VaccinationRecord> VaccinationRecords` to `ITenantDbContext` in `backend/ChildCare.Application/Common/ITenantDbContext.cs` — depends on T005–T010
- [X] T012 Add the six `DbSet`s and their `OnModelCreating` configuration to `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs` per data-model.md's exact EF configuration block (max lengths, `ChildContact` composite PK `(ChildId, ContactId)`, FKs to `Location`/`Child`/`Contact`/`Group`) — depends on T011
- [X] T013 Generate the `TenantDbContext` migration (`dotnet ef migrations add AddChildren --context TenantDbContext --project backend/ChildCare.Infrastructure --startup-project backend/ChildCare.Api`) into `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/` — depends on T012

### `IProfilePhotoStorage` generalization (research.md R1)

- [X] T014 Change `IProfilePhotoStorage` in `backend/ChildCare.Application/Common/IProfilePhotoStorage.cs`: `CreateUploadUrlAsync(Guid staffProfileId, ...)` → `CreateUploadUrlAsync(string category, Guid subjectId, ...)`
- [X] T015 Update `GcsProfilePhotoStorage` in `backend/ChildCare.Infrastructure/Storage/GcsProfilePhotoStorage.cs`: object path becomes `{category}/{subjectId}/photo.jpg` — depends on T014
- [X] T016 Update every feature-005 call site to pass `"staff"` explicitly: `RequestPhotoUploadUrlCommandHandler`, `ListStaffQuery`, `GetStaffByIdQuery` (all in `backend/ChildCare.Application/Staff/`) — depends on T014
- [X] T017 Update `FakeProfilePhotoStorage` in `backend/ChildCare.Api.Tests/FakeProfilePhotoStorage.cs` for the new signature, and confirm the existing feature-005 staff-photo tests (`StaffProfileUpdateTests.cs`) still pass unchanged in behavior — depends on T014

### Shared Application/Contracts shapes

- [X] T018 [P] Create `ChildResult` shared success/failure result type in `backend/ChildCare.Application/Children/ChildResult.cs`, mirroring `StaffResult` (feature 005) — failure cases: `NotFound`, `HasActiveDependents`
- [X] T019 [P] Create `ContactResult` in `backend/ChildCare.Application/Contacts/ContactResult.cs` — failure cases: `NotFound`, `ChildNotFound`, `LinkAlreadyExists`
- [X] T020 [P] Create `GroupResult` in `backend/ChildCare.Application/Groups/GroupResult.cs` — failure cases: `NotFound`, `ChildNotFound`, `LocationNotFound`, `OutOfChronologicalOrder` (`/speckit-checklist` CHK004)
- [X] T021 [P] Create `ChildResponse` in `backend/ChildCare.Contracts/Responses/ChildResponse.cs` (data-model.md full field list)
- [X] T022 [P] Create `ContactResponse`, `ChildContactResponse` in `backend/ChildCare.Contracts/Responses/ContactResponse.cs`
- [X] T023 [P] Create `GroupResponse`, `ChildGroupAssignmentResponse`, `VaccinationResponse` in `backend/ChildCare.Contracts/Responses/GroupResponse.cs`

### New port

- [X] T024 [P] Create `IChildDeactivationGuard` in `backend/ChildCare.Application/Common/IChildDeactivationGuard.cs`: `Task<bool> HasActiveDependentsAsync(Guid childId, ITenantDbContext db, CancellationToken ct)` (research.md R4, data-model.md) — no implementation registered by this feature

### Endpoint group wiring

- [X] T025 [P] Create `backend/ChildCare.Api/Endpoints/ChildrenEndpoints.cs` with `MapChildrenEndpoints(this WebApplication app)`: `app.MapGroup("/api/children").WithTags("Children").RequireAuthorization("DirectorOnly")` — empty route bodies for now — depends on T005
- [X] T026 [P] Create `backend/ChildCare.Api/Endpoints/ContactsEndpoints.cs` with `MapContactsEndpoints(this WebApplication app)`: `/api/contacts` and `/api/children/{id}/contacts` groups, both `DirectorOnly` — empty route bodies for now — depends on T006
- [X] T027 [P] Create `backend/ChildCare.Api/Endpoints/GroupsEndpoints.cs` with `MapGroupsEndpoints(this WebApplication app)`: `/api/groups`, `/api/children/{id}/groups`, `/api/children/{id}/vaccinations` groups, all `DirectorOnly` — empty route bodies for now — depends on T008, T010
- [X] T028 Register `app.MapChildrenEndpoints(); app.MapContactsEndpoints(); app.MapGroupsEndpoints();` in `backend/ChildCare.Api/Program.cs` alongside the existing `app.MapStaffEndpoints();` call — depends on T025, T026, T027
- [X] T029 [P] Add `errors.child.not_found` (404), `errors.child.has_active_dependents` (409), `errors.contact.not_found` (404), `errors.contact.link_already_exists` (409), `errors.group.not_found` (404), `errors.group.out_of_chronological_order` (422, `/speckit-checklist` CHK004), plus field-required/too-long error keys for child/contact/group/vaccination — including `errors.child.date_of_birth_in_future` and `errors.vaccination.date_administered_in_future` (`/speckit-checklist` CHK001/CHK002) — (all 422 unless noted, per contracts/children-api.md) to `backend/ERROR_KEYS.md` under a new "Child File Management (feature `006-children`)" section

**Checkpoint**: All six entities exist end-to-end (entities → EF config → migration), `IProfilePhotoStorage` is generalized with feature 005 unaffected, `IChildDeactivationGuard` is defined, all three endpoint groups are registered and policy-protected, and every command/query has the shared result/response shapes to return. User story implementation can now begin.

---

## Phase 3: User Story 1 - Director Creates a Child File with Medical Information (Priority: P1) 🎯 MVP

**Goal**: A director creates a child file (core profile + optional medical info) that exists and is fully usable independently of any contract.

**Independent Test**: Create a child with core fields only, then a second with full medical info, confirm both appear in the list with no contract required (quickstart.md Scenario 1).

### Tests for User Story 1

- [X] T030 [P] [US1] Integration test: `POST /api/children` with `firstName`/`lastName`/`dateOfBirth` only → `201`, appears in `GET /api/children` — in `backend/ChildCare.Api.Tests/ChildCrudTests.cs` (FR-001, FR-002, quickstart.md Scenario 1 step 1) — depends on T028
- [X] T031 [P] [US1] Integration test: `POST /api/children` including all medical fields → `201`, all fields retrievable via `GET /api/children/{id}` — same file (FR-003, quickstart.md Scenario 1 step 2) — depends on T028
- [X] T032 [P] [US1] Integration test: `POST /api/children` missing `firstName`/`lastName`/`dateOfBirth` → `422 errors.validation` with the corresponding field error — same file (FR-001) — depends on T028, T029
- [X] T033 [P] [US1] Integration test: a child created in Org A is invisible to Org B — `GET /api/children/{orgAChildId}` as Org B's director → `404 errors.child.not_found`; `GET /api/children` as Org B never includes it — same file (constitution Principle I, FR-017, quickstart.md Scenario 6) — depends on T028
- [X] T034 [P] [US1] Integration test: a Staff-role and a Parent-role access token each receive `403` against `GET /api/children`, `GET /api/children/{id}`, `POST /api/children` — same file (constitution Principle II's authorization posture, mirrors feature 005's `AuthRolePolicyTests` seeding pattern) — depends on T028

### Implementation for User Story 1

- [X] T035 [P] [US1] Create `CreateChildRequest`, `UpdateChildRequest` in `backend/ChildCare.Contracts/Requests/ChildRequests.cs` (data-model.md)
- [X] T036 [P] [US1] Create `CreateChildCommand` + `CreateChildCommandValidator` in `backend/ChildCare.Application/Children/CreateChildCommand.cs` / `CreateChildCommandValidator.cs`: required `FirstName`/`LastName`/`DateOfBirth` (max lengths per data-model.md, `DateOfBirth` must not be in the future — `/speckit-checklist` CHK001), every optional field respects its max length — every `.WithMessage(...)` MUST use the corresponding `errors.child.*` key from T029, never a raw English string (constitution Principle IV) — depends on T035
- [X] T037 [US1] Create `CreateChildCommandHandler` in `backend/ChildCare.Application/Children/CreateChildCommandHandler.cs` — depends on T036, T018
- [X] T038 [P] [US1] Create `UpdateChildCommand` + `UpdateChildCommandValidator` in `backend/ChildCare.Application/Children/UpdateChildCommand.cs` / `Validator.cs`, reusing the same length/format rules as `CreateChildCommandValidator` (T036) — depends on T035
- [X] T039 [US1] Create `UpdateChildCommandHandler` in `backend/ChildCare.Application/Children/UpdateChildCommandHandler.cs` — depends on T038, T018
- [X] T040 [P] [US1] Create `ListChildrenQuery` (with `bool IncludeDeactivated = false`) + `ListChildrenQueryHandler` in `backend/ChildCare.Application/Children/ListChildrenQuery.cs`: filter `DeactivatedAt == null` unless `IncludeDeactivated`; call `IProfilePhotoStorage.CreateDownloadUrlAsync` per row for `PhotoDownloadUrl` — depends on T018
- [X] T041 [P] [US1] Create `GetChildByIdQuery` + `GetChildByIdQueryHandler` in `backend/ChildCare.Application/Children/GetChildByIdQuery.cs` — same photo-URL inlining as T040 — depends on T018
- [X] T042 [US1] Create `ChildMapper` in `backend/ChildCare.Application/Children/ChildMapper.cs` mapping `Child` → `ChildResponse` — depends on T021
- [X] T043 [US1] Implement `GET /api/children` (with `includeDeactivated` query param), `GET /api/children/{id}`, `POST /api/children`, `PUT /api/children/{id}` in `backend/ChildCare.Api/Endpoints/ChildrenEndpoints.cs`, mapping `ChildResult`/query results to `ChildResponse` and the `200`/`201`/`404`/`422` responses from contracts/children-api.md — depends on T037, T039, T040, T041, T042

**Checkpoint**: US1 fully functional and independently testable — a director can create and view a child file with or without medical information, non-Director roles are rejected, and tenant isolation holds.

---

## Phase 4: User Story 2 - Director Manages a Child's Contacts (Priority: P1)

**Goal**: A director links one or more contacts to a child, with siblings sharing a single underlying contact record.

**Independent Test**: Create a contact, link it to two sibling children, update it once, confirm both children reflect the update (quickstart.md Scenario 2).

### Tests for User Story 2

- [X] T044 [P] [US2] Integration test: `POST /api/contacts` → `201`; `POST /api/children/{id}/contacts` with that `contactId` → `201`, appears on `GET /api/children/{id}` — in `backend/ChildCare.Api.Tests/ChildContactTests.cs` (FR-005, quickstart.md Scenario 2 steps 1–2) — depends on T028
- [X] T045 [P] [US2] Integration test: link the same `contactId` to a second (sibling) child → `201`, no second `Contact` row created (assert via `ITenantDbContextResolver` row count) — same file (FR-006, SC-004, quickstart.md Scenario 2 step 3) — depends on T028
- [X] T046 [P] [US2] Integration test: `PUT /api/contacts/{id}` changing phone → `200`; both linked children's `GET` responses reflect the new phone — same file (FR-006, quickstart.md Scenario 2 step 4) — depends on T028
- [X] T047 [P] [US2] Integration test: the first-ever contact link for a child is forced `isPrimary: true` regardless of the request value — same file (FR-007) — depends on T028
- [X] T048 [P] [US2] Integration test: designating a second contact as primary clears the first without deleting its `ChildContact` row (assert the row still exists via direct DB read, `isPrimary: false`) — same file (FR-007, Edge Cases) — depends on T028
- [X] T049 [P] [US2] Integration test: `DELETE /api/children/{childId}/contacts/{contactId}` removes the link but the `Contact` row and its link to any other child remain intact — same file (data-model.md) — depends on T028
- [X] T050 [P] [US2] Integration test: a child with zero contacts → `GET /api/children/{id}` shows an empty contacts-related response, no error — same file (Edge Cases) — depends on T028

### Implementation for User Story 2

- [X] T051 [P] [US2] Create `CreateContactRequest`, `UpdateContactRequest`, `LinkContactToChildRequest` in `backend/ChildCare.Contracts/Requests/ChildRequests.cs`
- [X] T052 [P] [US2] Create `CreateContactCommand` + `CreateContactCommandValidator` + `CreateContactCommandHandler` in `backend/ChildCare.Application/Contacts/CreateContactCommand.cs` (required `FirstName`/`LastName`/`Phone`/`Locale`, valid `Email` format when present) — depends on T051, T019
- [X] T053 [P] [US2] Create `UpdateContactCommand` + `UpdateContactCommandValidator` + `UpdateContactCommandHandler` in `backend/ChildCare.Application/Contacts/UpdateContactCommand.cs` — depends on T051, T019
- [X] T054 [US2] Create `LinkContactToChildCommand` + `LinkContactToChildCommandValidator` + `LinkContactToChildCommandHandler` in `backend/ChildCare.Application/Contacts/LinkContactToChildCommand.cs`: verify `Child`/`Contact` exist, reject on `(ChildId, ContactId)` duplicate (`ContactResult.LinkAlreadyExists` — use `PUT` to change an existing link's relationship instead), force `IsPrimary = true` when this is the child's first link regardless of the request (FR-007, research.md R3) — depends on T051, T019
- [X] T055 [US2] Create `UpdateChildContactLinkCommand` + `UpdateChildContactLinkCommandHandler` in `backend/ChildCare.Application/Contacts/UpdateChildContactLinkCommand.cs`: setting `IsPrimary = true` clears (not deletes) the child's previous primary link in the same transaction — depends on T019
- [X] T056 [US2] Create `UnlinkContactFromChildCommand` + `UnlinkContactFromChildCommandHandler` in `backend/ChildCare.Application/Contacts/UnlinkContactFromChildCommand.cs`: deletes only the `ChildContact` row, idempotent if not linked; if the removed link was the child's primary and at least one other `ChildContact` link remains for that child, promote the most-recently-linked remaining one to primary in the same transaction (FR-007, `/speckit-checklist` CHK005) — depends on T019
- [X] T057 [P] [US2] Create `ListContactsQuery` + `ListContactsQueryHandler` in `backend/ChildCare.Application/Contacts/ListContactsQuery.cs` (tenant-wide, research.md R6) — depends on T019
- [X] T058 [US2] Create `ContactMapper` in `backend/ChildCare.Application/Contacts/ContactMapper.cs` mapping `Contact`/`ChildContact` → `ContactResponse`/`ChildContactResponse` — depends on T022
- [X] T059 [US2] Implement `GET /api/contacts`, `POST /api/contacts`, `PUT /api/contacts/{id}`, `POST /api/children/{childId}/contacts`, `PUT /api/children/{childId}/contacts/{contactId}`, `DELETE /api/children/{childId}/contacts/{contactId}` in `backend/ChildCare.Api/Endpoints/ContactsEndpoints.cs` — depends on T052, T053, T054, T055, T056, T057, T058

**Checkpoint**: US1 and US2 both work independently — contacts can be created, linked, shared across siblings, and updated without touching medical info or group/vaccine data.

---

## Phase 5: User Story 3 - Director Assigns a Child to a Group (Priority: P2)

**Goal**: A director creates groups and assigns children to them over time, with a full non-overwritten history.

**Independent Test**: Create a group, assign a child, create a second group, reassign the child, confirm both assignments appear with correct non-overlapping date ranges (quickstart.md Scenario 3).

### Tests for User Story 3

- [X] T060 [P] [US3] Integration test: `POST /api/groups` → `201`; `POST /api/children/{id}/groups` with that `groupId`/`startDate` → `201`, appears in `GET /api/children/{id}/groups` — in `backend/ChildCare.Api.Tests/ChildGroupAssignmentTests.cs` (FR-008, FR-008a, quickstart.md Scenario 3 steps 1–2) — depends on T028
- [X] T061 [P] [US3] Integration test: assign the child to a second group with a later `startDate` → the first assignment's `endDate` is automatically set to the day before, both remain in the history — same file (FR-008a, quickstart.md Scenario 3 steps 3–4) — depends on T028
- [X] T062 [P] [US3] Integration test: a child with zero group assignments → `GET /api/children/{id}/groups` returns an empty array, no error — same file (Edge Cases) — depends on T028
- [X] T063 [P] [US3] Integration test: `POST /api/groups` with a `locationId` from a different organisation's tenant → `404 errors.location.not_found` — same file (constitution Principle I) — depends on T028

### Implementation for User Story 3

- [X] T064 [P] [US3] Create `CreateGroupRequest`, `AssignChildToGroupRequest` in `backend/ChildCare.Contracts/Requests/ChildRequests.cs`
- [X] T065 [US3] Create `CreateGroupCommand` + `CreateGroupCommandValidator` + `CreateGroupCommandHandler` in `backend/ChildCare.Application/Groups/CreateGroupCommand.cs`: verify `LocationId` exists **and is active** (`DeactivatedAt == null`) in this tenant, else `GroupResult.LocationNotFound` (`/speckit-checklist` CHK003 — a group cannot be newly created against an already-deactivated location) — depends on T064, T020
- [X] T066 [US3] Create `AssignChildToGroupCommand` + `AssignChildToGroupCommandValidator` + `AssignChildToGroupCommandHandler` in `backend/ChildCare.Application/Groups/AssignChildToGroupCommand.cs`: verify `Child`/`Group` exist; reject with a new `GroupResult.OutOfChronologicalOrder` failure if the child has a currently-open assignment (`EndDate == null`) whose `StartDate` is later than or equal to the new request's `StartDate` (`/speckit-checklist` CHK004); otherwise close out that open assignment by setting `EndDate = StartDate.AddDays(-1)` and insert the new row — depends on T064, T020
- [X] T067 [P] [US3] Create `ListGroupsQuery` + `ListGroupsQueryHandler` (optional `LocationId` filter) in `backend/ChildCare.Application/Groups/ListGroupsQuery.cs` — depends on T020
- [X] T068 [P] [US3] Create `ListChildGroupHistoryQuery` + `ListChildGroupHistoryQueryHandler` in `backend/ChildCare.Application/Groups/ListChildGroupHistoryQuery.cs` (ordered most-recent-first) — depends on T020
- [X] T069 [US3] Create `GroupMapper` in `backend/ChildCare.Application/Groups/GroupMapper.cs` mapping `Group`/`ChildGroupAssignment` → `GroupResponse`/`ChildGroupAssignmentResponse` — depends on T023
- [X] T070 [US3] Implement `GET /api/groups`, `POST /api/groups`, `GET /api/children/{childId}/groups`, `POST /api/children/{childId}/groups` in `backend/ChildCare.Api/Endpoints/GroupsEndpoints.cs`, mapping `GroupResult.OutOfChronologicalOrder` to `422 errors.group.out_of_chronological_order` — depends on T065, T066, T067, T068, T069

**Checkpoint**: US1, US2, and US3 all work independently — groups can be created and children assigned without touching contacts or vaccination data.

---

## Phase 6: User Story 4 - Record and Track Vaccine/Health Records (Priority: P2)

**Goal**: A director or caregiver records vaccine entries per child; a computed due flag surfaces when a next-due date has arrived.

**Independent Test**: Record a vaccine with a future next-due date (not due), then one with a past next-due date (due) (quickstart.md Scenario 4).

### Tests for User Story 4

- [X] T071 [P] [US4] Integration test: `POST /api/children/{id}/vaccinations` with a future `nextDueDate` → `201`, `GET /api/children/{id}/vaccinations` shows `isDue: false` — in `backend/ChildCare.Api.Tests/ChildVaccinationTests.cs` (FR-010, quickstart.md Scenario 4 step 1) — depends on T028
- [X] T072 [P] [US4] Integration test: record a vaccine with a past `nextDueDate` → `isDue: true` — same file (FR-011, quickstart.md Scenario 4 step 2) — depends on T028
- [X] T073 [P] [US4] Integration test: record a vaccine with no `nextDueDate` → `isDue: false` always, never flagged — same file (FR-011) — depends on T028

### Implementation for User Story 4

- [X] T074 [P] [US4] Create `RecordVaccinationRequest` in `backend/ChildCare.Contracts/Requests/ChildRequests.cs`
- [X] T075 [US4] Create `RecordVaccinationCommand` + `RecordVaccinationCommandValidator` + `RecordVaccinationCommandHandler` in `backend/ChildCare.Application/Groups/RecordVaccinationCommand.cs`: required `VaccineName`/`DateAdministered`, `DateAdministered` must not be in the future (`/speckit-checklist` CHK002) — depends on T074, T020
- [X] T076 [US4] Create `ListChildVaccinationsQuery` + `ListChildVaccinationsQueryHandler` in `backend/ChildCare.Application/Groups/ListChildVaccinationsQuery.cs`: computes `IsDue = NextDueDate.HasValue && NextDueDate <= today` at query time (FR-011) — depends on T020
- [X] T077 [US4] Implement `GET /api/children/{childId}/vaccinations`, `POST /api/children/{childId}/vaccinations` in `backend/ChildCare.Api/Endpoints/GroupsEndpoints.cs` — depends on T075, T076

**Checkpoint**: US1–US4 all work independently.

---

## Phase 7: User Story 5 - Director Deactivates and Reactivates a Child (Priority: P3)

**Goal**: A director soft-deletes a child who has left (blocking active listings, preserving all history) and can reactivate them later.

**Independent Test**: Deactivate a child with no dependents, confirm they disappear from the active list while medical/contact/group/vaccine data remain retrievable, then reactivate (quickstart.md Scenario 5).

### Tests for User Story 5

- [X] T078 [P] [US5] Integration test: `POST /api/children/{id}/deactivate` on a child with no dependents → `200`, `deactivatedAt` set; default `GET /api/children` excludes it; `GET /api/children?includeDeactivated=true` includes it — in `backend/ChildCare.Api.Tests/ChildDeactivationTests.cs` (FR-012, quickstart.md Scenario 5) — depends on T043
- [X] T079 [P] [US5] Integration test: after deactivation, `GET /api/children/{id}` still returns full medical info, contacts, group history, and vaccination history — same file (FR-012, SC-005, quickstart.md Scenario 5 step 3) — depends on T078
- [X] T080 [P] [US5] Integration test: `POST /api/children/{id}/reactivate` → `200`, `deactivatedAt` cleared; child reappears in the default list — same file (FR-014) — depends on T078
- [X] T081 [P] [US5] Integration test: deactivating/reactivating an already-deactivated/already-active child is idempotent (`200`, no state change on the second call) — same file — depends on T078

### Implementation for User Story 5

- [X] T082 [US5] Create `DeactivateChildCommand` + `DeactivateChildCommandHandler` and `ReactivateChildCommand` + `ReactivateChildCommandHandler` in `backend/ChildCare.Application/Children/DeactivateChildCommand.cs` / `ReactivateChildCommand.cs`: deactivate resolves `IEnumerable<IChildDeactivationGuard>` from DI, fails with `ChildResult.HasActiveDependents` if any returns `true` (FR-013), else sets `DeactivatedAt = DateTime.UtcNow` (idempotent); reactivate sets `DeactivatedAt = null` (idempotent) — depends on T024, T018
- [X] T083 [US5] Implement `POST /api/children/{id}/deactivate` and `POST /api/children/{id}/reactivate` in `backend/ChildCare.Api/Endpoints/ChildrenEndpoints.cs`, mapping `ChildResult.HasActiveDependents` to `409 errors.child.has_active_dependents` — depends on T082

**Checkpoint**: All five user stories are independently functional.

---

## Phase 8: Photo Upload (cross-cutting, depends on US1 + Foundational photo-storage generalization)

**Purpose**: Child profile photos, reusing the generalized `IProfilePhotoStorage` port (T014–T017). Kept as its own small phase since it depends on both Foundational and US1's `ChildrenEndpoints.cs` skeleton existing.

- [X] T084 [P] Integration test: `POST /api/children/{id}/photo/upload-url` → `200`, response includes `uploadUrl`/`objectPath` with an object path prefixed `children/` (`IProfilePhotoStorage` faked per constitution Principle V) — in `backend/ChildCare.Api.Tests/ChildCrudTests.cs` (research.md R1) — depends on T043, T017
- [X] T085 [P] Integration test: after requesting an upload URL, `GET /api/children/{id}` returns a non-null `photoDownloadUrl` — same file — depends on T084
- [X] T086 Create `RequestChildPhotoUploadUrlCommand` + `RequestChildPhotoUploadUrlCommandHandler` in `backend/ChildCare.Application/Children/RequestChildPhotoUploadUrlCommand.cs`: calls `IProfilePhotoStorage.CreateUploadUrlAsync("children", childId, ct)`, persists `ProfilePhotoObjectPath` immediately (mirrors feature 005's `RequestPhotoUploadUrlCommand`) — depends on T014, T018
- [X] T087 Implement `POST /api/children/{id}/photo/upload-url` in `backend/ChildCare.Api/Endpoints/ChildrenEndpoints.cs` — depends on T086

---

## Phase 9: Requirements-Quality Follow-ups

**Purpose**: Per-loop policy, every finding from `/speckit-checklist` (`checklists/requirements-quality.md`, 19 items) must actually be fixed and tested, not just logged as advisory. The spec/contract wording fixes are already in spec.md/contracts/children-api.md and folded into the amended task descriptions above (T036, T056, T065, T066, T075) — this phase adds the tests those fixes need.

- [X] T088 [P] Integration test: `POST /api/children` with a future `dateOfBirth` → `422 errors.child.date_of_birth_in_future` — in `backend/ChildCare.Api.Tests/ChildCrudTests.cs` (FR-001, CHK001) — depends on T043
- [X] T089 [P] Integration test: `POST /api/children/{id}/vaccinations` with a future `dateAdministered` → `422 errors.vaccination.date_administered_in_future` — in `backend/ChildCare.Api.Tests/ChildVaccinationTests.cs` (FR-010, CHK002) — depends on T077
- [X] T090 [P] Integration test: `POST /api/groups` with a `locationId` that exists but is deactivated → `404 errors.location.not_found` — in `backend/ChildCare.Api.Tests/ChildGroupAssignmentTests.cs` (FR-008, CHK003) — depends on T070
- [X] T091 [P] Integration test: `POST /api/children/{childId}/groups` with a `startDate` on or before the currently-open assignment's `startDate` → `422 errors.group.out_of_chronological_order` — same file (FR-008a, CHK004) — depends on T070
- [X] T092 [P] Integration test: unlinking a child's primary contact while another contact remains linked → the remaining (most-recently-linked) contact is automatically promoted to primary — in `backend/ChildCare.Api.Tests/ChildContactTests.cs` (FR-007, CHK005) — depends on T059
- [X] T093 [P] Integration test: unlinking a child's only (primary) contact → `GET /api/children/{id}` shows an empty contacts-related response, no error, and no promotion is attempted (nothing to promote) — same file (FR-007, CHK005/CHK012) — depends on T059

**Checkpoint**: Every `/speckit-checklist` finding for this feature has a corresponding fix and test, not just a note.

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Final validation across all stories.

- [X] T094 [P] Run `dotnet ef migrations script --context TenantDbContext --project backend/ChildCare.Infrastructure --startup-project backend/ChildCare.Api` and review the generated SQL for the `AddChildren` migration (constitution Principle VI — rollout to existing tenant schemas uses the existing `migrate-tenants` CLI, unchanged mechanism from feature 002)
- [X] T095 Run the full `dotnet test backend/ChildCare.sln` suite and confirm no regressions in pre-existing tests (features 001–005) — extend `TenantMigrationRolloutTests`' revert-simulation to also drop `child_contacts`/`child_group_assignments`/`vaccination_records`/`children`/`contacts`/`groups` (FK-dependency order) and `AddChildren`'s migration history row (same class of gap features 004/005 already hit)
- [X] T096 Walk through every scenario in `quickstart.md` manually (or via the automated tests already covering them) and confirm all six pass end-to-end

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phase 3–7)**: All depend on Foundational phase completion
  - US1 has no dependency on other stories
  - US2 depends on US1's `Child` entity existing (Foundational) but not on US1's endpoints — could run in parallel with US1 by a different contributor, though in practice a child needs to exist to exercise contact-linking tests meaningfully
  - US3 depends on Foundational only for its own commands, but needs a child to assign, so in practice follows US1
  - US4 depends on Foundational only, but needs a child to record a vaccine against, so in practice follows US1
  - US5 depends on US1's `CreateChildCommand`/`ChildrenEndpoints.cs` existing (T043) since it needs a real child to deactivate
- **Photo Upload (Phase 8)**: Depends on Foundational's `IProfilePhotoStorage` generalization (T014–T017) and US1's `ChildrenEndpoints.cs` skeleton (T043)
- **Requirements-Quality Follow-ups (Phase 9)**: T088/T089 depend on US1/US4 (T043, T077); T090/T091 depend on US3 (T070); T092/T093 depend on US2 (T059) — can run immediately after those stories, in parallel with Phase 10
- **Polish (Phase 10)**: Depends on all five user stories + Photo Upload + Requirements-Quality Follow-ups being complete

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Entity/config before commands/queries
- Commands/queries before endpoints
- Story complete before moving to next priority

### Parallel Opportunities

- All Foundational tasks marked [P] can run in parallel (T002, T003, T004, T006, T008, T018, T019, T020, T021, T022, T023, T024, T025, T026, T027, T029)
- Once Foundational completes, US1's tests (T030–T034) can run in parallel; command/query scaffolding (T035, T036, T038, T040, T041) can run in parallel before their handlers
- US2's five commands (T052, T053, T054, T055, T056) touch different files and can be built in parallel once Foundational + US1 exist
- US3/US4's commands can each be built in parallel with US2's once Foundational + US1 exist
- Different user stories can be worked on in parallel by different team members once Foundational is done, keeping in mind the practical sequencing notes above

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "Integration test: create child with core fields only in backend/ChildCare.Api.Tests/ChildCrudTests.cs"
Task: "Integration test: create child with full medical info in backend/ChildCare.Api.Tests/ChildCrudTests.cs"
Task: "Integration test: missing required fields rejected in backend/ChildCare.Api.Tests/ChildCrudTests.cs"
Task: "Integration test: tenant isolation in backend/ChildCare.Api.Tests/ChildCrudTests.cs"
Task: "Integration test: Staff/Parent roles get 403 in backend/ChildCare.Api.Tests/ChildCrudTests.cs"

# Launch command/query scaffolding for User Story 1 together:
Task: "Create CreateChildCommand + Validator in backend/ChildCare.Application/Children/CreateChildCommand.cs"
Task: "Create UpdateChildCommand + Validator in backend/ChildCare.Application/Children/UpdateChildCommand.cs"
Task: "Create ListChildrenQuery + Handler in backend/ChildCare.Application/Children/ListChildrenQuery.cs"
Task: "Create GetChildByIdQuery + Handler in backend/ChildCare.Application/Children/GetChildByIdQuery.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently (quickstart.md Scenario 1, 6)
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Add User Story 3 → Test independently → Deploy/Demo
5. Add User Story 4 → Test independently → Deploy/Demo
6. Add User Story 5 → Test independently → Deploy/Demo
7. Add Photo Upload → Test independently → Deploy/Demo
8. Each story adds value without breaking previous stories

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- No task in this feature touches web/mobile — backend only (plan.md Technical Context)
- `IProfilePhotoStorage` is faked in all tests — no test hits real GCS (constitution Principle V's InMemory-vs-TestContainers concern is about database behavior; this is an external-service seam, faked the same way Google/Apple OAuth validation and feature 005's staff photos already are)
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
