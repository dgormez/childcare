# Implementation Plan: Child File Management

**Branch**: `006-children` | **Date**: 2026-07-06 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/006-children/spec.md`

## Summary

Adds `Child` as a new tenant-domain entity — the central record for every enrolled or waitlisted child, existing independently of any contract. Directors/staff capture core profile fields, medical information, and link multiple `Contact` records (people, not accounts) via a `ChildContact` join carrying relationship/can-pickup/primary flags, so siblings share one contact record rather than duplicating it. A minimal `Group` entity (name, scoped to a `Location`) is introduced — the first feature to need one — so children can be assigned to a group/section via a dated `ChildGroupAssignment` history (assigning a new group ends the prior one). `VaccinationRecord` entries track vaccine history with a computed due-alert. Deactivation is soft-delete only, following the `IDeactivationGuard`-per-feature extension-point pattern established in features 004/005 (`IChildDeactivationGuard`, zero guards registered — feature 007 registers its own once contracts exist). Profile photos reuse feature 005's `IProfilePhotoStorage` port, generalized from a staff-only path convention to a `(category, subjectId)` shape so both staff and child photos share one signed-URL mechanism rather than two parallel ones. Backend-only, matching every prior feature's boundary.

## Technical Context

**Language/Version**: C# / .NET 10, EF Core 10 (unchanged from features 001–005)

**Primary Dependencies**: ASP.NET Core Minimal APIs + `DirectorOnly`/`StaffOrDirector` policies (feature 003) — child profile/medical/contact/group/vaccine writes are `DirectorOnly` (matches every prior tenant-admin feature); MediatR + FluentValidation (constitution Principle III); EF Core 10 + Npgsql; existing `IProfilePhotoStorage` (feature 005, generalized here — research.md R1)

**Storage**: PostgreSQL 16, schema-per-tenant (unchanged) — one new tenant-schema migration (`AddChildren`) covering `children`, `contacts`, `child_contacts`, `groups`, `child_group_assignments`, `vaccination_records`, applied the same way feature 005's `AddStaff` was (auto-applied to new schemas via `TenantProvisioningService`, rolled out to existing schemas via the `migrate-tenants` CLI)

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (constitution Principle V), extending `TestWebAppFactoryBase`; `IProfilePhotoStorage` continues to be faked in tests (no real GCS calls in CI) via the existing `FakeProfilePhotoStorage` from feature 005, updated for the generalized signature

**Target Platform**: Linux container on Cloud Run (existing `infra/gcp/` Terraform + `.github/workflows/deploy-gcp.yml`) — no new infrastructure; reuses feature 005's GCS bucket (a `category` path segment, e.g. `children/{id}/photo.jpg`, distinguishes child photos from staff photos in the same bucket)

**Project Type**: Web service (ASP.NET Core API) — backend only, no web admin UI work, same boundary as features 001–005

**Performance Goals**: No explicit throughput target beyond SC-001's task-completion-time framing (director creates a child file in under 2 minutes); child rosters are small-to-moderate per organisation (dozens to low hundreds), so no pagination is required for Phase 1

**Constraints**: Deny-by-default tenant scoping — every child endpoint goes through `TenantMiddleware`/`ICurrentTenantService` (feature 002), same as features 004/005; every endpoint requires `DirectorOnly`; no hard-delete code path (FR-012); a child file must be fully usable with zero contacts, zero group assignments, and zero contract (FR-002, edge cases); profile photos never served via a public/unsigned URL (FR-015, constitution Principle VI)

**Scale/Scope**: Same Phase 1 scale as prior features (dozens of organisations, dozens to low hundreds of children each, each with a handful of contacts/vaccine records). New entities: `Child`, `Contact`, `ChildContact` (join), `Group`, `ChildGroupAssignment`, `VaccinationRecord`. One generalized existing port (`IProfilePhotoStorage`). ~20 endpoints across children/contacts/groups/vaccinations.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation (NON-NEGOTIABLE) | ✅ Pass | Every `ChildrenEndpoints.cs`/`ContactsEndpoints.cs`/`GroupsEndpoints.cs` route is non-exempt and goes through `TenantMiddleware`/`ICurrentTenantService` (feature 002), same as features 004/005. All six new entities carry no `OrganisationId`/tenant column — schema-per-tenant is the isolation boundary (mirrors `TenantUser`/`Location`/`StaffProfile`). |
| II. Regulatory Compliance by Design | ✅ Pass | This feature stores the data (medical info, `kindcode`, vaccine records) that later regulatory features depend on (e.g. `kindcode` for Phase 3 IKT reporting) but enforces no regulatory ratio itself — no BKR/day-overlap logic lives here, consistent with "storage here, enforcement there" already established for feature 005's qualification field. |
| III. CQRS via MediatR & Thin Endpoints | ✅ Pass | Every write (create/update child, create/update/link/unlink contact, create group, assign group, record vaccination, deactivate/reactivate, request photo-upload URL) is a MediatR command with a FluentValidation validator. Reads are MediatR queries, consistent with features 004/005. Endpoint files only map HTTP ↔ MediatR. |
| IV. Internationalization First (NON-NEGOTIABLE) | ✅ Pass | All validation/error responses use new `errors.child.*`/`errors.contact.*`/`errors.group.*`/`errors.vaccination.*` keys (added to `backend/ERROR_KEYS.md` during implementation), never raw text. No email/transactional-text content exists in this feature at all (unlike 005), so there is no analogous i18n carve-out to make here. |
| V. Test with Real Infrastructure (NON-NEGOTIABLE) | ✅ Pass | TestContainers-backed integration tests extending `TestWebAppFactoryBase`. `IProfilePhotoStorage` remains the one faked external-service seam (feature 005's `FakeProfilePhotoStorage`, updated for the generalized signature) — no test hits real GCS. |
| VI. Secure Configuration & Storage | ✅ Pass | Reuses feature 005's GCS bucket/credentials wiring (no new bucket, no new IAM binding needed) — a `category` path segment (`children/...` vs `staff/...`) is the only change, and it's a plain string, not configuration. The `AddChildren` migration follows the ordinary reviewed/manual-rollout path for existing tenant schemas (no new-tenant-schema carve-out needed here — same as every feature since 002). |
| VII. Monolith-First Simplicity | ✅ Pass | No new project. `Child`/`Contact`/`ChildContact`/`Group`/`ChildGroupAssignment`/`VaccinationRecord` live in `ChildCare.Domain`; `Children`/`Contacts`/`Groups` command/query handlers in `ChildCare.Application`; `IProfilePhotoStorage`'s generalization touches `ChildCare.Application/Common` and its existing `ChildCare.Infrastructure` implementation — no new deployable, no new solution project. |

**Overall**: 7 of 7 clean passes. No Complexity Tracking entries needed.

## Project Structure

### Documentation (this feature)

```text
specs/006-children/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── children-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks — not created by this command)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   └── Entities/
│       ├── Child.cs                            #   NEW — FK to nothing but schema-scoped, no OrganisationId
│       ├── Contact.cs                           #   NEW — standalone person record, no TenantUser link
│       ├── ChildContact.cs                      #   NEW — join: ChildId, ContactId, Relationship, CanPickup, IsPrimary
│       ├── Group.cs                             #   NEW — FK to Location, minimal (name only)
│       ├── ChildGroupAssignment.cs              #   NEW — ChildId, GroupId, StartDate, EndDate?
│       └── VaccinationRecord.cs                 #   NEW — ChildId, VaccineName, DateAdministered, NextDueDate?
├── ChildCare.Domain/Enums/
│   ├── ContactRelationship.cs                   #   NEW — Mother, Father, Guardian, EmergencyContact, AuthorisedPickup
│   └── AllergySeverity.cs                       #   NEW — Mild, Moderate, Severe
├── ChildCare.Application/
│   ├── Common/
│   │   ├── IChildDeactivationGuard.cs           #   NEW — extension point for feature 007 (mirrors IStaffDeactivationGuard)
│   │   └── IProfilePhotoStorage.cs              #   MODIFIED — generalized to (category, subjectId) — research.md R1
│   ├── Children/                                 #   NEW
│   │   ├── CreateChildCommand.cs / …Validator.cs / …Handler.cs
│   │   ├── UpdateChildCommand.cs / …Validator.cs / …Handler.cs
│   │   ├── DeactivateChildCommand.cs / …Handler.cs
│   │   ├── ReactivateChildCommand.cs / …Handler.cs
│   │   ├── RequestChildPhotoUploadUrlCommand.cs / …Handler.cs
│   │   ├── ListChildrenQuery.cs / …Handler.cs
│   │   ├── GetChildByIdQuery.cs / …Handler.cs
│   │   ├── ChildResult.cs                       #   shared success/failure result shape (mirrors StaffResult)
│   │   └── ChildMapper.cs
│   ├── Contacts/                                 #   NEW
│   │   ├── CreateContactCommand.cs / …Validator.cs / …Handler.cs
│   │   ├── UpdateContactCommand.cs / …Validator.cs / …Handler.cs
│   │   ├── LinkContactToChildCommand.cs / …Validator.cs / …Handler.cs
│   │   ├── UpdateChildContactLinkCommand.cs / …Handler.cs
│   │   ├── UnlinkContactFromChildCommand.cs / …Handler.cs
│   │   ├── ListContactsQuery.cs / …Handler.cs
│   │   └── ContactResult.cs / ContactMapper.cs
│   └── Groups/                                   #   NEW
│       ├── CreateGroupCommand.cs / …Validator.cs / …Handler.cs
│       ├── AssignChildToGroupCommand.cs / …Handler.cs
│       ├── RecordVaccinationCommand.cs / …Validator.cs / …Handler.cs
│       ├── ListGroupsQuery.cs / …Handler.cs
│       ├── ListChildGroupHistoryQuery.cs / …Handler.cs
│       ├── ListChildVaccinationsQuery.cs / …Handler.cs
│       └── GroupResult.cs / GroupMapper.cs
├── ChildCare.Infrastructure/
│   ├── Persistence/
│   │   ├── TenantDbContext.cs                  #   MODIFIED — + 6 new DbSets/configuration
│   │   └── Migrations/Tenant/                  #   NEW migration — AddChildren
│   └── Storage/
│       └── GcsProfilePhotoStorage.cs           #   MODIFIED — object path now {category}/{subjectId}/photo.jpg
├── ChildCare.Contracts/
│   ├── Requests/
│   │   └── ChildRequests.cs                    #   NEW — Create/Update Child/Contact/Group/Vaccination request DTOs
│   └── Responses/
│       └── ChildResponse.cs                    #   NEW (+ ContactResponse, GroupResponse, VaccinationResponse)
├── ChildCare.Api/
│   ├── Endpoints/
│   │   ├── ChildrenEndpoints.cs                #   NEW — /api/children/*, all DirectorOnly
│   │   ├── ContactsEndpoints.cs                #   NEW — /api/contacts/*, /api/children/{id}/contacts/*
│   │   └── GroupsEndpoints.cs                  #   NEW — /api/groups/*, /api/children/{id}/groups/*, /api/children/{id}/vaccinations/*
│   └── Program.cs                              #   MODIFIED — map new endpoint groups; no new DI registration
│                                                #     beyond what 005 already registered for IProfilePhotoStorage
└── ChildCare.Api.Tests/
    ├── ChildCrudTests.cs                        #   NEW — US1 (SC-001/SC-002, FR-001–004)
    ├── ChildContactTests.cs                      #   NEW — US2 (SC-004, FR-005–007)
    ├── ChildGroupAssignmentTests.cs              #   NEW — US3 (FR-008/FR-008a)
    ├── ChildVaccinationTests.cs                  #   NEW — US4 (FR-010/FR-011)
    └── ChildDeactivationTests.cs                 #   NEW — US5 (SC-005, FR-012–014)
```

**Structure Decision**: Web-service (backend-only). No new projects — extends the existing 5-project structure the same way features 004/005 did. `IProfilePhotoStorage`'s signature generalization is a modification to an existing shared port, not a new abstraction — every caller (feature 005's staff-photo commands, and this feature's new child-photo command) is updated in the same change.

## Complexity Tracking

> Empty — no unresolved Constitution Check violations remain for this plan.
