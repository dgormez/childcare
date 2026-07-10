# Feature Specification: Parent Communication

**Feature Branch**: `013-parent-communication`

**Created**: 2026-07-10

**Status**: Draft

**Input**: User description: "Build two-way communication between parents and the KDV, plus the parent's daily summary of their child's day — thread-based messaging, director announcements, an in-app notification centre, push notifications, and a parent daily summary aggregated from child_events."

## Clarifications

### Session 2026-07-10

- Q: How does a Contact record (feature 006) become a parent login account (TenantUser)? → A: Director-invited — a director explicitly invites a parent from a child's contact list (same invite-only pattern as staff, feature 005; matches the platform-wide "no open registration" rule from features 001/003/005). The invite links to the Contact record by matching email at invitation time.
- Q: Should two parents of the same child share ONE message thread, or does each parent contact get their own separate thread with the KDV? → A: Shared family thread — both parents (if both have accounts) are participants on the same child-scoped thread; either can see and reply to the full conversation.
- Correction (research, not a question — no reasonable alternative existed): the original prompt's daily-summary scope named "photos" alongside naps/bottles/meals/etc., but feature 009's own clarification session explicitly deferred all photo attachment (event photos and child+date photos) to a future feature — no `Photo` entity or attachment mechanism exists anywhere in the domain yet. Photos are removed from this feature's daily-summary scope; the summary aggregates only data that actually exists (naps, bottles, diaper changes, mood, temperature, medication, activities). Re-added automatically once a future feature ships photo attachment — no rework needed here since the summary already aggregates by event type generically.

## User Scenarios & Testing *(mandatory)*

### User Story 0 - Director invites a parent to the app (Priority: P1)

Before any parent can see a summary or send a message, they need an account. A director invites a parent from a child's contact list — the same invite-only pattern already used for staff accounts — so a parent can then complete registration and log in to the new parent app.

**Why this priority**: Every other story in this feature is unreachable without a parent account existing first. This is the prerequisite, not an optional add-on.

**Independent Test**: As a director, invite a contact with `can_pickup = true` and an email on file to the parent app; confirm an invitation is created and, using the resulting token, the invited contact can complete registration and log in.

**Acceptance Scenarios**:

