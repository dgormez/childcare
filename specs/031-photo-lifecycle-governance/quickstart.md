# Quickstart: Photo Lifecycle & Governance

Validation scenarios proving this feature works end-to-end. Run against local dev
(`docker compose up` PostgreSQL + API) unless noted.

## Prerequisites

- A tenant with at least one location, one staff account, one director account, one parent
  account with a child enrolled and photo-consent granted (see feature 007).
- A GCS emulator or a real dev bucket configured per `Storage:ProfilePhotosBucketName` — the
  storage-class assertions below require a real GCS bucket (the fake-gcs-server emulator used in
  other tests does not model storage classes); run those specific scenarios against the dev GCP
  project, not local Docker.

## Scenario 1 — Staff/director RBAC parity

1. Using a paired kiosk device token (not a staff JWT), `POST /api/group-activities/{id}/photos`
   (upload) — succeeds, unchanged by this feature.
2. As a staff JWT account, `DELETE /api/group-activities/{id}` for that activity — expect `204`
   (previously would have been `403 DirectorOnly`).
3. As the same staff account, `POST`, `PUT`, `POST .../attachment-upload-url`, and `DELETE` on
   `/api/children/{childId}/health-records/{id}` and the equivalent
   `/api/children/{childId}/vaccine-records/{id}` routes — expect success on all eight calls
   (previously all eight would have been `403 DirectorOnly` — staff had zero access to either
   record type before this feature).
4. As a parent account, attempt any of the delete/create/edit calls above — expect `403` for all.

## Scenario 2 — Parent downloads an original photo

1. As a parent, `GET /api/parent/photos/group-activity/{photoId}/download` for a photo their
   child appears in (via group membership) — expect `200` with a `downloadUrl` whose signed
   query params include `response-content-disposition=attachment%3B...`.
2. Fetch `downloadUrl` directly — expect the response `Content-Disposition` header to be
   `attachment; filename="..."`, and the byte size to match the full-resolution object, not the
   thumbnail.
3. As a different parent (no relationship to the depicted children), attempt the same download
   URL request — expect `403`.

## Scenario 3 — Archive-on-departure

1. Deactivate a child (`DeactivateChildCommand`) who has a profile photo and one solely-theirs
   group-activity photo.
2. Manually back-date `DeactivatedAt` by 31 days (test-only DB update) — do not wait 31 real days.
3. Run `dotnet run --project ChildCare.Api -- evaluate-photo-archival`.
4. Query the GCS object's storage class directly (`gsutil stat gs://.../children/{id}/photo.jpg`
   or the equivalent `Google.Cloud.Storage.V1` call) — expect `COLDLINE`.
5. Re-fetch the child's profile via the normal API — expect the signed URL to still resolve and
   the image to still load (proves FR-006/FR-011: no visible functional change).
6. Reactivate the child (`ReactivateChildCommand`) — expect no error, no "un-archive" step
   required, and the same signed URL flow to keep working (FR-007/FR-014).

## Scenario 4 — Group-activity photo stays on Standard while any depicted child is active

1. Create a group activity with two children in its group, one of whom is deactivated (and
   back-dated 31 days as above), the other still active.
2. Run `evaluate-photo-archival`.
3. Expect the group-activity photo's storage class to remain `STANDARD` — not transitioned.
4. Deactivate the second child too, back-date 31 days, re-run the job — now expect `COLDLINE`.

## Scenario 5 — GDPR purge

1. Deactivate a child with: a profile photo, a health-record attachment, a group-activity photo
   where they are the only depicted child, and a second group-activity photo shared with an
   active child.
2. As a director, `POST /api/children/{childId}/purge-photos`.
3. Expect `200` with `deletedObjectPaths` containing the profile photo, the health attachment,
   and the solely-depicted group photo; `preservedGroupPhotoCount: 1`.
4. Attempt to fetch the shared group-activity photo (via the parent of the still-active child) —
   expect it still resolves normally.
5. Attempt to fetch any of the deleted objects via a previously-issued signed URL — expect a GCS
   404.
6. Attempt the same purge again on an **active** (non-deactivated) child — expect
   `400 errors.children.still_active`, and confirm via GCS that nothing was deleted.

## Scenario 6 — General cost tiering (infra-only, no app interaction)

1. `terraform plan` against `infra/gcp/main.tf` after this feature's changes — expect a new
   `lifecycle_rule` diff on `google_storage_bucket.staff_profile_photos`, scoped to the
   `staff/`, `children/`, `health-records/`, `vaccine-records/` prefixes, `age = 90`,
   `SetStorageClass → NEARLINE`.
2. No app-level test needed for this scenario — it is bucket configuration, not application
   behavior (per the original backlog's own framing: "no handler changes needed for this part").
