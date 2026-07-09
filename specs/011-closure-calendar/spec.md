# Feature Specification: Closure Calendar

**Feature Branch**: `011-closure-calendar`

**Created**: 2026-07-09

**Status**: Draft

**Input**: User description: "Build the KDV closure calendar — each location's own holiday and closure schedule, independent of school holidays."

## Product Context

### Feature Type

Mixed.

### Primary Consumer

Director.

### Workflow Boundary

This feature belongs to Attendance & Presence, Parent Communication, Reporting & Management, and the future Billing & Payments workflow.

Actors: Director creates and maintains location-specific closure days; System applies published closures to attendance, notifies affected parents, and exposes the data for invoicing; Parent receives closure or cancellation communication; Caregiver is blocked from checking children in on closure days.

Actions: Director views a full-year calendar per location, adds/edits/removes closure days, chooses whether publishing should notify parents, and sees warnings when a closure affects existing same-day attendance.

Data Flow: Closure days are stored per tenant schema and location, linked to the creating director, consumed by attendance for `closure` status generation/check-in blocking, exposed to future invoicing as a non-billable day source, and recorded as parent-facing in-app notification/messages when published or cancelled.

Outputs: Location-year closure calendar, closure-day records, attendance closure records for enrolled children, push notification requests, parent in-app messages, and an invoicing query surface for feature 014.

Cross-platform Impact: Director web gains the calendar management UI. Backend gains storage, APIs, attendance integration, and notification/message records. Caregiver tablet only observes the existing attendance blocking/closure status behavior. Parent mobile receives notification/message data when the parent app exists; email fallback is out of scope.

### User Impact

This enables directors to publish location-specific KDV closure days, resulting in accurate attendance, parent communication, and future billing exclusions.

### UX Requirements

Persona: A director managing operational planning for one or more childcare locations.

Platform: Director web, desktop-first at 1280px and above, with dense calendar/list controls and keyboard-reachable actions.

User job: Maintain a trustworthy annual closure schedule, publish urgent or planned closures, and avoid accidental attendance or parent-communication mistakes.

Success criteria: A director can identify all closures for a selected location/year, add or edit a closure without leaving the page, and understand whether parents will be notified before publishing.

Main flow: Select location and year, inspect highlighted closure days, add/edit/remove a closure, review any warning about existing attendance, publish, and receive confirmation that attendance and parent notifications were processed.

Loading/empty/error states: Calendar loading shows a compact skeleton or progress state; no closures shows a natural empty sentence with an icon; validation errors point to the invalid field; notification failures are visible without losing the saved closure.

Accessibility: Calendar days and actions must be keyboard reachable with visible focus. Color highlighting must be paired with text/icon indicators. Forms and warnings must be announced semantically.

Offline behavior: Director web requires network access for writes. Failed writes keep entered values on screen and do not create optimistic closure records.

### Technical Requirements

API impact: Add director-only closure calendar endpoints for listing by location/year, creating, updating, deleting/cancelling, publishing, and querying closure days for attendance/invoicing consumers.

Data-model impact: Add a tenant-schema `kdv_closure_days` entity with unique `(location_id, date)`, type, label, notify flag, notification timestamp, cancellation metadata, creator, and audit timestamps. Add parent in-app closure message records only if no reusable message store exists.

Security considerations: All closure endpoints require `DirectorOnly`, tenant-scoped access, location ownership validation, and no cross-tenant reads. Parent notification recipients are derived from enrolled children at the closure location.

Performance considerations: A location-year calendar query must be bounded to one location and one calendar year. Publishing must handle all enrolled children at the location without duplicate parent messages.

Testing requirements: Cover director-only authorization, per-location uniqueness, past-date rejection, same-day extraordinary closure, attendance closure generation/blocking, notification/cancellation behavior, and future invoicing query semantics.

## Clarifications

### Session 2026-07-09

- Q: Does creating a closure record automatically notify parents, or is notification tied only to publish? -> A: Notification is tied only to publish; directors may draft/edit closure records before publishing.
- Q: What should happen if no reusable parent-message store exists before feature 013? -> A: Add a minimal tenant-scoped closure message store for one-way parent notices, leaving two-way messaging to feature 013.
- Q: How should same-day closures with already checked-in children be handled? -> A: Require explicit director confirmation, preserve audit evidence of the prior attendance state, then apply closure status according to the confirmed closure.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Maintain Location Closure Calendar (Priority: P1)

A director manages the annual closure schedule for one location and sees closure days highlighted in a year calendar.

**Why this priority**: This is the core administrative value; without location-specific closure records there is no attendance, notification, or invoicing integration.