1. **Given** a child has a contact with `can_pickup = true` and an email on file, **When** a director sends a parent-app invitation to that contact, **Then** an invitation is created and (conceptually) delivered to that email address.
2. **Given** a valid, unexpired invitation, **When** the invited person completes registration (sets a password, or uses Google/Apple sign-in), **Then** a parent account is created, linked to that Contact record, and they can log in.
3. **Given** an invitation has expired or was already used, **When** someone attempts to complete it, **Then** registration is rejected with a generic, non-enumerable error (consistent with feature 001's invitation-security precedent).
4. **Given** a contact has no email on file, **When** a director views that contact, **Then** the invite action is unavailable/disabled rather than silently failing.
5. **Given** a child has two contacts who both qualify (`can_pickup = true`, email on file), **When** a director invites both, **Then** each gets their own independent account, and both later become participants on the same shared threads for that child (see User Story 2).

---

### User Story 1 - Parent sees today's summary (Priority: P1)

A parent opens the app during their workday and immediately sees an aggregated summary of what happened with their child today — naps, bottles, diaper changes, mood, temperature, medication, activities — without hunting through a raw event log.

**Why this priority**: This is the single highest-value moment in the whole feature — the "what happened with my child today?" reassurance job every reference product (Famly, ClassDojo) treats as the core home-screen action. It also requires no new writes, so it can ship and be validated before messaging exists.

**Independent Test**: Seed a child with a mix of `visible_to_parent = true` and `visible_to_parent = false` events for today, log in as that child's parent contact, and confirm the summary shows only the visible events, correctly aggregated, with the internal-only events completely absent.

**Acceptance Scenarios**:

1. **Given** a child has today's naps, a bottle, a diaper change, and a mood entry recorded with `visible_to_parent = true`, **When** the parent opens the app, **Then** the home screen shows a summary counting/displaying all of them for that child.
2. **Given** a child also has an internal staff note recorded with `visible_to_parent = false`, **When** the parent views the summary, **Then** that note never appears, in any form.
3. **Given** a parent has two children enrolled at the same KDV, **When** the parent opens the app, **Then** both children's summaries are shown, clearly separated by child.
4. **Given** a child has no events recorded yet today, **When** the parent opens the app, **Then** the summary shows a clear "no updates yet" state rather than an empty-looking screen.

---

### User Story 2 - Parent messages the KDV and receives a reply (Priority: P1)

A parent sends a message to the KDV about their child (e.g. "can you make sure she takes her medicine at 2pm?"). A director or staff member sees it and replies. The conversation continues as a thread, like an email conversation, and both sides can see the full history.

**Why this priority**: Two-way messaging is the other half of the "I need to tell the KDV something" job and the feature's core two-way trust mechanism — without it this is just a read-only summary feature.

**Independent Test**: As a parent, start a new thread and send a message; as a director on web admin, see the new thread appear, open it, and reply; confirm the parent sees the reply appear in the same thread.

**Acceptance Scenarios**:

1. **Given** a parent is viewing their child's profile, **When** they start a new message thread and send a message, **Then** the thread is created and the message is visible to both the parent and every director/staff participant with access.
2. **Given** an existing thread with unread messages, **When** a director opens it from web admin and sends a reply, **Then** the parent sees the reply in the same thread, in chronological order.
3. **Given** a parent has multiple message threads, **When** they open their message list, **Then** they see all their threads with the most recently active first, and can tell which have unread replies.
4. **Given** a parent who is not a contact/participant of a thread, **When** they attempt to access it (directly or via any query), **Then** access is denied.
5. **Given** a child has two parent contacts who both have accepted parent-app invitations, **When** either parent sends or receives a message on a thread tied to that child, **Then** both parents are participants on the same shared thread and both see the full conversation — no separate thread per parent.

---

### User Story 3 - Director sends an announcement (Priority: P2)

A director needs to tell every family at a location, or every family in a specific group, something at once (e.g. "closed early Friday for a staff training") without messaging each parent individually and without inviting a flood of individual replies.

**Why this priority**: High-value but lower frequency than 1:1 messaging and the daily summary; a director could work around its absence in v1 by messaging threads one at a time (painful, but not blocking), whereas a missing daily summary or missing messaging would leave the feature without its core value.

**Independent Test**: As a director, compose and send an announcement scoped to one location; confirm every parent contact with a child currently enrolled at that location receives it (visible in their notification centre and, if they have a push token, as a push notification), and confirm a parent cannot reply to it.

**Acceptance Scenarios**:

1. **Given** a director composes an announcement scoped to a location, **When** they send it, **Then** every parent/guardian contact of a currently-enrolled child at that location receives it.
2. **Given** a director scopes an announcement to a specific group instead of a whole location, **When** they send it, **Then** only contacts of children currently assigned to that group receive it.
3. **Given** a parent receives an announcement, **When** they view it, **Then** they cannot reply to it — it is read-only, distinct from a two-way message thread.
4. **Given** a location or group has zero currently-enrolled children at send time, **When** the director sends the announcement, **Then** the system completes the send with zero recipients rather than erroring.

---

### User Story 4 - Parent sees all notifications in one place (Priority: P2)

A parent wants a single place to check everything that needs their attention — new messages, announcements, and alerts like a temperature notice — rather than having to remember which screen each type lives on.

**Why this priority**: Ties the other stories together into one coherent "what needs my attention" surface, and is the fallback path for any parent who missed a push notification (no app installed, token invalid, phone off) — but the underlying events (messages, announcements) already have their own screens, so this is additive rather than blocking.

**Independent Test**: Trigger a new message, an announcement, and a temperature alert for the same parent; open the notification centre and confirm all three appear, each linking to the right underlying content, and confirm marking one as read doesn't affect the others.

**Acceptance Scenarios**:

1. **Given** a parent receives a new message reply, an announcement, and a temperature alert, **When** they open the notification centre, **Then** all three appear, most recent first, each identifiable by type.
2. **Given** an unread notification, **When** the parent opens/taps it, **Then** it is marked read and navigates to the relevant thread/announcement.
3. **Given** a parent never installed the app or has no valid push token, **When** an announcement is sent to them, **Then** it is still waiting in their notification centre the next time they log in.

---

### User Story 5 - Parent and KDV both get timely push notifications (Priority: P3)

When something happens that needs a parent's attention (new message reply, announcement posted), or a director/staff member's attention (new parent message), the recipient gets a push notification rather than having to keep the app open and checking.

**Why this priority**: A real quality-of-life and responsiveness improvement, but the feature is fully usable without it (both notification centre and message threads work as a manual-check fallback) — this is the layer that makes it feel timely rather than the layer that makes it functional.

**Independent Test**: Send a message to a parent with a registered, valid push token; confirm a push notification is received. Send to a parent with an expired/invalid token; confirm the send fails gracefully, is logged, and the notification still appears in-app.

**Acceptance Scenarios**:

1. **Given** a parent has a valid registered push token, **When** they receive a new message or an announcement, **Then** a push notification is sent to their device.
2. **Given** a push send fails (expired/invalid token), **When** the failure occurs, **Then** it is logged, the sender's action is not blocked or shown as failed, and the notification still appears in the parent's in-app notification centre.
3. **Given** a parent uninstalls and reinstalls the app, **When** they log in again, **Then** their new push token replaces the old one for future notifications.
4. **Given** a director or staff member has replies pending, **When** a parent sends a new message, **Then** a director/staff-facing notification (in-app on web admin, at minimum) makes the new message visible without requiring them to poll the thread list manually.

---

### Edge Cases

- A parent has two children enrolled at the same KDV: daily summary shows both, clearly separated (User Story 1).
- An event exists with `visible_to_parent = false`: never appears in the daily summary or anywhere in the parent-facing API, regardless of how the query is written (User Story 1) — this must be enforced structurally (see FR-002), not just by the query the daily-summary endpoint happens to use today.
- A parent starts a message thread about a specific child they are not actually a contact of: rejected — a parent may only start/participate in threads tied to their own children (or a general, non-child-specific thread with the KDV).
- Two staff members reply to the same parent thread within seconds of each other: both replies are preserved in order; no reply is lost or overwritten.
- An announcement is sent to a location/group with zero currently-enrolled children: completes with zero recipients, not an error (see Story 3, Scenario 4).
- A push notification token is expired or invalid at send time: logged, does not crash or block the triggering action, in-app notification still created.
- A parent uninstalls and reinstalls the app: new token replaces the old one on next login, per-contact (not per-device-ever-seen).
- A contact with no email/push token at all: unaffected by push delivery; still reachable via in-app notification centre on next login.
- A message thread has no director/staff participant yet (KDV hasn't triaged it): the parent's message is still saved and visible to them; it is immediately visible to every director/staff member at the organisation, since thread visibility is organisation-wide rather than requiring an explicit staff participant to be added first (FR-004).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-000a**: A director MUST be able to invite a child's contact (who has `can_pickup = true` and an email on file) to the parent app; the invite action MUST be unavailable for a contact with no email.
- **FR-000b**: An invited contact MUST be able to complete registration via a valid, unexpired invitation (setting a password, or via Google/Apple sign-in) to create a parent account linked to that Contact record; an expired or already-used invitation MUST be rejected with a generic, non-enumerable error.
- **FR-000c**: A parent account MUST NOT be creatable through any path other than a director-issued invitation — no open/self-registration for parents, consistent with every other role in this system.
- **FR-001**: The system MUST provide an aggregated daily summary per child, per calendar day, showing naps, bottles/feeding, diaper changes, mood, temperature, medication administered, and activities recorded for that child that day. (Photos are explicitly out of scope — see Clarifications; no photo-attachment mechanism exists in the domain yet, deferred by feature 009's own prior clarification.)
- **FR-002**: The daily summary and every other parent-facing view MUST exclude any underlying record marked internal/staff-only (`visible_to_parent = false`), enforced at the data-access layer so no new or future parent-facing query can accidentally leak internal-only records.
- **FR-003**: A parent MUST be able to start a new message thread addressed to the KDV, either tied to a specific one of their children or as a general (non-child-specific) thread.
- **FR-003a**: When a message thread is tied to a specific child, every parent contact of that child who has an active parent account MUST be a participant on that same thread — a child's parents share one conversation with the KDV, not one each.
- **FR-004**: A director or staff member MUST be able to view and reply to any parent-initiated message thread at their organisation — both threads tied to a specific child and general (non-child-specific) threads, both visible organisation-wide to every director/staff member, not scoped to a particular location or the specific person who first replied.
- **FR-005**: Message threads MUST preserve full history in chronological order, visible identically to every participant.
- **FR-006**: A parent MUST only be able to access threads they are a participant of; access to any other thread MUST be denied regardless of how it is requested.
- **FR-006a**: When a second parent account is later linked to a child who already has an active thread, that parent MUST be added as a participant and gain access to the thread's existing history (not just messages sent after they joined).
- **FR-007**: A director MUST be able to compose and send a one-to-many announcement scoped to either a single location or a single group within a location.
- **FR-008**: An announcement MUST reach every parent/guardian contact of a currently-enrolled child within the announcement's scope at send time **who has an active parent account** (per FR-000a/FR-000b — a contact who was never invited or has not completed registration has no notification centre or push token to reach, and is not a gap this feature introduces; see Assumptions).
- **FR-009**: An announcement MUST be read-only to parents — the system MUST NOT allow a parent to reply to an announcement the way they can reply within a two-way message thread.
- **FR-010**: The system MUST provide a parent-facing in-app notification centre listing, most-recent-first, every notification-worthy event addressed to that parent (new message/reply, announcement, temperature alert), each identifiable by type and linking to its underlying content.
- **FR-011**: A parent MUST be able to mark a notification as read; marking one notification read MUST NOT affect the read state of any other notification.
- **FR-012**: The system MUST send a push notification to a parent's registered device when they receive a new message reply or a new announcement within their scope (one active push token per parent account, per FR-014 — not a multi-device fan-out).
- **FR-013**: The system MUST make a director/staff member aware, without manual polling, that a new parent message has arrived (at minimum, a visible unread indicator in web admin).
- **FR-014**: The system MUST allow a client to register/update a push token for the signed-in user, replacing any previously stored token for that user so a reinstall does not leave a stale token active.
- **FR-015**: If a push notification send fails for any reason (expired/invalid token, delivery error), the system MUST log the failure, MUST NOT fail or block the action that triggered it, and MUST still create the corresponding in-app notification.
- **FR-016**: Every user-facing string (message UI, announcement UI, notification centre, push notification titles/bodies) MUST be internationalised (NL/FR/EN); push notification content MUST render in the recipient's own locale preference, not the sender's.
- **FR-017**: A parent MUST only ever see data (summaries, threads, notifications) for their own children and their own account — no cross-family or cross-child data must be reachable through any parent-facing endpoint.
- **FR-018**: Tenant isolation MUST hold throughout — no message, announcement, notification, or summary data is ever reachable across organisations.

### Key Entities

- **Parent Invitation**: A director-issued, signed, time-limited invitation linking a specific Contact record to a not-yet-created parent account; consumed once on successful registration.
- **Message Thread**: A conversation between a family (all of a child's parent accounts, sharing one thread per child) and the KDV, optionally tied to a specific child, with a subject and a set of participants (the parent account(s) plus any director/staff who has engaged with it).
- **Message**: A single entry within a thread — sender, body text, sent time, and a single "read by the other side" marker (not a per-individual read receipt): for a parent-authored message, set on the first director/staff read; for a staff-authored message, set on the first read by any parent participant on that thread.
- **Announcement**: A one-to-many broadcast from a director to every parent contact within a location or group scope, at a point in time; read-only, no reply path.
- **Notification**: An in-app, per-parent-contact record representing something needing their attention (new message, announcement, temperature alert), with a type, a link to its source, and a read state.
- **Push Token**: A per-user registration of a device's push-notification address, always representing the most recently registered device/install for that user.
- **Daily Summary**: A computed (not stored) aggregation of a child's parent-visible events for one calendar day.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-000**: A director can invite a qualifying contact to the parent app and that person can complete registration and log in without director follow-up beyond the initial invite.
- **SC-001**: A parent can see their child's daily summary within one screen/tap of opening the app, with no manual filtering or scrolling through a raw event log required.
- **SC-002**: A parent can send a message and a director/staff member can see and reply to it without either party needing to leave their respective app/admin console.
- **SC-003**: 100% of internal-only (`visible_to_parent = false`) records are absent from every parent-facing view, verified by automated tests covering every parent-facing read path, not just the daily summary.
- **SC-004**: A director can compose and send a location- or group-scoped announcement in a single guided flow (a handful of clicks), reaching every in-scope family without per-family manual selection.
- **SC-005**: A parent with a valid push token receives a push notification for a new message or announcement without needing to have the app open.
- **SC-006**: 100% of push send failures are logged and degrade to an in-app notification rather than a lost or silently dropped notification.
- **SC-007**: A parent never sees data belonging to a child they are not a contact of, verified by automated tests covering thread access, notification access, and summary access.

## Assumptions

- Parent account provisioning follows the director-invitation model (see Clarifications) — the same invite-only pattern as feature 005 (staff), and consistent with the platform-wide "no open registration" rule (features 001/003/005). A Contact becomes eligible for invitation once it has `can_pickup = true` and an email on file; this reuses feature 006's existing `can_pickup` flag as the eligibility signal rather than introducing a new one.
- The parent-facing client for this feature is a new, standalone parent mobile app (Expo) — no parent mobile app exists yet in this repository; feature 008/008a's `mobile/` app is caregiver-only and out of scope to extend for parent use. This is the first feature to ship real parent-facing UI on any platform.
- Director/staff replies to parent messages happen from the director web admin only. No caregiver-tablet UI ships for messaging (mirrors feature 012's explicit precedent that 008a's kiosk tablet has no personal caregiver session to build personal messaging UI against). The backend still authorizes replies under the existing `StaffOrDirector` policy (consistent with every other feature's authorization pattern) even though, in practice, only directors have a shipped UI surface to use it from in v1.
- Announcements are modeled as their own concept, distinct from message threads (no participant list, no reply capability, one send fans out to many read-only recipient views) — not a special case of a two-way thread.
- "Request approved/rejected" push notifications named in the original feature prompt refer to feature 013a (day reservations), which does not exist yet. The notification system is built generically (a typed notification with a source reference) so 013a can register a new notification type later without a redesign, but no request-approval notification is triggered by this feature itself.
- The existing generic push-sending mechanism (an Expo push port used by features 009's temperature alerts and 011's closure notices) is reused for this feature's message/announcement pushes rather than building a second push-sending path.
- The existing daily-summary aggregation logic (built by feature 009, already filtering on `visible_to_parent`) is reused and extended (activities) rather than rebuilt from scratch; it is newly exposed to parents through this feature's own parent-authenticated endpoint, since no parent-facing endpoint for it existed before. Photos are excluded per the Clarifications correction above.
- The parent app has no offline read/write infrastructure in v1 — unlike the caregiver app (feature 008), messaging and the daily summary require connectivity. This is a reasonable v1 scope reduction: a parent checking in during their workday is expected to have normal connectivity, unlike a caregiver mid-shift on a room tablet.
- A parent with no email or push token on file is not blocked from anything — they simply rely on the in-app notification centre when they next open the app, consistent with feature 020's later (unshipped) precedent for the same fallback pattern.
- Day reservation request submission UI (referenced as "decide at plan time" in the original prompt) is deferred entirely to feature 013a — this feature ships no request-submission UI of any kind.
- Parent account access continuity when a child departs the KDV or a contact is deactivated is explicitly out of scope for v1 — this feature introduces the first parent accounts, so there is no existing deactivation mechanism to hook into yet, and no prior feature's contact/child soft-delete flow was designed with a linked login account in mind. A parent's access is not automatically revoked by this feature when their child leaves; that is deferred to a future feature once this gap is felt in practice, rather than guessed at now.
