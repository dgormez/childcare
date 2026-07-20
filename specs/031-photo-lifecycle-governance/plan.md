# Implementation Plan: Photo Lifecycle & Governance

**Branch**: `031-photo-lifecycle-governance` | **Date**: 2026-07-19 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/031-photo-lifecycle-governance/spec.md`

## Summary

Close three governance gaps left open by the existing photo storage ports (005/006 profile
photos, 009b group-activity photos, 013b/013c health/vaccine attachments): (1) inconsistent
staff/director authorization — verified against the actual endpoint code (spec.md's
Clarifications), health/vaccine record create/edit/attachment-upload/delete are ALL
`DirectorOnly` today (staff has zero access, not a delete-lags-upload asymmetry), while
group-activity photos create via the caregiver-tablet's `DeviceAuthenticated` device-token
channel (unchanged, not a gap) but delete is `DirectorOnly` (no staff-JWT path at all) — (2) no
storage-class cost tiering at all, and (3) no way to delete a profile photo or health/vaccine
attachment (only group-activity photos have a `DeleteAsync` today). Technical approach: widen
health/vaccine record create/edit/attachment-upload/delete endpoints, and the group-activity
delete endpoint, from `DirectorOnly` to `StaffOrDirector`; add `DeleteAsync` to
`IProfilePhotoStorage`/`IHealthAttachmentStorage`; add an attachment-disposition download-URL
method to all three storage ports for the parent-facing download action; add a new
tenant-looping CLI job (`evaluate-photo-archival`, following the `send-payment-reminders`
pattern) that transitions a deactivated child's objects to Coldline after a 30-day grace period,
using the same group-membership derivation `GetParentGroupActivityGalleryQuery` already uses
(extracted into a shared service so both consumers stay consistent); add a Terraform bucket
`lifecycle_rule`, prefix-scoped to full-resolution originals, moving any object to Nearline after
90 days of `age` (a proxy for access-recency — GCS has no native "days since last read"
condition); add a new `PurgeChildPhotosCommand` (`StaffOrDirector`, per FR-008) that permanently
deletes a deactivated child's profile photo and health/vaccine attachments plus any
group-activity photo where they are the sole derived child, guarded against active children,
logging a structured audit entry per the codebase's existing `ILogger`-based precedent (no new
`AuditLog` table).

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript (Next.js 15 web-admin); TypeScript
(Expo/React Native, parent-mobile).

**Primary Dependencies**: MediatR (CQRS), EF Core 9, `Google.Cloud.Storage.V1` (existing GCS
client already used by all three storage adapters), FluentValidation.

**Storage**: PostgreSQL 16 (schema-per-tenant) for `Child.DeactivatedAt` / group-membership
lookups only — this feature adds no new tables; GCP Cloud Storage (single shared bucket,
`{project}-staff-profile-photos`) for the photo objects whose lifecycle this feature governs.

**Testing**: xUnit + Moq for unit tests; TestContainers-backed PostgreSQL integration tests per
Constitution V — no EF Core InMemory provider.

**Target Platform**: Cloud Run (backend API + the new scheduled CLI job); parent-mobile (Expo);
director-web (Next.js).

**Project Type**: Web application (existing 5-project backend solution + parent-mobile + web).

**Performance Goals**: No new request-path cost beyond existing signed-URL generation; the
archival job runs outside the request path, tenant-by-tenant, isolating one tenant's failure
from blocking others (matching `send-payment-reminders`/`send-daily-reports` precedent).

**Constraints**: No new GCS bucket and no change to existing object-path layout (Assumptions,
spec.md) — Terraform lifecycle rules are prefix-scoped within the one shared bucket; storage-class
transitions must never change the signed-URL serving path (FR-006/FR-011 in spec.md numbering —
see Requirements).

**Scale/Scope**: Backend: 2 storage-port interface additions (delete + attachment-download),
1 storage-port interface unchanged (group-activity already has delete), 3 endpoint policy
widenings, 1 new command (purge), 1 new CLI job, 1 new shared derivation service. Infra: 1
Terraform `lifecycle_rule` block. Parent-mobile: 1 new UI action. Director-web: 1 new action on
the deactivated-child profile screen.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation (NON-NEGOTIABLE) | PASS | The new CLI job and purge command both go through `ITenantDbContextResolver.ForSchema(tenant.SchemaName)` per tenant, the same pattern as `SendPaymentRemindersCommand`/`SendDailyReportsCommand` — no cross-tenant query path. Research confirmed GCS object paths carry no tenant segment (isolation relies on non-guessable GUIDs + signed URLs, an existing pattern this feature does not change); the Terraform lifecycle rule this feature adds is bucket/prefix-scoped, not tenant-scoped, but it is a **cost-tiering storage-class change**, not a data-read or data-deletion path — it cannot leak one tenant's data to another (see research.md R1 for why this is safe despite being bucket-wide). |
| II. Regulatory Compliance by Design (NON-NEGOTIABLE) | PASS (N/A) | This feature is not itself enforcing a BKR/ratio or attendance regulation; it implements GDPR-erasure and retention-adjacent policy for photos specifically. No leefgroep/BKR carve-out is implicated. |
| III. CQRS via MediatR & Thin Endpoints | PASS | `PurgeChildPhotosCommand` is a MediatR command; the archival evaluation runs as an app-level CLI job (same shape as existing scheduled jobs, which also route through MediatR/services rather than endpoint-embedded logic); endpoint files only gain policy-attribute changes and one new route mapping. |
| IV. Internationalization First (NON-NEGOTIABLE) | PASS | FR-015 requires all new strings (download action, purge action/confirmation) to ship as NL/FR/EN keys from the start — data-model.md/quickstart.md enumerate the exact keys. |
| V. Test with Real Infrastructure (NON-NEGOTIABLE) | PASS | Purge-cascade and RBAC-widening tests run as TestContainers-backed integration tests (per constitution, not InMemory); the archival job's tenant-loop and group-membership-derivation logic get unit tests plus one TestContainers integration test for the eligibility computation. |
| VI. Secure Configuration & Storage | PASS | No new secrets; signed URLs remain time-limited (15-minute TTL, existing convention) for both the existing inline view and the new attachment-disposition download; purge failures surface a generic, localized message to the director/staff user while the full exception is logged server-side, per this principle and the user's global error-handling convention. |
| VII. Monolith-First Simplicity | PASS | No new deployable/service — the archival job is a CLI subcommand on the existing API image (matching `send-payment-reminders`), and audit logging reuses the existing `ILogger`-based pattern rather than introducing a new `AuditLog` table/service (research.md R4 — a new persisted audit-log entity was considered and rejected as premature for a single feature, consistent with the same reasoning the constitution's own carve-outs apply elsewhere). |

No violations — Complexity Tracking is empty.

## Project Structure

### Documentation (this feature)

```text
specs/031-photo-lifecycle-governance/
├── plan.md              # This file
├── research.md           # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── photo-lifecycle-api.md
└── tasks.md               # Phase 2 output (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   └── (no new entities — DeactivatedAt and ChildGroupAssignment already exist)
├── ChildCare.Application/
│   ├── Common/
│   │   ├── IProfilePhotoStorage.cs           # + DeleteAsync, + CreateAttachmentDownloadUrlAsync
│   │   ├── IGroupActivityPhotoStorage.cs     # + CreateAttachmentDownloadUrlAsync
│   │   ├── IHealthAttachmentStorage.cs       # + DeleteAsync, + CreateAttachmentDownloadUrlAsync
│   │   └── IGroupActivityChildDerivationService.cs   # new — shared derivation, extracted
│   ├── GroupActivities/
│   │   └── GetParentGroupActivityGalleryQuery.cs      # refactored to use the shared service
│   └── Children/
│       └── PurgeChildPhotosCommand.cs         # new
├── ChildCare.Infrastructure/
│   ├── Storage/
│   │   ├── GcsProfilePhotoStorage.cs          # + DeleteAsync, + attachment-URL method
│   │   ├── GcsGroupActivityPhotoStorage.cs    # + attachment-URL method
│   │   └── GcsHealthAttachmentStorage.cs      # + DeleteAsync, + attachment-URL method
│   └── GroupActivities/
│       └── GroupActivityChildDerivationService.cs     # new implementation
├── ChildCare.Api/
│   ├── Endpoints/
│   │   ├── HealthRecordEndpoints.cs           # DirectorOnly → StaffOrDirector (create/edit/attachment-upload/delete — staff had zero access before)
│   │   ├── VaccineRecordEndpoints.cs          # DirectorOnly → StaffOrDirector (create/edit/attachment-upload/delete x2 — staff had zero access before)
│   │   ├── GroupActivityEndpoints.cs          # DirectorOnly → StaffOrDirector (delete only — create/upload stays DeviceAuthenticated, unchanged)
│   │   ├── ChildrenEndpoints.cs               # + purge route (StaffOrDirector per FR-008, standalone route outside this file's DirectorOnly group)
│   │   └── ParentEndpoints.cs (or equivalent)  # + download-original routes
│   ├── Cli/
│   │   └── EvaluatePhotoArchivalCommand.cs    # new — mirrors SendPaymentRemindersCommand
│   └── Program.cs                              # + args[0] == "evaluate-photo-archival" branch
└── ChildCare.Contracts/
    └── Responses/ (extend with purge result / download response shapes as needed)

infra/gcp/
└── main.tf                # + lifecycle_rule on google_storage_bucket.staff_profile_photos,
                            #   + google_cloud_run_v2_job / google_cloud_scheduler_job pair for
                            #   evaluate-photo-archival, mirroring send-payment-reminders' wiring

parent-mobile/
├── app/ (or screens/) ...  # "Download original" action on photo detail/gallery view
└── i18n/locales/{en,nl,fr}.json   # + downloadOriginal / downloadFailed keys

web/
├── app/ ...                # "Purge photos" action on the deactivated-child profile screen
└── (i18n) ...               # + purge action/confirmation keys, all three locales
```

**Structure Decision**: Follows the existing 5-project backend solution (Domain / Application /
Infrastructure / Api / Contracts) plus the two existing client apps this feature touches
(parent-mobile for download, web for the director-facing purge action) — no new project, no new
deployable, per Constitution VII.

## Complexity Tracking

*No Constitution Check violations — table intentionally left empty.*