**Independent Test**: Can be tested by creating holiday/training/extraordinary closures for one location/year, then listing the calendar and verifying only that location's closures appear with type, label, date, and notification status.

**Acceptance Scenarios**:

1. **Given** a director has two active locations, **When** they create a holiday closure for Location A, **Then** the year calendar for Location A highlights that date and Location B's calendar remains unchanged.
2. **Given** a closure exists for a location/date, **When** the director edits its label, type, or notify preference before publishing, **Then** the updated calendar reflects the changed values and preserves the same unique location/date record.
3. **Given** a director attempts to create a closure in the past, **When** they submit the form, **Then** the system rejects it with an i18n validation error and no closure record is created.

---

### User Story 2 - Publish Closures and Notify Parents (Priority: P2)

A director publishes a closure day and affected parents receive an immediate push notification and in-app message.

**Why this priority**: Closure days affect families immediately, especially same-day extraordinary closures; notification must be trustworthy and auditable.

**Independent Test**: Can be tested by publishing a notify-enabled closure for a location with enrolled children and verifying each affected parent receives exactly one push request and one in-app closure message.

**Acceptance Scenarios**:

1. **Given** a future closure has `notify_parents = true`, **When** the director publishes it, **Then** the system immediately sends parent push notifications, creates in-app messages, and records the notification timestamp.
2. **Given** a closure has `notify_parents = false`, **When** the director publishes it, **Then** attendance integration still applies but no parent push or in-app message is created.
3. **Given** an extraordinary same-day closure is published, **When** parents are enrolled at that location, **Then** notification processing starts immediately and the director sees the publish outcome.

---

### User Story 3 - Apply Closures to Attendance (Priority: P2)

The system converts published closure days into attendance closure records so caregivers cannot check children in and attendance views show closure status.

**Why this priority**: Feature 010 already reserves `status = closure`; this feature completes the promised integration and prevents operational inconsistencies.

**Independent Test**: Can be tested by publishing a closure for a location/date with enrolled children, then querying attendance and attempting check-in for that date.

**Acceptance Scenarios**:

1. **Given** children are enrolled at a location on a published closure date, **When** the closure is published, **Then** the system creates or updates same-day attendance records with `status = closure`.
2. **Given** an attendance record already has `status = closure`, **When** a caregiver attempts check-in, **Then** the existing feature 010 closure-day rejection is triggered.
3. **Given** a director attempts to publish a same-day closure where children are already checked in, **When** they submit, **Then** the system warns about existing attendance and requires explicit confirmation before changing attendance status.

---

### User Story 4 - Cancel a Published Closure (Priority: P3)

A director removes a published closure and affected parents are informed that the closure was cancelled.

**Why this priority**: Published mistakes must be reversible, and parents need explicit cancellation communication to avoid confusion.

**Independent Test**: Can be tested by publishing a closure, cancelling it, and verifying cancellation notifications/messages plus attendance closure cleanup rules.

**Acceptance Scenarios**:

1. **Given** a closure notification has already been sent, **When** the director cancels the closure, **Then** affected parents receive an immediate cancellation push notification and in-app message.
2. **Given** a cancelled closure only produced system-generated closure attendance records, **When** cancellation completes, **Then** those records no longer block check-in.
3. **Given** a cancelled closure has attendance records manually changed after publication, **When** cancellation completes, **Then** the system preserves non-closure attendance data and reports what was not automatically changed.

### Edge Cases

