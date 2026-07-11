# Feature Specification: Group Activities

**Feature Branch**: `009b-group-activities`

**Created**: 2026-07-10

**Status**: Draft

**Input**: User description: "Let caregivers record group-level activity moments — garden time, a visiting musician, a drawing session, a walk, a birthday celebration — with a description and optional photos. Parents see these in the parent app and in the daily report."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Caregiver records a group activity (Priority: P1)

A caregiver on the room tablet taps "Activiteit toevoegen" from the group home screen, picks an activity type (e.g. "Buiten"), edits the pre-filled title if needed, optionally adds a description and up to 10 photos, and saves. The activity appears immediately in the group's timeline alongside individual child events.

**Why this priority**: This is the entire feature's reason to exist — without capture, there is nothing for parents or directors to see. It must work standalone, offline included, since it rides the same tablet used for child events.

**Independent Test**: On the caregiver tablet, create a group activity with a title and two photos while online; confirm it appears in the group timeline with its photos. Repeat while offline; confirm it queues and appears with a pending-sync indicator, then syncs on reconnect.

**Acceptance Scenarios**:

1. **Given** a caregiver is on the room tablet's group home screen, **When** they tap "Activiteit toevoegen", pick a type, and save with no description or photos, **Then** the activity is created with just a title and appears in the group timeline.
2. **Given** a caregiver is creating an activity, **When** they attach 3 photos and save, **Then** all 3 photos are uploaded, resized, and visible on the activity in the group timeline.
3. **Given** a caregiver attempts to attach an 11th photo, **When** they try to add it, **Then** the app blocks the addition and shows a message that 10 is the maximum.
4. **Given** the tablet has no network connection, **When** a caregiver creates an activity with text only, **Then** the activity appears immediately in the local timeline marked "pending sync" and is queued for upload.
5. **Given** the tablet has no network connection, **When** a caregiver creates an activity with photos, **Then** the activity's text/metadata queue immediately and the photos show a "Foto's worden geüpload…" indicator, resuming automatically once connectivity returns.

---

### User Story 2 - Parent sees group activities in the daily feed (Priority: P1)

A parent opens the daily report for their child and sees a dedicated section listing the group activities recorded that day for their child's group — title, description, and photos (subject to their photo consent), ordered chronologically among themselves.

**Why this priority**: This is the direct payoff promised to parents and is equally load-bearing as capture — a feature that captures activities nobody sees delivers no value.

**Independent Test**: As a parent whose child is in a group with a recorded activity today, open the daily report and confirm the activity appears with its description and photos in chronological position among the day's events.

**Acceptance Scenarios**:

1. **Given** a group activity was recorded today for a parent's child's group, **When** the parent opens today's daily report, **Then** the activity appears in a dedicated "activities" section of the report with its title, description, and timestamp, ordered chronologically relative to any other activities that day.
2. **Given** the parent's active contract has `photos_internal = true`, **When** they view an activity with photos, **Then** the photos are visible.
3. **Given** the parent's active contract has `photos_internal = false` (or no active contract), **When** they view an activity with photos, **Then** the title and description are visible but the photos are not shown.
4. **Given** a child was moved to a different group partway through the day, **When** the parent views that day's report, **Then** only activities recorded for the group the child belonged to at the activity's `occurred_at` are shown.

---

### User Story 3 - Parent browses the monthly activity gallery (Priority: P2)

A parent opens a "Galerij" tab and sees all group-activity photos from the current month for their child's group, browsable as a simple photo grid, subject to the same consent rule as the daily feed.

**Why this priority**: A meaningful enrichment of the emotional/reassurance goal (per `reference-products.md`'s Famly/ClassDojo principles) but not required for the core "see today's activity" loop that User Story 2 already delivers.

**Independent Test**: As a parent with photo consent, open the Galerij tab and confirm it shows every group-activity photo from the current calendar month for their child's group(s), and that a child without consent shows the correct filtered result.

**Acceptance Scenarios**:

1. **Given** a parent has photo consent and their child's group had 3 activities with photos this month, **When** they open the Galerij tab, **Then** they see all of those photos in a single grid, most recent first.
2. **Given** a parent does not have photo consent, **When** they open the Galerij tab, **Then** it shows an empty state explaining that photo consent has not been given, rather than an empty grid that looks broken.
3. **Given** a parent has two children in two different groups, **When** they view the Galerij tab, **Then** photos from both children's groups are shown (each gated by that child's own contract consent).

