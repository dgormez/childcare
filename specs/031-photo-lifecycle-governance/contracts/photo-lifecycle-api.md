# Contract: Photo Lifecycle & Governance API

Auth idiom: existing policies only (`StaffOrDirector`, `ParentOnly`, `DirectorOnly`) — no new
policy is introduced (spec.md Assumptions).

## `DELETE /api/group-activities/{id}` (policy change only)

Was `DirectorOnly`, now `StaffOrDirector`. Request/response shape unchanged — deletes the
activity and cascades to its photos via the existing `IGroupActivityPhotoStorage.DeleteAsync`.
`POST /api/group-activities/{id}/photos` (create/upload) is **unchanged** — it already runs on
the caregiver-tablet's `DeviceAuthenticated` device-token channel (008a), which has no staff-JWT
role claim at all and is not part of this RBAC gap; only the delete path (a director/staff-web
JWT action) gets widened.

## Health-record and vaccine-record endpoints (policy change — all four routes per port)

Per spec.md's Clarifications, health/vaccine records were verified to be **entirely**
`DirectorOnly` today — create, edit, attachment-upload-url, and delete alike — meaning staff had
*zero* access to either record type, not a delete-lags-upload asymmetry. This feature widens all
four routes on each port from `DirectorOnly` to `StaffOrDirector`:

- `POST /api/children/{childId}/health-records`, `PUT .../health-records/{id}`,
  `POST .../health-records/{id}/attachment-upload-url`, `DELETE .../health-records/{id}`.
- `POST /api/children/{childId}/vaccine-records`, `PUT .../vaccine-records/{id}`,
  `POST .../vaccine-records/{id}/attachment-upload-url`, `DELETE .../vaccine-records/{id}`.

Request/response shapes are unchanged for all eight routes — this is a policy-only change.

## `GET /api/parent/photos/{photoType}/{objectRef}/download` (new)

`ParentOnly`. `photoType` is one of `profile | group-activity` — **not** `health-attachment`:
parents have no visibility into health/vaccine records anywhere in the API today (no route on
`ParentEndpoints.cs` exposes them), and neither spec.md's User Story 2 acceptance scenarios nor
FR-012 mention medical documents — only "photos." Introducing new parent-facing access to
health/vaccine attachments would be an unrequested, privacy-sensitive scope expansion; the
`IHealthAttachmentStorage.CreateAttachmentDownloadUrlAsync` method this feature adds exists for
a possible future need but is not wired to this endpoint. `objectRef` identifies the specific
photo (child id for profile; photo id for group-activity). The handler re-validates the same
ownership/consent gate `GetParentGroupActivityGalleryQuery` already applies (own child only;
group-activity photos additionally require the existing `Contract.Consent.PhotosInternal` gate,
via the shared `IGroupActivityChildDerivationService`) before issuing the URL.

- Response `200`: `{ "downloadUrl": "https://storage.googleapis.com/...", "expiresAt": "..." }`
  — a V4 signed URL with `Content-Disposition: attachment`, 15-minute TTL (existing convention),
  pointing at the full-resolution object (never the thumbnail).
- `403 errors.photos.forbidden` — child is not the requesting parent's own, or consent is not
  granted for a group-activity photo.
- `404 errors.photos.not_found` — unknown object reference.

## `POST /api/children/{childId}/purge-photos` (new)

`StaffOrDirector` — per FR-008 and the UX Requirements' persona line, both spec.md's own text,
which explicitly extend purge to "director or staff member" / "Director/Staff," not director
alone. (An earlier draft of this contract restricted this route to `DirectorOnly`, reasoning it
as a narrower compliance action outside the FR-011 RBAC-parity gap; that reasoning directly
contradicted the spec's own FR-008 and was corrected during `/speckit-analyze`.) This does mean
`ChildrenEndpoints.cs` gains its first non-`DirectorOnly` write route alongside the file's
existing `DirectorOnly` group — map it the same way `StaffEndpoints.cs` already handles its one
`StaffOrDirector` exception (a standalone route outside the `DirectorOnly` group, per that file's
own comment on why route-group composition requires this).

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