- Duplicate closure for the same location/date is rejected with a clear validation error.
- Same calendar date can be closed for one location and open for another location in the same tenant.
- A closure can be added for today, but publishing must warn if children are already checked in.
- Parent notification delivery can partially fail; the closure remains published and failed recipients are visible for retry or support investigation.
- Removing an unpublished closure does not notify parents.
- Removing a published closure sends a cancellation notification even if the original notification was only partially delivered.
- Closure labels are required and localized copy must use i18n keys; user-entered label content remains director-authored text.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST store KDV closure days per tenant schema and per location with `location_id`, `date`, `label`, `closure_type`, `notify_parents`, `notification_sent_at`, `created_by`, and `created_at`.
- **FR-002**: System MUST enforce uniqueness of closure days by `(location_id, date)`.
- **FR-003**: System MUST support closure types `holiday`, `training`, and `extraordinary`.
- **FR-004**: System MUST reject creation of a closure day before the current `Europe/Brussels` calendar date.
- **FR-005**: System MUST allow a director to list closure days by location and calendar year.
- **FR-006**: System MUST allow a director to create, edit, publish, and cancel/remove closure days only for locations in their tenant.
- **FR-006a**: System MUST support draft closure records; creating or editing a draft MUST NOT notify parents until the director publishes it.
- **FR-007**: System MUST require the `DirectorOnly` policy for every closure calendar write and director-facing read endpoint.
- **FR-008**: System MUST expose a director web year-calendar view where closure days are highlighted and distinguishable by type without relying on color alone.
- **FR-009**: System MUST use i18n keys for every system-authored user-facing string in the director UI, API errors, parent notifications, and parent in-app messages.
- **FR-010**: System MUST send parent push notifications immediately when a closure is published with `notify_parents = true`.
- **FR-011**: System MUST create a parent in-app closure message for each affected parent when a notify-enabled closure is published.
- **FR-012**: System MUST derive affected parents from children enrolled at the closure's location on the closure date.
- **FR-013**: System MUST record `notification_sent_at` when notification/message processing for a closure publish has completed, even if individual deliveries fail.
- **FR-014**: System MUST not send parent notifications or in-app messages when a closure is published with `notify_parents = false`.
- **FR-015**: System MUST create or update attendance records with `status = closure` for enrolled children at the location/date when a closure is published.
- **FR-016**: System MUST preserve feature 010's rule that manual check-in is rejected when a child/location/date attendance record has `status = closure`.
- **FR-017**: System MUST warn and require explicit director confirmation before publishing a same-day closure that would affect children already checked in.
- **FR-018**: System MUST avoid silently overwriting checked-in/present attendance without confirmation and audit evidence.
- **FR-018a**: When an explicitly confirmed same-day closure changes an existing checked-in attendance record to `status = closure`, the system MUST retain audit evidence of the prior attendance state and the confirming director.
- **FR-019**: System MUST send immediate cancellation push notifications and in-app messages when a published, notified closure is cancelled.
- **FR-020**: System MUST remove or release only system-generated closure attendance records when a closure is cancelled, preserving manually edited attendance records.
- **FR-021**: System MUST expose a queryable closure-day source for future invoicing so contracted days falling on closures can be excluded automatically by feature 014.
- **FR-022**: System MUST audit create, edit, publish, cancellation, notification, and attendance-generation actions with actor, timestamp, location, date, and outcome.
- **FR-023**: System MUST surface partial notification failures to directors without rolling back the published closure.
- **FR-024**: System MUST keep closure days tenant-isolated and deny any request where the selected location does not belong to the current tenant.

### Key Entities *(include if feature involves data)*

- **KdvClosureDay**: A location-specific closure date with label, type, notify preference, publish/notification state, cancellation state, creator, and audit timestamps.
- **ClosureNotificationDelivery**: Delivery attempt/outcome for a parent recipient when a closure publish or cancellation sends notifications.
- **ParentClosureMessage**: Parent-visible, one-way in-app message describing a published closure or cancellation; this feature adds the minimal closure-specific store if no reusable message store exists yet.
- **AttendanceRecord**: Existing attendance entity; this feature writes `status = closure` for system-generated closure absences.
- **Location**: Existing tenant-scoped childcare location to which closure days belong.
- **ChildContract/Enrollment**: Existing enrollment source used to determine affected children and parents for a location/date.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can create a closure for a location/year and see it in the year calendar in under 30 seconds from opening the calendar.
- **SC-002**: A location-year closure calendar query returns the year's closure data for one location in under 500ms for a tenant with up to 20 locations and 250 closure records.
- **SC-003**: Publishing a notify-enabled closure creates no duplicate parent in-app messages for parents linked to multiple enrolled children at the same location.
- **SC-004**: 100% of enrolled children at the closed location/date receive `status = closure` attendance coverage unless blocked by an explicitly confirmed checked-in conflict.
- **SC-005**: Same-day extraordinary closure publishing starts parent notification processing within 10 seconds of the director confirming publish.
- **SC-006**: Cancelling a previously notified closure creates a cancellation message for every parent who was in the affected-recipient set.

## Assumptions

- "Today" and "past" are determined by the existing `Europe/Brussels` calendar-day helper used by attendance.
- Parent push notification infrastructure can reuse the existing `IExpoPushSender` abstraction.
- If the codebase has no reusable parent in-app message store yet, this feature adds the smallest tenant-scoped message table needed for closure notices and leaves two-way messaging to feature 013.
- Email notification fallback remains out of scope and is consolidated into feature 020.
- iCal/Google Calendar export remains out of scope.
- Advance-reminder rules remain out of scope.
- Calendar school holidays are not imported; directors enter KDV-specific closure days themselves.
