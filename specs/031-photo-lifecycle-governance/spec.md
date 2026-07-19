# Feature Specification: Photo Lifecycle & Governance

**Feature Branch**: `031-photo-lifecycle-governance`

**Created**: 2026-07-19

**Status**: Draft

**Input**: User description: "Build the retention, cost-tiering, and access-control rules that govern photos already stored in GCS (profile photos, group activity photos, health attachments) — no new upload/storage mechanism, this feature only adds policy on top of the existing ports."

## Clarifications

### Session 2026-07-19 (self-resolved against verified codebase precedent — no product-owner ambiguity)

- Q: The prompt's premise is "ALL photo buckets" (plural) — is that accurate? → A: No. Research against `infra/gcp/main.tf` found exactly **one** GCS bucket (`{project}-staff-profile-photos`) shared by every storage port in the codebase: profile photos, group-activity photos, health/vaccine attachments, fiscal attestations (015), and bulk-email attachments (020) — disambiguated only by object-path prefix, never by bucket. This feature's lifecycle/tiering rules apply only to the photo/attachment prefixes it governs (`staff/`, `children/`, `group-activities/`, `health-records/`, `vaccine-records/`); fiscal-attestation and bulk-email-attachment prefixes are explicitly untouched — those are out of scope and governed by their own features if/when they need retention policy.
- Q: What does "tagged child" mean for a group-activity photo, given 009b never built a per-photo child-tagging table? → A: A group-activity photo's depicted children are derived the same way `GetParentGroupActivityGalleryQuery` (009b) already derives them for the parent gallery: every child whose `ChildGroupAssignment` places them in the activity's group on the activity's date. This feature reuses that existing derivation for archiving/deletion eligibility rather than inventing a new persisted tagging concept — a photo is archive-eligible once every child derived this way is inactive.
- Q: What is the ACTUAL current authorization gap across the three photo ports (the prompt says "confirm... close any gap found")? → A: More specific than the prompt assumed, verified directly against the endpoint code:
  - **Health/vaccine records** (`HealthRecordEndpoints.cs`, `VaccineRecordEndpoints.cs`): create, edit, attachment-upload, AND delete are ALL `DirectorOnly` today — symmetric, not an upload/delete asymmetry. Staff (a real, already-used JWT role — `TenantUser.Role == UserRole.Staff` logs in through the same shared web login as directors and already drives other `StaffOrDirector`-gated screens, e.g. `StaffScheduleEndpoints`, `MessageThreadEndpoints`) currently has **zero** access to health/vaccine records of any kind. Widening this to `StaffOrDirector` is a genuine capability grant to staff, not just closing a delete-lags-behind-upload gap.
  - **Group-activity photos** (`GroupActivityEndpoints.cs`): create/upload goes through the caregiver-tablet `DeviceAuthenticated` policy (008a's device-token + PIN model, not a staff JWT at all — the tablet never carries a `staff`-role JWT), while delete is `DirectorOnly` (a human JWT session). The real gap is that a staff member's own JWT session (as used elsewhere in this codebase) has no group-activity action at all today — closing it means widening delete to `StaffOrDirector`; the tablet's device-token create path is untouched (it's 008a's established, correct mechanism, not a gap).
  - **Resolution**: widen `DeleteGroupActivityCommand`'s, `DeleteHealthRecordCommand`'s, `DeleteVaccineRecordCommand`'s, and the health/vaccine create+edit+attachment-upload endpoints' authorization to `StaffOrDirector` — directed by the prompt's own explicit instruction to close any gap found so all three ports treat Staff and Director identically for upload/caption/edit/delete/download, per its item 3. This does not touch the caregiver-tablet's device-token upload path.
- Q: Does a GDPR purge action need an audit-trail record (who purged what, when)? → A: Yes — this codebase already established the precedent that a security/compliance-sensitive irreversible action gets an audit log entry (008a's revoked-device-rejection audit logging), and a GDPR erasure action is squarely in that category; a purge without an audit trail would be a regression against that precedent, not a neutral omission.

## Product Context

### Feature Type

Mixed — backend policy/access-control changes (RBAC audit + fix across three photo ports, a new GDPR deletion-cascade command, GCS lifecycle Terraform) plus a parent-mobile UI addition (download-original action).

### Primary Consumer

Mixed — Director/Staff (consistent photo RBAC, GDPR purge action), Parent (original-photo download), System (GCS lifecycle transitions, automatic archiving eligibility).

### Workflow Boundary

This is a cross-cutting policy layer over three existing workflows rather than a new top-level workflow: **Daily Child Care** (group-activity photos, 009b), **Child Lifecycle** (child deactivation/reactivation triggers archiving eligibility, 006), and **Health & Safety** (health/vaccine attachments, 013b/013c) — plus a parent-facing surface in **Parent Communication** (original-photo download). `workflows.md` is updated with a short cross-reference note under each of those four workflows pointing at this feature, rather than a new workflow section, since this feature introduces no new actor-facing business process of its own — only policy over four that already exist.

- **Actors**: Director, Staff (RBAC parity with Director on all three photo ports), Parent (view/download own child's photos only), System (GCS lifecycle transitions, archiving-eligibility evaluation on child deactivation).
- **Actions**: child deactivation → archiving-eligibility check per photo port; director/staff-initiated GDPR purge → cross-port deletion cascade; parent → download original.
- **Data Flow**: `DeactivateChildCommand` (006) does not change (no coupling added to the command itself); this feature instead adds a read-time/lifecycle-time eligibility check (a scheduled or on-access evaluation, detailed in plan.md) that looks at `Child.DeactivatedAt` plus, for group photos, every derived child's `DeactivatedAt`. GDPR purge is a new explicit command that fans out across `IProfilePhotoStorage`, `IGroupActivityPhotoStorage`, and `IHealthAttachmentStorage`.
- **Outputs**: GCS storage-class transitions (Standard → Nearline/Coldline), deleted GCS objects (purge only), a downloaded file (parent).
- **Cross-platform Impact**: backend spans all three photo ports + a new GDPR command; parent-mobile gets a new download action; director-web gets a new "Purge photos" action on the child profile (deactivated children only); infra/Terraform gets the bucket's first `lifecycle_rule` blocks. No caregiver-tablet change (008a) — caregivers don't manage retention/deletion policy, only upload/caption, which is unchanged.

### User Impact

This enables a director or staff member to permanently and correctly remove a departed child's photos on request (GDPR erasure) without accidentally deleting a group photo that still depicts an actively-enrolled child, keeps pre-revenue storage cost down via automatic cold-tiering of rarely-accessed originals, and enables a parent to save a full-resolution original of their child's photo to their own device.

### UX Requirements

**Persona**: Parent (primary, download action); Director/Staff (secondary, GDPR purge action on a deactivated child's profile).

**Platform**: parent-mobile (download action); director-web (purge action, deactivated-child profile only).

**User job (parent)**: "I want to save this original photo of my child to my own device."
**User job (director/staff)**: "A parent asked us to delete their departed child's photos — I need to do that without breaking group photos of other children."

**Success criteria**: tapping download saves the full-resolution original, not the thumbnail; the purge action is unavailable (not just disabled) for an active child, preventing accidental use on the wrong record.

**Main flow (parent)**: gallery/profile photo → download action → signed attachment-disposition URL → native share/save sheet.
**Main flow (director/staff)**: deactivated child's profile → "Purge photos" action → confirmation dialog naming exactly what will be deleted (profile photo, N solely-tagged group photos, N health/vaccine attachments) and what will be preserved (M group photos still depicting active children) → confirm → success state.

**Loading/empty/error states**: parent download shows a loading spinner on tap and a toast on signed-URL failure (generic, no internal/GCS error detail, per this codebase's error-handling convention); purge confirmation shows a loading state while the cascade runs and a clear success/failure result — a partial failure (e.g. one GCS delete call fails) must not silently report success.

**Accessibility**: 48pt minimum touch target on both actions (parent-mobile platform floor, `platform-rules.md`); purge is a destructive text-style action per `design-system.md`'s Destructive button pattern, never a filled button, so it doesn't compete with the profile screen's primary actions.

**Offline behavior**: parent download action is hidden when offline (a live signed URL is required; no offline-queue applicability, unlike 008a's check-in queue — this is a pure read/download action with no state to reconcile later).

**i18n**: parent-mobile keys follow the existing `gallery`/`invoices`/`fiscalAttestations` convention (`downloadOriginal`, `downloadFailed`) in `parent-mobile/i18n/locales/{en,nl,fr}.json`; director-web purge dialog strings follow web's existing i18n convention, all three locales.

### Technical Requirements

**API impact**:

- Extend `IProfilePhotoStorage`, `IGroupActivityPhotoStorage`, and `IHealthAttachmentStorage`'s download-URL generation (or add a sibling method) to support an attachment-disposition (`Content-Disposition: attachment`) signed URL, distinct from the existing inline/view signed URL — used only by the new parent-facing download endpoint(s), existing view/gallery endpoints unchanged.
- Widen authorization on `DeleteGroupActivityCommand`, `DeleteHealthRecordCommand`, `DeleteVaccineRecordCommand` (and their upload/caption/edit counterparts, where currently `DirectorOnly`) to `StaffOrDirector`, matching this codebase's existing `StaffOrDirector` policy (`backend/ChildCare.Api/Program.cs`) — no new policy invented.
- New GDPR purge command (director/staff-initiated, per-child) that: (a) deletes the child's profile photo object, (b) deletes every group-activity photo where this feature's derived-child-list resolves to exactly this one child (never a photo still depicting another active OR inactive-but-not-purged child — this feature only ever purges on the *targeted* child's explicit request, not incidentally sweeping up other departed children's photos), (c) deletes the child's health/vaccine attachment objects, and (d) is blocked (400, not silently no-op) if the child is still active — deletion is deliberate and requires deactivation first.
- New scheduled evaluation (System actor) that identifies archive-eligible objects (deactivated child past the grace period; group photo where every derived child is inactive past the grace period) and applies the storage-class transition — implementation mechanism (a backend job vs. a pure Terraform age/custom-time lifecycle rule) is a plan.md decision, not fixed here, since GCS's native lifecycle conditions can't natively evaluate "is every child in this cohort inactive," only object age/custom-time — this feature needs an explicit signal, detailed in plan.md/research.md.

**Data-model impact**: no new column is required to represent "archived" as a queryable flag — storage class lives on the GCS object itself, and the codebase's `Child.DeactivatedAt` already carries the trigger date needed to compute grace-period eligibility. Plan.md confirms whether a lightweight tracking column (e.g. last-evaluated timestamp) is needed for the scheduled evaluation job's own bookkeeping, distinct from any user-visible archived state.

**Security considerations**: attachment-disposition signed URLs remain short-lived (match the existing 15-minute TTL convention) and scoped to the requesting parent's own child (existing `ParentOnly` + child-ownership checks, unchanged pattern); the purge command must be provably scoped — a test proves a group photo with ≥2 distinct derived children is never deleted by purging one of them.

**Performance considerations**: none beyond existing signed-URL generation cost; the scheduled evaluation job runs outside the request path.

**Testing requirements**: RBAC regression tests per photo port (StaffOrDirector parity, ParentOnly view/download-only, no parent delete/upload/edit path); purge-cascade tests (solely-tagged vs. multi-tagged group photo, active-child block, profile photo, health/vaccine attachments); archiving-eligibility tests (all-derived-children-inactive vs. mixed, reactivation restores eligibility to "not archived" without an explicit un-archive step per the Edge Cases below).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director purges a departed child's photos on GDPR request (Priority: P1)

A parent has formally requested (outside the app, e.g. by email) that their departed child's photos be deleted. The director opens the deactivated child's profile and explicitly purges their photos — profile photo, every group photo where this child was the only depicted child, and health/vaccine attachments — while any group photo still depicting other children (active or not yet purged) is preserved.

**Why this priority**: This is the feature's core legal-compliance capability (GDPR erasure) and the one edge case where getting it wrong (deleting a shared photo, or failing to delete a solely-owned one) causes real harm — either a privacy violation or an accidental loss of another family's memory.

**Independent Test**: Can be fully tested by deactivating a child with a profile photo, a group photo shared with an active sibling-classmate, a group photo where they're the only child, and a health attachment, then purging — verifying exactly the solely-owned objects are deleted and the shared one survives untouched.

**Acceptance Scenarios**:

1. **Given** a deactivated child with a profile photo and two health-record attachments, **When** the director purges their photos, **Then** all three objects are deleted from GCS and the confirmation shows what was deleted.
2. **Given** a deactivated child depicted alone in one group-activity photo and alongside an actively-enrolled child in a second group-activity photo, **When** the director purges their photos, **Then** the solely-depicted photo is deleted and the shared photo remains fully intact and accessible.
3. **Given** an actively-enrolled (not deactivated) child, **When** a director attempts to purge their photos, **Then** the action is rejected with a clear message and nothing is deleted.

---

### User Story 2 - Parent downloads an original-resolution photo (Priority: P1)

A parent viewing their child's daily-report gallery or group-activity photo wants to keep a copy. They tap "Download original" and receive the full-resolution file (not the thumbnail used for gallery display) via their device's native share/save flow.

**Why this priority**: Directly requested capability with clear, frequent parent value — parents currently can only view photos in-app, not keep them, which is a real gap against competitor products (`reference-products.md`'s Famly/ClassDojo references both treat photos as keepsakes, not disposable content).

**Independent Test**: Can be fully tested by a parent opening a group-activity photo their child is tagged in (via 009b's existing group-derivation) and downloading it, verifying the downloaded file matches the full-resolution object, not the thumbnail.

**Acceptance Scenarios**:

1. **Given** a parent viewing a group-activity photo their child appears in, **When** they tap "Download original," **Then** the device receives the full-resolution image via its native share/save sheet, not the thumbnail.
2. **Given** a parent viewing a group photo depicting their child alongside other children, **When** they download it, **Then** the download succeeds and includes the full unmodified photo (no cropping/redaction of other children, per this feature's explicit out-of-scope).
3. **Given** the parent's device is offline, **When** they view a photo, **Then** the download action is not available (a live signed URL is required).

---

### User Story 3 - A departed child's photos automatically move to cheaper storage (Priority: P2)

Thirty days after a child is deactivated, their photos (profile photo, and any group photo where every depicted child is now inactive) transition to a cheaper GCS storage class automatically — without an administrator taking any action, and without any visible change to how the photos are viewed if ever needed again.

**Why this priority**: Directly addresses the feature's stated pre-revenue cost-control motivation, but it's an invisible background process with no user-facing urgency (unlike P1's compliance/parent-value stories) — correctness matters, but nothing breaks for a user if this runs a day late.

**Independent Test**: Can be fully tested by deactivating a child, advancing past the grace period, and verifying (via the storage layer, not the UI — this is invisible to end users by design) that the object's storage class has transitioned while the object remains fully resolvable via the existing signed-URL mechanism.

**Acceptance Scenarios**:

1. **Given** a child deactivated 31 days ago with no other active child depicted in their group photos, **When** the scheduled evaluation runs, **Then** their profile photo and solely-theirs group photos transition to the cheaper storage class.
2. **Given** a group-activity photo depicting eight children where seven have been deactivated for over 30 days but one remains active, **When** the scheduled evaluation runs, **Then** that photo remains on the Standard storage class.
3. **Given** a child was archived and is later reactivated (re-enrolled), **When** a caregiver or parent next views one of their photos, **Then** it resolves normally via the existing signed-URL mechanism (higher latency acceptable, no functional change, no explicit "un-archive" step required).

---

### User Story 4 - Staff and Director have consistent photo permissions (Priority: P2)

A staff member (not just a director) can create/edit, delete, and download health records, vaccine records, and group-activity photos within their assigned location(s) — matching what directors could already do — closing two verified gaps: health/vaccine records were entirely director-only (staff had no access at all), and group-activity deletion was director-only even though staff already have an established path to *create* group-activity photos (via the caregiver-tablet device-token model, feature 008a — that creation path itself is unchanged by this feature).

**Why this priority**: A real, verified authorization gap (staff had zero access to health/vaccine records, and no way to delete a group-activity photo their own tablet session created) — worth fixing but lower urgency than the compliance/parent-facing stories since it's a permission gap, not a data-safety one.

**Independent Test**: Can be fully tested by authenticating as a staff (non-director) web session and successfully creating/editing/deleting a health record and a vaccine record, and deleting a group-activity photo, all within their assigned location, then confirming a parent account still cannot perform any of upload/edit/delete.

**Acceptance Scenarios**:

1. **Given** a staff member assigned to a location, **When** they create, edit, or delete a health record or vaccine record at that location, **Then** the action succeeds (previously director-only).
2. **Given** a staff member assigned to a location, **When** they delete a group-activity photo at that location, **Then** the deletion succeeds (previously director-only) — the caregiver-tablet's own creation path for group-activity photos is unchanged.
3. **Given** a parent account, **When** they attempt to upload, edit, or delete any photo, **Then** the request is rejected — parents remain view/download-only.

---

### Edge Cases

- A child is reactivated after being archived: photos remain fully accessible via the same signed-URL mechanism (possibly higher latency); no explicit un-archive step is required, though a future optimization could move objects back to Standard proactively (out of scope here).
- A group photo has 8 tagged children; 7 become inactive over time while 1 remains enrolled: the photo stays on Standard tier and fully accessible until the last depicted child also becomes inactive, per US3/AC2.
- Purging one child's photos must never affect another child's still-active or not-yet-purged photos, even within the same group-activity photo, per US1/AC2.
- A child is deactivated and reactivated multiple times before any archiving grace period elapses: the eligibility evaluation always reflects current `DeactivatedAt` state, never a stale prior deactivation.
- The scheduled evaluation runs while a purge is concurrently in progress for the same child: the purge (explicit, user-initiated deletion) and the archiving evaluation (automatic, storage-class-only, never deletes) operate on disjoint outcomes — a deleted object is simply no longer present for the evaluation to consider, no conflict.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST NOT delete or auto-expire any photo/attachment object when a child is deactivated — deactivation alone never triggers deletion.
- **FR-002**: System MUST transition a deactivated child's profile photo to a cheaper storage class after a configurable grace period (default 30 days) following deactivation.
- **FR-003**: System MUST transition a group-activity photo to a cheaper storage class only once every child derivable as depicted in it (per the existing 009b group/date derivation) has been inactive for at least the grace period — a photo remains on the standard class as long as any derived child is active.
- **FR-004**: System MUST NOT transition thumbnail objects to a cheaper storage class — only full-resolution originals are tiered.
- **FR-005**: System MUST apply a general, configurable no-recent-activity cost-tiering policy (default 90 days) to full-resolution photo/attachment objects across all three photo ports, independent of child status, for cost control on active-child content too.
- **FR-006**: System MUST serve a storage-class-transitioned object through the same signed-URL mechanism as any other object — no functional or UI difference is visible to a caregiver, staff, director, or parent.
- **FR-007**: Reactivating a previously-deactivated child MUST NOT require any explicit "un-archive" action for their photos to remain accessible.
- **FR-008**: System MUST allow a director or staff member to explicitly and permanently delete (purge) a deactivated child's photo/attachment objects across all three photo ports, in a single action.
- **FR-009**: The purge action MUST be rejected, without deleting anything, if the targeted child is still active (not deactivated).
- **FR-010**: The purge action MUST delete a group-activity photo only when the targeted child is its sole depicted child (per the FR-003 derivation) — a group photo depicting any other child (active or inactive) MUST be preserved.
- **FR-011**: System MUST enforce identical authorization (Staff or Director) for create/upload, caption/edit, and delete actions on health records, vaccine records, and group-activity-photo deletion — closing the verified gap where health/vaccine records were entirely director-only (no staff access of any kind) and group-activity deletion was director-only despite staff already having an established creation path via the caregiver tablet (008a's device-token model, left unchanged by this feature).
- **FR-012**: Parents MUST be able to view and download original-resolution photos of their own child, including group photos their child is derived as depicted in, but MUST NOT be able to upload, edit, caption, or delete any photo.
- **FR-013**: The parent-facing "download original" action MUST return the full-resolution object (never the thumbnail) via a signed URL with an attachment (not inline) content-disposition.
- **FR-014**: Group-photo downloads MUST NOT crop, blur, or otherwise redact any child other than the requesting parent's own child (explicitly out of scope for this feature).
- **FR-015**: All new user-facing strings (download action, purge action and its confirmation dialog, any retention-related setting) MUST use i18n keys across NL/FR/EN, following each platform's existing key-naming convention.
- **FR-016**: A partial failure during a purge cascade (e.g. one object's GCS deletion call fails) MUST be surfaced as a failure, not reported as success, and MUST NOT silently leave the child's record in an inconsistent "some objects deleted" state without indicating this to the director/staff user.
- **FR-017**: Every purge action MUST be recorded in an audit log (who performed it, when, for which child) — an irreversible GDPR-erasure action is never silent.

### Key Entities

- **Photo/attachment object (GCS)**: not a new entity — the existing objects referenced by `Child.ProfilePhotoObjectPath`, `GroupActivityPhoto.ObjectPath`/`ThumbnailObjectPath`, `HealthRecord.AttachmentObjectPath`, and `VaccineRecord.AttachmentObjectPath`. This feature adds storage-class and deletion policy over these existing references, not a new table.
- **Archiving eligibility**: a derived/computed state (deactivated child's grace period elapsed; for group photos, every derived depicted child inactive past the grace period) — not necessarily a new persisted column; plan.md confirms the exact mechanism.
- **Purge action record**: a new audit-log entry (who purged what, when, for which child) is recorded on every purge action, per the Clarifications session — mirroring 008a's revoked-device audit-logging precedent. Exact persistence mechanism (dedicated table vs. reuse of an existing audit-log entity, if one exists) is a plan.md decision.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of group-activity photos depicting at least one currently-active child remain on the standard storage class — verified by a regression test, not just manual review, since silently mis-tiering an actively-viewed photo would degrade a caregiver/parent-facing experience.
- **SC-002**: A GDPR purge action never deletes a photo that still depicts another active or not-targeted child — verified by a regression test asserting zero cross-child data loss across repeated purge scenarios.
- **SC-003**: A parent can download a full-resolution original photo in two taps or fewer from the gallery/timeline view where they already view it.
- **SC-004**: Staff members can perform the same upload/edit/delete/download actions directors could already perform, across all three photo types, with zero remaining director-only gaps in the audited paths.
- **SC-005**: Storage-class transitions produce no reported functional regression (broken image, failed load) for any deactivated-then-reactivated child's photos.

## Assumptions

- The grace period defaults (30 days for archive-on-departure, 90 days for general no-recent-activity tiering) come directly from the BACKLOG prompt's own suggested values and are treated as the concrete starting configuration, not placeholders needing further clarification — both are described as "configurable," so the exact mechanism for changing them (Terraform variable vs. an application-level setting) is a plan.md decision.
- "Nearline or Coldline" (the prompt offers both) resolves to: **Nearline** for the general 90-day no-recent-activity tier (content that may still occasionally be viewed for an active child), and **Coldline** for the 30-day post-deactivation archive tier (access is expected to be rare — reactivation or an explicit purge request only). This is a cost/access-pattern judgment call, not a product-facing decision, and can be revisited without spec impact.
- The existing GCS object key layout (e.g., `group-activities/{id}/{photoId}.jpg` vs. `group-activities/{id}/{photoId}-thumb.jpg`) may need restructuring so that Terraform lifecycle rules (which match literal prefixes/suffixes, not patterns) can reliably distinguish full-resolution objects from thumbnails per FR-004; the exact approach is a plan.md/research.md decision, out of scope for this spec.
- No new authorization policy is introduced — `StaffOrDirector` and `ParentOnly` (both already defined in `backend/ChildCare.Api/Program.cs`) are sufficient for every access rule in this feature.
- The single shared GCS bucket (`{project}-staff-profile-photos`) is not split into multiple buckets by this feature — lifecycle rules are scoped by object-path prefix within the one bucket, consistent with how every existing storage port already shares it.
- Content-licensing / photo-consent flags (`Contract.Consent.PhotosInternal` etc., feature 007) are unchanged by this feature — a parent's download eligibility for a group photo already depends on the same consent gate `GetParentGroupActivityGalleryQuery` enforces today; this feature does not alter consent semantics, only adds a download variant of an already-consent-gated view.
