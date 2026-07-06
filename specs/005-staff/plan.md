# Implementation Plan: Staff Management

**Branch**: `005-staff` | **Date**: 2026-07-06 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/005-staff/spec.md`

## Summary

Adds `StaffProfile` as a new tenant-domain entity linked one-to-one with the existing `TenantUser` (feature 003), attachable to either a `Staff`-role or a `Director`-role account (clarified session 2026-07-06). A director creates a staff profile (name, phone, qualification level, optional photo), which provisions a new `TenantUser` (Role = Staff) and emails a tenant-scoped invitation token letting the invitee set their own password — reusing the invitation-token *mechanism* from feature 001 but as a new, tenant-scoped entity (`StaffInvitation`), since feature 001's `Invitation` is public-schema and creates a new organisation, not a new user within an existing one. A many-to-many `StaffLocationEligibility` join records which locations (feature 004) a staff member may work at, with no date/schedule semantics (feature 011's concern). Deactivation is soft-delete only, following the same `IDeactivationGuard`-style extension point feature 004 established for locations (zero guards registered here — features 009/011 register their own later). Profile photos introduce the project's first real GCP Cloud Storage integration: a new `IProfilePhotoStorage` port issues V4-signed upload/download URLs so the API never proxies image bytes and never stores or serves a public URL (constitution Principle VI). Backend-only, matching every prior feature's boundary — the web admin UI is a separate effort.

## Technical Context

**Language/Version**: C# / .NET 10, EF Core 10 (unchanged from features 001–004)

**Primary Dependencies**: ASP.NET Core Minimal APIs + `DirectorOnly` policy (feature 003, unchanged), MediatR + FluentValidation (constitution Principle III), EF Core 10 + Npgsql, `Google.Cloud.Storage.V1` (NEW — V4 signed URL generation, no other GCS client library exists yet), existing `IEmailSender`/`EmailService` (feature 001/003) gains one new method for staff invitation emails

**Storage**: PostgreSQL 16, schema-per-tenant (unchanged) — one new tenant-schema migration (`AddStaff`) covering `StaffProfile`, `StaffInvitation`, `StaffLocationEligibility`, applied the same way feature 004's `AddLocations` was (auto-applied to new schemas via `TenantProvisioningService`, rolled out to existing schemas via the `migrate-tenants` CLI). GCP Cloud Storage (NEW) for profile photo objects — one bucket, no database storage of binary content, only the object path stored on `StaffProfile`

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (constitution Principle V), extending `TestWebAppFactoryBase`; `IProfilePhotoStorage` is faked/stubbed in tests (no real GCS calls in CI, consistent with Google/Apple OAuth validation already being mocked in feature 003's tests)

**Target Platform**: Linux container on Cloud Run (existing `infra/gcp/` Terraform + `.github/workflows/deploy-gcp.yml`) — one new Terraform resource (a single `google_storage_bucket` for profile photos) plus a service-account binding for signing, no other infra changes

**Project Type**: Web service (ASP.NET Core API) — backend only, no web admin UI work, same boundary as features 001–004

**Performance Goals**: No explicit throughput target beyond SC-001/SC-003's task-completion-time framing (director creates a profile in under 2 minutes; a staff member logs in within one session of receiving their invite); staff rosters are small per organisation (dozens, not thousands), so no pagination is required

**Constraints**: Deny-by-default tenant scoping — every staff endpoint goes through `TenantMiddleware`/`ICurrentTenantService` (feature 002), same as feature 004; every endpoint requires `DirectorOnly`; no hard-delete code path (FR-010); qualification level is conditionally required (mandatory for `Staff` role, optional for `Director` role — FR-003, clarified); profile photos are never served via a public/unsigned URL (FR-013, constitution Principle VI)

**Scale/Scope**: Same Phase 1 scale as prior features (dozens of organisations, a handful to dozens of staff each). New entities: `StaffProfile`, `StaffInvitation`, `StaffLocationEligibility` join. New external integration: GCP Cloud Storage signed URLs (first feature to need it). ~9 endpoints (create, get, list, update, deactivate, reactivate, accept-invitation, assign/unassign eligible location, request-photo-upload-url)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation (NON-NEGOTIABLE) | ✅ Pass | Every `StaffEndpoints.cs` route is non-exempt and goes through `TenantMiddleware`/`ICurrentTenantService` (feature 002), same as feature 004. `StaffProfile`/`StaffInvitation`/`StaffLocationEligibility` carry no `OrganisationId`/tenant column — schema-per-tenant is the isolation boundary (mirrors `TenantUser`/`Location`). |
| II. Regulatory Compliance by Design | ✅ Pass | Qualification level is captured and persisted here specifically because feature 009's BKR computation is NON-NEGOTIABLE and depends on it (only qualified caregivers/auxiliaries count). This feature stores the field correctly (required for Staff role — FR-003) but does not itself compute or enforce BKR; that enforcement lives in feature 009 per the constitution's explicit assignment ("BKR ratio enforcement lives in attendance (009), not in contracts" — same principle extends to staff: storage here, enforcement there). |
| III. CQRS via MediatR & Thin Endpoints | ✅ Pass | Every write (create profile, update profile, deactivate, reactivate, accept invitation, assign/unassign location eligibility, request photo-upload URL) is a MediatR command with a FluentValidation validator. Reads (get, list) are MediatR queries, consistent with feature 004's choice to model even simple lookups as queries. `StaffEndpoints.cs` only maps HTTP ↔ MediatR. |
| IV. Internationalization First (NON-NEGOTIABLE) | ✅ Pass | All validation/error responses and UI-facing labels use new `errors.staff.*` keys (added to `backend/ERROR_KEYS.md` during implementation), never raw text. Invitation email *body* content is explicitly scoped out of this feature's i18n requirement (spec.md FR-014, amended during `/speckit-analyze` finding C1) — it follows the existing `EmailService` raw-string-literal, English-only pattern already shipped in features 001/003, with feature 019 owning the templating/i18n rework for all transactional email project-wide. This is a deliberate, spec-documented scope boundary, not an undocumented gap. |
| V. Test with Real Infrastructure (NON-NEGOTIABLE) | ✅ Pass | TestContainers-backed integration tests extending `TestWebAppFactoryBase`. `IProfilePhotoStorage` is the one seam that is faked in tests (no test hits real GCS), the same way Google tokeninfo/Apple JWKS validation is already faked in feature 003's tests — this is an external-service boundary, not a database behavior InMemory would hide. |
| VI. Secure Configuration & Storage | ✅ Pass | GCS service-account credentials come from environment/Secret Manager (never hardcoded), matching existing Google/Apple OAuth credential handling. Profile photos are served exclusively via V4-signed, time-limited URLs generated on read — no public bucket, no public object ACLs, no signed URL persisted to the database (only the object path is stored; the signed URL itself is generated fresh on each read so it can't outlive its intended short lifetime). The `AddStaff` migration follows the ordinary reviewed/manual-rollout path for existing tenant schemas (no carve-out applies — this isn't new-tenant-schema provisioning). |
| VII. Monolith-First Simplicity | ✅ Pass | No new project. `StaffProfile`/`StaffInvitation`/`StaffLocationEligibility` live in `ChildCare.Domain`; `Staff/` command/query handlers in `ChildCare.Application`; `IProfilePhotoStorage` port in `ChildCare.Application/Common` with its GCS implementation in `ChildCare.Infrastructure` (mirrors how `IEmailSender`'s port/implementation are already split across Application/Api) — no new deployable, no new solution project. |

**Overall**: 7 of 7 clean passes. No Complexity Tracking entries needed.

## Project Structure

### Documentation (this feature)

```text
specs/005-staff/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── staff-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks — not created by this command)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   └── Entities/
│       ├── StaffProfile.cs                     #   NEW — FK to TenantUser, no OrganisationId column (research.md R1)
│       ├── StaffInvitation.cs                  #   NEW — tenant-scoped, distinct from public-schema Invitation (research.md R2)
│       └── StaffLocationEligibility.cs         #   NEW — join entity (StaffProfileId, LocationId)
├── ChildCare.Application/
│   ├── Common/
│   │   ├── IStaffDeactivationGuard.cs          #   NEW — extension point for 009/011 (mirrors ILocationDeactivationGuard)
│   │   └── IProfilePhotoStorage.cs             #   NEW — signed upload/download URL port (research.md R3)
│   └── Staff/                                   #   NEW
│       ├── CreateStaffProfileCommand.cs / …Validator.cs / …Handler.cs
│       ├── UpdateStaffProfileCommand.cs / …Validator.cs / …Handler.cs
│       ├── DeactivateStaffProfileCommand.cs / …Handler.cs
│       ├── ReactivateStaffProfileCommand.cs / …Handler.cs
│       ├── AcceptStaffInvitationCommand.cs / …Validator.cs / …Handler.cs
│       ├── ResendStaffInvitationCommand.cs / …Handler.cs      #   NEW — added during /speckit-analyze follow-ups (FR-006a)
│       ├── AssignLocationEligibilityCommand.cs / …Handler.cs
│       ├── UnassignLocationEligibilityCommand.cs / …Handler.cs
│       ├── RequestPhotoUploadUrlCommand.cs / …Handler.cs
│       ├── ListStaffQuery.cs / …Handler.cs
│       ├── GetStaffByIdQuery.cs / …Handler.cs
│       └── StaffResult.cs                       #   shared success/failure result shape (mirrors LocationResult)
├── ChildCare.Infrastructure/
│   ├── Persistence/
│   │   ├── TenantDbContext.cs                  #   MODIFIED — + DbSet<StaffProfile/StaffInvitation/StaffLocationEligibility>
│   │   └── Migrations/Tenant/                  #   NEW migration — AddStaff
│   └── Storage/
│       └── GcsProfilePhotoStorage.cs           #   NEW — IProfilePhotoStorage impl, Google.Cloud.Storage.V1 V4 signing
├── ChildCare.Contracts/
│   ├── Requests/
│   │   └── StaffRequests.cs                    #   NEW — Create/Update/AcceptInvitation/AssignLocation request DTOs
│   └── Responses/
│       └── StaffResponse.cs                    #   NEW
├── ChildCare.Api/
│   ├── Endpoints/
│   │   └── StaffEndpoints.cs                   #   NEW — /api/staff/*, all DirectorOnly except accept-invitation
│   ├── Services/
│   │   └── EmailService.cs                     #   MODIFIED — + SendStaffInvitationAsync
│   └── Program.cs                              #   MODIFIED — app.MapStaffEndpoints(); register IProfilePhotoStorage,
│                                                #     default IStaffDeactivationGuard (research.md R4)
└── ChildCare.Api.Tests/
    ├── StaffProfileCrudTests.cs                 #   NEW — US1 (SC-001/SC-002/SC-003, FR-001–003/005–008/014/015)
    ├── StaffLocationEligibilityTests.cs          #   NEW — US2 (SC-005, FR-004)
    ├── StaffProfileUpdateTests.cs                #   NEW — US3 (FR-009, FR-013)
    └── StaffDeactivationTests.cs                  #   NEW — US4 (SC-004, FR-010–012)

infra/gcp/
└── main.tf                                      #   MODIFIED — + google_storage_bucket (profile photos) + signing SA binding
```

**Structure Decision**: Web-service (backend-only). No new projects — extends the existing 5-project structure the same way feature 004 did. The one structural addition beyond a typical CRUD feature is `ChildCare.Infrastructure/Storage/` (new folder, not a new project) for the GCS-backed `IProfilePhotoStorage` implementation, since this is the first feature to need real Cloud Storage integration.

## Complexity Tracking

> Empty — no unresolved Constitution Check violations remain for this plan.