---

### User Story 4 - Director moderates group activities (Priority: P2)

A director viewing a location's group timeline in the web admin sees group activities alongside individual events and can delete an activity that was recorded in error or contains an inappropriate photo.

**Why this priority**: A necessary safety valve (moderation of parent-visible content) but not on the critical path of the feature working at all — activities are correct far more often than not.

**Independent Test**: As a director, open the group timeline for a location/date with a recorded activity, delete it, and confirm it disappears from the timeline and from the parent app's daily feed.

**Acceptance Scenarios**:

1. **Given** a director is viewing a group's timeline, **When** they select an activity and confirm deletion, **Then** the activity (and its photos) are removed from the timeline and no longer appear in any parent-facing feed.
2. **Given** an activity was deleted by a director, **When** a caregiver views the group timeline on the tablet, **Then** the deleted activity no longer appears.

---

### Edge Cases

- A caregiver records an activity for the wrong group — resolved by director deletion (User Story 4); the caregiver re-creates it on the correct group. No "move to different group" edit path exists.
- A parent has partial consent (e.g. `photos_internal = true`, `photos_website = false`) — in-app viewing (this feature) is governed only by `photos_internal`, since nothing in this feature performs external sharing or bulk export; the other four consent flags are not read by this feature.
- A child is absent on the day of a recorded activity — the system cannot verify which children appear in an uploaded photo, so no technical enforcement exists; the caregiver UI carries a reminder ("Foto's mogen enkel aanwezige kinderen tonen") at upload time.
- Two caregivers on the same tablet, both checked in — the activity's `recorded_by` records every caregiver checked into that room/group at `occurred_at` (mirrors `ChildEvent.RecordedBy` from feature 009), not a single author; there is no individual-attribution UI for group activities the way medical events have PIN confirmation, since a shared group moment has no single "administerer" concept to confirm.
- No caregiver is checked in yet when an activity is recorded (opening minutes of the day) — `recorded_by` is an empty list; the activity is still created (never blocked), matching `ChildEvent`'s precedent.
- An activity is recorded, then the child's group assignment changes later the same day — the activity is scoped to the group it was recorded against, not to individual children, so this does not affect which parents see it; only a child's group membership *at the time the parent views a given day* determines what's shown (see User Story 2, Acceptance Scenario 4).
- A photo fails to resize/upload after several sync retries — the activity itself (text/metadata) is still visible; the failed photo shows a retry-needed state rather than silently vanishing.
- A director deletes an activity while a caregiver's tablet is still uploading photos for it (race) — deletion proceeds normally; any photo that finishes uploading after its parent activity was deleted is rejected (the activity no longer exists) rather than silently reappearing, consistent with FR-011's "removed from every surface" guarantee.
- A parent has two children (e.g. twins) in the same group — an activity recorded for that group is shown once in the parent's feed/gallery, not duplicated once per matching child.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow a caregiver, authenticated via the room tablet's device token (no individual caregiver identity required), to create a group activity with an activity type, a title, an optional description, and zero or more photos.
- **FR-002**: The system MUST provide six activity types (outdoor, creative, music, story, celebration, other) and pre-fill the title from the selected type while allowing the caregiver to edit it.
- **FR-003**: The system MUST reject an attempt to attach more than 10 photos to a single activity, and reject any individual photo larger than 10MB before resizing.
- **FR-004**: The system MUST resize an uploaded photo to a maximum of 1920px on its long edge and generate a 400px thumbnail for list/grid display.
- **FR-005**: The system MUST store all photos as GCS objects served only via signed URLs — no publicly accessible blob URLs are ever issued.
- **FR-006**: The system MUST populate an activity's `recorded_by` as the set of caregivers checked into that activity's location/group at `occurred_at`, resolved the same way `ChildEvent.RecordedBy` is resolved (feature 009) — never requiring or accepting a client-supplied individual caregiver identity, and never blocking creation when the set is empty.
- **FR-007**: The system MUST make a saved activity visible in the caregiver tablet's group timeline immediately, interleaved chronologically with that group's individual child events.
- **FR-008**: The system MUST make a saved activity visible in the daily report feed of every parent whose child was assigned to that activity's group at the activity's `occurred_at`.
- **FR-009**: The system MUST show an activity's photos to a viewing parent only when that parent's child has an active contract with `photos_internal = true` at the time of viewing; otherwise the activity's title and description remain visible but photos are withheld.
- **FR-010**: The system MUST provide a parent-facing monthly gallery view aggregating all group-activity photos (subject to FR-009's consent rule) for the current calendar month, across all groups the parent's child(ren) belong(s) to.
- **FR-011**: The system MUST allow a director to delete a group activity (and its photos), after which it is removed from every surface (caregiver timeline, parent daily feed, parent gallery, director timeline).
- **FR-012**: The system MUST allow a group activity to be created while the caregiver tablet is offline: text/metadata queue via the existing offline-write queue (feature 008) and sync on reconnect; photos queue and upload separately, showing a distinct "uploading" indicator until each succeeds.
- **FR-013**: All user-facing strings (activity type labels, form fields, empty states, consent messaging, upload/sync indicators) MUST use i18n keys with NL/FR/EN translations — no hardcoded copy.
- **FR-014**: The system MUST NOT surface any editing capability for a group activity beyond director deletion — no update/amend path exists in this feature (a mis-recorded activity is deleted and re-created, per the Edge Cases section).

### Key Entities

- **Group Activity**: A single shared moment recorded once for an entire group (not per-child). Has a type, title, optional description, timestamp, the group and location it belongs to, and the set of caregivers present when it was recorded. Distinct from a `ChildEvent`, which is always scoped to one child.
- **Group Activity Photo**: Zero-to-ten photos attached to one group activity. Each has a stored (resized) image, a generated thumbnail, and an optional caption. Deleted together with its parent activity.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A caregiver can record a group activity with two photos in under 30 seconds of active interaction on the tablet.
- **SC-002**: A parent can see a recorded group activity (text) in their daily report within the same sync cycle as an individual child event recorded at the same time — no separate/slower path.
- **SC-003**: 100% of group-activity photos shown to a parent respect that parent's current photo-consent setting — zero photos are ever shown to a parent without `photos_internal` consent.
- **SC-004**: A director can remove an inappropriate or mis-recorded activity from every parent-facing surface in a single action, with no propagation delay beyond normal sync/cache timing.
- **SC-005**: An activity recorded fully offline (text + photos) reaches every affected surface correctly once the tablet reconnects, with no data loss and no duplicate activity created.

## Assumptions

- **`recorded_by` resolution** (spec-note, resolving an inconsistency in the originating backlog prompt): the backlog's own draft schema listed `recorded_by UUID REFERENCES users(id) NOT NULL`, which assumes a single individually-authenticated caregiver — but the caregiver tablet authenticates via feature 008a's device token, carrying no individual caregiver identity on the request. This spec follows feature 009's already-resolved precedent instead: `recorded_by` is the set of caregivers checked into the room/group at the time of the activity (feature 008a's `RoomShift` log), stored as a list, and may be empty. No `AdministeredBy`-style PIN-confirmation step applies here — group activities have no single "administerer" concept the way a medication event does.
- **Photo consent scope**: "parents only see photos of children they have consent for" is interpreted, per the original prompt's own framing, as gating on the *viewing parent's own* consent (their child's contract `photos_internal` flag) — not an attempt to identify which specific children appear in a given uploaded photo, which is not technically possible from an uploaded image alone. This mirrors the Edge Cases note about absent children: enforcement is procedural (a UI reminder to caregivers), not technical, at upload time.
- **In-app-only consent gate**: only the `photos_internal` consent flag governs visibility in this feature. The other four consent flags on `Contract.Consent` (`photos_website`, `photos_social_media`, `video_internal`, `photos_press`) are not read, since this feature does not publish, export, or externally share any photo — that remains out of scope until a feature actually performs external distribution.
- **No group timeline UI exists yet** in either the caregiver app or the director web app (confirmed: neither surface currently renders a group-scoped, multi-child-event timeline). This feature is the first to build that view on both platforms; it is scoped as new UI, not an extension of an existing screen.
- **No multi-photo, resizing, or thumbnailing capability exists yet** in the current photo-storage port (`IProfilePhotoStorage` supports exactly one deterministic photo per subject, no resize). This feature requires new storage/processing capability rather than reusing that port as-is; planning should decide whether to extend it or add a parallel port, but either way this is net-new backend work, not a thin wrapper.
- Directors can delete but not edit an activity's content (title/description/type) — full edit capability is not requested by the source prompt and is deferred as unnecessary scope; if a correction is needed the activity is deleted and re-recorded.
- The monthly gallery (User Story 3) shows the current calendar month only — no month-picker/history browsing is required by the source prompt; this is a reasonable default for an MVP gallery view, consistent with "Out of scope" not mentioning historical browsing. The calendar month boundary uses `Europe/Brussels`, matching the existing `BelgianCalendarDay` convention `GetDailySummaryQuery` (feature 009) already uses for its day boundary — not left as an unstated, timezone-ambiguous "current month."
- Deleting an activity is a hard delete (activity + photos removed from GCS and the database), not a soft-delete/audit-trail pattern — unlike `ChildEvent`, which prefers soft-delete for caregiver/director edits. This asymmetry is deliberate: FR-014 already restricts deletion to directors only (a moderation action against inappropriate content, e.g. an inappropriate photo), so retaining a "deleted but visible in an audit view" record of that content works against the reason deletion exists in the first place.
- `occurred_at` defaults to the moment of save (no time picker) — consistent with the "reduce typing, use defaults intelligently" principle already applied to routine `child_events` quick-entry (009); a caregiver logging an activity is logging it as it happens, not backdating it.
- The caregiver tablet's group timeline shows today only, matching the room home screen's existing "today's classroom" scope (008a/009) — no historical browsing on the tablet.
- The director web group timeline defaults to today with a date picker to view prior days, matching the date-range browsing pattern already shipped for attendance history (010) — the first precedent for a director-web "group activities" view, since no such screen exists yet (see Assumptions above).
- The Galerij gallery only surfaces activities that have at least one photo (it is a photo album, per the original prompt's framing) — a text-only activity still appears in the daily report feed (User Story 2) but not in the gallery grid (User Story 3).
- **Parent app is a separate codebase** (`parent-mobile/`, a distinct Expo project from the caregiver app `mobile/`) — confirmed during planning. Its existing "daily report" (`DailySummaryCard`, feature 009/013) is a card of aggregate counts plus an unordered text bullet list of individual `activity`-type child-event descriptions — there is no per-event chronological timeline UI on the parent side today (unlike the caregiver tablet's `EventTimeline`). "Appears in the daily report" (User Story 2) therefore means a new, dedicated activities section within that existing card — each activity chronologically ordered relative to other activities that day — not a merge into a single mixed feed with naps/meals/diaper events, since no such merged feed exists to extend.

## Product Context

### Feature Type

Mixed — API-backend capability plus user-facing UI on all three surfaces (caregiver tablet, parent mobile, director web).

### Primary Consumer

Caregiver (creates), Parent (views), Director (moderates).

### Workflow Boundary

Belongs to **Daily Child Care** (`Workflows/dailycare.md` — Activities, Photos, Daily reports), and touches **Parent Communication** (daily report feed) and **Classroom Operations** (group timeline). Actors: Caregiver (create, device-token auth), Parent (view daily feed + gallery), Director (delete), System (resize/thumbnail, consent filtering, offline sync). Data flow: caregiver tablet → device-token-authenticated write → tenant schema + GCS → parent app reads via a consent-filtered query; director web reads/deletes via existing role-based auth. Cross-platform impact: all three surfaces, backend-only capability underlies all of them.

### User Impact

This enables a caregiver to record a shared group moment once instead of duplicating it per child, resulting in parents seeing a richer, more emotionally engaging picture of their child's day beyond individual care events.

### UX Requirements

**Caregiver tablet** — Persona: a busy, standing caregiver, often mid-task. Platform: landscape tablet, kiosk mode (008a), device-token auth, no individual login. User job: capture a shared moment fast, with minimal typing, without leaving the group view. Success criteria: activity creation takes under 30 seconds of active interaction (SC-001); large touch targets (48pt minimum) throughout the type picker and photo attach flow. Main flow: group home screen → "Activiteit toevoegen" → type picker (icon-based) → pre-filled title (editable) → optional description → optional photos (camera or gallery) → save → activity appears in group timeline. Loading state: photo thumbnails show a resize/upload progress indicator, not a blocking spinner over the whole form. Empty state: group timeline with no activities yet shows nothing extra (per design-system.md, don't decorate an empty state that's simply "nothing has happened yet" — individual events already establish the timeline is populated in the normal case). Error state: a failed photo upload shows a retry-needed badge on that photo without blocking the rest of the activity. Offline: text/metadata queue immediately (optimistic, per FR-012); photos show "Foto's worden geüpload…" and resume on reconnect (per design-system.md's `warning`/`info` banner tokens for pending/syncing states). Accessibility: 48pt touch targets, icon+text pairing on the type picker (never color alone), reduced-motion-safe.

**Parent mobile app** (`parent-mobile/`, a separate Expo project from the caregiver app — see Assumptions) — Persona: an emotionally invested parent checking in on their child's day. Platform: portrait mobile, low-density per design-system.md, `Tabs`-based navigation (`app/(app)/_layout.tsx`). User job: quickly answer "what happened with my child today?" and browse a reassuring record of group moments over time. Success criteria: activity appears in the same daily-report sync cycle as any other event (SC-002); zero photos ever shown without consent (SC-003). Main flow: daily report (`DailySummaryCard`) → a new "Activiteiten" section listing each activity (title/description/photos), chronologically ordered among themselves → a new "Galerij" tab → month grid of photos, most recent first. Loading state: standard card/grid loading pattern already established by feature 013's daily report and messaging screens — no new pattern introduced. Empty state (no photo consent): an explicit sentence explaining consent hasn't been given (User Story 3, Acceptance Scenario 2) — never a silently empty grid that reads as broken. Error state: a failed photo load in the gallery grid shows a placeholder tile, not a broken-image icon. Offline: parent app is read-only for this feature; standard read-cache behavior from feature 008/013 applies, no new offline design needed. Accessibility: photos have alt text derived from the activity title/caption where available; warm, natural-language copy per platform-rules.md's parent-mobile tone ("In de tuin vandaag" rather than "Group activity recorded").

**Director web app** — Persona: an administrator scanning for operational or moderation issues, not a first-time viewer of the day's care. Platform: desktop web, high density, keyboard-navigable. User job: spot and remove an inappropriate or mis-recorded activity quickly from the group timeline. Success criteria: a single confirm action removes the activity from every surface (SC-004). Main flow: group timeline (new view, per Assumptions) → activity rendered inline with individual events → row action → confirm delete. Loading/empty/error: standard table/timeline patterns already used elsewhere in `web/` (e.g. attendance history, staff list) — no new pattern; an empty group/date (no events or activities) uses design-system.md's standard empty state (an icon plus one short sentence), not a blank timeline. Accessibility: full keyboard reachability for the delete action and a visible focus ring, per platform-rules.md's Director Web section.

### Technical Requirements

API impact: new endpoints for create/list/delete of group activities and photos, plus a consent-filtered read for the parent app and a new "group timeline"/"daily feed" aggregation query (no such query exists today — feature 009's `GetDailySummaryQuery` is per-child only). Data-model impact: two new tenant-schema tables (`group_activities`, `group_activity_photos`). Security considerations: caregiver-tablet writes authenticate via the `DeviceAuthenticated` device-token policy (008a/009 precedent — the dual-scheme `DeviceOrDirector` policy is not needed here, since this feature has no device-token-authenticated edit path to combine with a director path, unlike `ChildEvent`'s `PATCH`/`DELETE`), resolving `recorded_by` server-side from the room-shift log — never client-supplied; photo consent enforced at read time from `Contract.Consent.PhotosInternal` (007), never at upload time; signed GCS URLs only, no public blob URLs. Performance considerations: server-side photo resize (max 1920px long edge) and thumbnail generation (400px) are new backend capability (the existing `IProfilePhotoStorage` port supports neither multi-photo-per-subject nor resizing); activity/gallery queries must be paginated. Testing requirements: happy-path create/list/delete across all three surfaces; consent-filtering correctness (full/no consent, since only `photos_internal` is read); offline queue integration for activity creation (text separately from photos); recorded_by resolution with 0/1/2+ checked-in caregivers.
