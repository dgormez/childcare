# Contract: Photo Lifecycle & Governance API

Auth idiom: existing policies only (`StaffOrDirector`, `ParentOnly`, `DirectorOnly`) — no new
policy is introduced (spec.md Assumptions).

## `DELETE /api/group-activities/{id}` (policy change only)

Was `DirectorOnly`, now `StaffOrDirector`. Request/response shape unchanged — deletes the
activity and cascades to its photos via the existing `IGroupActivityPhotoStorage.DeleteAsync`.

## `DELETE /api/children/{childId}/health-records/{id}` (policy change only)

Was `DirectorOnly`, now `StaffOrDirector`. Request/response shape unchanged.

## `DELETE /api/children/{childId}/vaccine-records/{id}` (policy change only)

Was `DirectorOnly`, now `StaffOrDirector`. Request/response shape unchanged.

Upload routes (`POST .../photos`, `POST .../attachment-upload-url`) are unchanged — kiosk
`DeviceAuthenticated` upload for group-activity photos and `DirectorOnly`-issued upload URLs for
health/vaccine attachments were already staff-accessible per spec.md's Clarifications; this
feature widens *delete* to match, it does not change who can upload.

## `GET /api/parent/photos/{photoType}/{objectRef}/download` (new)

`ParentOnly`. `photoType` is one of `profile | group-activity | health-attachment`; `objectRef`
identifies the specific photo (child id for profile; photo id for group-activity; attachment id
for health/vaccine). The handler re-validates the same ownership/consent gate
`GetParentGroupActivityGalleryQuery` already applies (own child only; group-activity photos
additionally require the existing `Contract.Consent.PhotosInternal` gate) before issuing the URL.

- Response `200`: `{ "downloadUrl": "https://storage.googleapis.com/...", "expiresAt": "..." }`
  — a V4 signed URL with `Content-Disposition: attachment`, 15-minute TTL (existing convention),
  pointing at the full-resolution object (never the thumbnail).
- `403 errors.photos.forbidden` — child is not the requesting parent's own, or consent is not
  granted for a group-activity photo.
- `404 errors.photos.not_found` — unknown object reference.

## `POST /api/children/{childId}/purge-photos` (new)

`DirectorOnly` — matches `ChildrenEndpoints.cs`'s existing convention that all *write* routes on
this file are `DirectorOnly` (per its own file-level comment); this is a deliberate,
compliance-sensitive irreversible action, kept at the narrower existing tier rather than widened
to `StaffOrDirector` alongside the delete-endpoint changes above, since spec.md's Clarifications
only extend the *record-level delete* audit gap to `StaffOrDirector` — GDPR erasure was never
part of that inconsistency (it's new capability, not an existing gap) and stays director-level by
default, matching this file's established pattern.

Rejects (`400 errors.children.still_active`) if the child's `DeactivatedAt` is null — nothing is
deleted.

- Response `200`:
  ```json
  {
    "deletedObjectPaths": ["children/{id}/photo.jpg", "health-records/{id}/attachment.pdf"],
    "failedObjectPaths": [],
    "preservedGroupPhotoCount": 1
  }
  ```
  `preservedGroupPhotoCount` — number of group-activity photos that depict this child alongside
  at least one other (active or not-yet-purged) child, intentionally left untouched (FR-016), so
  the director-web confirmation can state plainly what was and wasn't deleted (UX Requirements,
  spec.md).
- `400 errors.children.still_active` — child is not deactivated; nothing deleted.
- `404 errors.children.not_found`.
- A non-empty `failedObjectPaths` in a `200` response indicates a partial failure (some deletes
  succeeded, at least one GCS call failed) — the director-web client MUST render this as a
  failure state, not success, per FR-016; this is a body-level signal rather than a different
  HTTP status because the *request* itself succeeded (the cascade ran), only some underlying GCS
  operations did not.
