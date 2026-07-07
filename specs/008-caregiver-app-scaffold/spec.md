# Feature Specification: Caregiver App Scaffold

**Feature Branch**: `008-caregiver-app-scaffold`

**Created**: 2026-07-07

**Status**: Draft

**Input**: User description: "Bootstrap the caregiver Expo app — remove the Habits walking skeleton, wire authentication, set up the API client, establish the navigation structure, and build the shared offline infrastructure layer that all caregiver features (child events, attendance, scheduling) will depend on. This feature ships NO domain features. Its sole output is a working, authenticated caregiver app shell with offline capability ready for feature 009 (child events) to build on top of. Includes: app cleanup, caregiver authentication (email/password, SecureStore, refresh rotation, auto-refresh), a typed API client, a group view home screen with medical quick-access, and a generic offline queue + sync engine that later features register entity handlers against."

## Clarifications

### Session 2026-07-07

- Q: What triggers the sync engine to attempt sending queued actions? → A: Automatically on network reconnect, on app foreground, and on an explicit pull-to-refresh — no fixed-interval background polling, to avoid needless battery/network use when nothing has changed.
- Q: When the server rejects a queued action as a conflict (it processed a newer/conflicting change already), what's the default outcome for a caregiver? → A: The action is discarded and marked as synced-with-a-conflict-note by default; only a future feature that registers its own handler for a specific action type may override this default with its own conflict-resolution behavior.
- Q: Does a cached read (e.g. the children list) ever expire on its own, forcing a fresh network fetch? → A: No — a cached read is only ever refreshed opportunistically (next successful login, explicit pull-to-refresh, or a normal reload while online); this feature does not implement any time-based cache expiry.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Caregiver signs in and stays signed in (Priority: P1)

A caregiver opens the app on their assigned tablet, enters their email and password, and is taken to their group view. The next morning, they open the app again and are still signed in — no need to log in every shift.

**Why this priority**: Nothing else in the app is reachable without authentication — this is the absolute floor of the feature, and every other user story depends on it.

**Independent Test**: Can be fully tested by logging in with valid caregiver credentials, confirming the group view loads, force-closing and reopening the app, and confirming the session is still active with no login prompt.

**Acceptance Scenarios**:

1. **Given** a caregiver with valid credentials and network connectivity, **When** they submit the login form, **Then** they land on the group view and their session token is stored securely on the device.
2. **Given** a caregiver is already signed in, **When** they close and reopen the app (including after the device restarts), **Then** they remain signed in without re-entering credentials.
3. **Given** a caregiver's access token has expired but their refresh token is still valid, **When** they make any API call, **Then** the app silently refreshes the token and retries the call — the caregiver never sees an interruption.
4. **Given** a caregiver taps "Log out", **When** the action completes, **Then** their session is revoked on the server, all locally stored tokens and cached data are cleared, and they land back on the login screen.
5. **Given** a caregiver's account is deactivated by a director while the caregiver is using the app, **When** the next API call (or a refresh attempt) comes back unauthorized, **Then** the app signs the caregiver out cleanly and shows a clear message — it never gets stuck retrying.

---

### User Story 2 - Caregiver sees their children, even offline (Priority: P1)

After logging in, a caregiver sees the list of children they're responsible for today, including a photo, age, and any active medical alerts for each child. If the tablet loses network connectivity, the same list — loaded moments earlier — remains fully visible and usable.

**Why this priority**: This is the reason the app exists — a caregiver needs to know, at a glance, who they're caring for and whether anyone needs special attention, with or without a working network connection (a real constraint in physical childcare spaces).

**Independent Test**: Can be fully tested by logging in, confirming the children list loads with correct alert icons, then enabling airplane mode and confirming the same list (and each child's medical quick-access) is still available.

**Acceptance Scenarios**:

1. **Given** a caregiver is signed in with network connectivity, **When** the group view loads, **Then** it shows every child the caregiver is responsible for today, each with name, photo, age, and an allergy icon (if applicable) and a fever icon (if a temperature was recorded today above the alert threshold).
2. **Given** a caregiver taps a child's card, **When** the medical quick-access sheet opens, **Then** it shows that child's allergy and medical-notes information without a network round trip if it was already cached.
3. **Given** the group view has successfully loaded at least once, **When** the device goes offline, **Then** the same children list and medical information remain visible and usable.
4. **Given** a caregiver signs in on a brand-new tablet with no prior cached data, **When** the device has no network connectivity at that moment, **Then** the group view shows a clear "can't load without network" state rather than an empty or broken screen.
5. **Given** a caregiver pulls down to refresh the group view, **When** the device has connectivity, **Then** the list reloads from the server and the cache updates.
6. **Given** a caregiver has no children currently assigned to them (e.g., a newly provisioned account not yet eligible for any location, or an eligible location with no children today), **When** the group view loads, **Then** it shows a clear empty state rather than an indistinguishable-from-broken blank screen.
7. **Given** two caregivers are eligible for different locations in the same organisation, **When** each signs in, **Then** neither ever sees the other's location's children, groups, or medical data.

---

### User Story 3 - Actions taken offline are never lost (Priority: P1)

While the tablet has no network connectivity, the app continues to accept and queue the caregiver's actions rather than blocking them. As soon as connectivity returns, everything queued is sent to the server automatically, in the order it happened, with no caregiver action required.

**Why this priority**: This is the foundational guarantee every future caregiver feature (child events, attendance) is built on — without a trustworthy generic queue-and-sync mechanism, no offline-capable feature on top of it can be trusted either. It ships no user-facing action of its own yet, but its correctness is what everything after it depends on.

**Independent Test**: Can be fully tested (using a synthetic/test action registered against the generic queue, since no real feature registers one yet) by queuing several actions while offline, confirming they appear as "pending", restoring connectivity, and confirming they are sent to the server in the order queued and marked as synced.

**Acceptance Scenarios**:

1. **Given** the device is offline, **When** a queueable action is taken, **Then** it is recorded locally and marked as pending, and the caregiver sees a clear "working offline" indicator somewhere in the app.
2. **Given** several actions were queued while offline, **When** connectivity returns, **Then** they are sent to the server automatically, one at a time, in the exact order they were originally taken.
3. **Given** a queued action fails against the server for a reason unrelated to the action's own content (e.g. the connection drops mid-request), **When** the sync engine retries, **Then** the action is retried rather than discarded.
4. **Given** the caregiver's session has expired while a queued action is being sent, **When** the sync engine encounters this, **Then** it refreshes the session once and retries that action before giving up and clearly surfacing that manual attention is needed.
5. **Given** 50 or more actions are queued from an extended offline period, **When** connectivity returns, **Then** all of them are eventually sent and none are silently dropped or sent out of order.
6. **Given** the app is online with nothing queued, **When** no reconnect, foreground, or pull-to-refresh event has occurred, **Then** no sync attempt is made — the sync engine never polls on its own timer.
7. **Given** a queued action is rejected by the server as conflicting with a newer change, **When** no specific handling has been registered for that action's type, **Then** it is discarded and marked as synced with a conflict note rather than retried indefinitely.

---

### Edge Cases

- A caregiver logs in on a brand-new tablet with no cached data at all, and there is no network at that moment — login must fail with a clear, actionable message (there is nothing to authenticate against without a first successful network call).
- A caregiver's account is deactivated mid-shift; the app must recognize the resulting repeated unauthorized responses and stop cleanly rather than looping on retry attempts.
- The offline queue accumulates a large backlog (50+ entries) during a long outage; processing must remain strictly ordered per the sequence actions were originally taken in.
- A caregiver logs out while queued actions are still unsent — those unsent actions are lost (this is a known, accepted limitation, not a defect) and the caregiver should be made aware before confirming logout if there is anything still pending.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST let a caregiver sign in with an email address and password; no social/third-party sign-in options are presented for caregivers.
- **FR-002**: System MUST keep a caregiver signed in across app restarts and device reboots until they explicitly log out, their session is revoked, or their account is deactivated.
- **FR-003**: System MUST store session credentials only in on-device secure storage designed for sensitive data — never in general-purpose unencrypted storage.
- **FR-004**: System MUST silently renew an expired session in the background and transparently retry the action that triggered the renewal, without interrupting the caregiver, as long as the underlying session is still valid.
- **FR-005**: System MUST fully sign a caregiver out — revoking their session server-side and clearing all locally stored credentials and cached data — when they explicitly log out.
- **FR-006**: System MUST detect when a caregiver's session can no longer be renewed — whether because their account was deactivated or because their refresh session has simply expired or is otherwise rejected by the server — and sign them out cleanly with a clear explanation in every such case, without repeatedly retrying.
- **FR-007**: System MUST show every child the signed-in caregiver is responsible for today, each with name, photo, age, and a visible indicator for an active allergy and for an elevated temperature recorded that day; when no children are currently assigned to the caregiver, System MUST show a clear empty state rather than a blank or broken screen.
- **FR-007a**: System MUST NOT show a caregiver any child, group, or location data outside the location(s) they are eligible to work at — this is a data-boundary requirement, not merely a consequence of how the group view happens to be populated.
- **FR-008**: System MUST let a caregiver open a medical quick-access view for any child from that child's card, showing that child's allergy and medical-notes information.
- **FR-009**: System MUST make the most recently loaded children list and medical information available for viewing even when the device has no network connectivity.
- **FR-010**: System MUST clearly indicate to the caregiver when the app is operating without network connectivity, and clearly indicate when queued actions are still waiting to be sent.
- **FR-011**: System MUST allow a caregiver to manually refresh the children list on demand when connectivity is available.
- **FR-012**: System MUST provide a generic mechanism by which an action taken offline is durably recorded on the device and automatically sent to the server once connectivity returns, without requiring the caregiver to do anything to trigger the retry.
- **FR-012a**: System MUST attempt to send queued actions automatically on three triggers only — network connectivity being restored, the app returning to the foreground, and an explicit pull-to-refresh — and MUST NOT poll on a fixed timer in the background.
- **FR-013**: System MUST process queued offline actions in the exact order they were originally taken, and MUST NOT process them concurrently in a way that could reorder actions relative to each other.
- **FR-014**: System MUST retry a queued action that failed for a transient reason (network/server issue) rather than discarding it, and MUST preserve a record of every queued action, including ones that ultimately succeed, for later reference.
- **FR-014a**: System MUST, by default, discard a queued action the server reports as conflicting with a newer change and mark it as synced with a conflict note, unless a specific action type has its own registered handler overriding this default — this feature ships only the default behavior, since no action type is registered yet.
- **FR-015**: System MUST attempt exactly one session renewal for a queued action blocked by an expired session, retry that action once after renewal succeeds, and stop attempting further queued actions with a clear signal if renewal itself fails.
- **FR-015a**: System MUST NOT expire a cached read on a timer — a cached read is only ever replaced by a fresh one through an explicit reload (sign-in, pull-to-refresh, or a normal online reload), never invalidated automatically in the background.
- **FR-016**: System MUST present every user-facing piece of text — including error messages, the offline indicator, and sync status — in the caregiver's selected language (Dutch, French, or English).
- **FR-017**: System MUST use touch targets no smaller than 48pt throughout the caregiver app and lay out every screen for landscape tablet orientation.
- **FR-018**: System MUST remove every trace of the prior placeholder application (its screens, navigation entries, and account-creation flow) so no unrelated functionality remains reachable in the caregiver app.
- **FR-019**: System MUST scope every cached read and every queued action to the signed-in caregiver's own organisation, and MUST clear all of it on logout so a different caregiver signing in on the same device never sees a prior caregiver's cached data.

### Key Entities

- **Caregiver Session**: The signed-in caregiver's credentials and identity held on-device — access session, renewal session, and the caregiver's own identity/role. Not persisted anywhere outside secure on-device storage.
- **Cached Read**: A previously fetched piece of server data (e.g., a children list) kept on-device so it remains viewable without network connectivity, tagged with when it was fetched and which organisation it belongs to.
- **Queued Action**: A caregiver-initiated action recorded on-device before being confirmed by the server — what kind of action, what it contains, when it was taken, and whether/when it was successfully sent.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A caregiver can go from opening the app to viewing their children list in under 15 seconds on a normal network connection.
- **SC-002**: 100% of a caregiver's already-loaded children and medical information remains viewable with the device in airplane mode.
- **SC-003**: 100% of actions queued during a simulated extended offline period (50+ queued actions) are successfully sent once connectivity returns, in their original order, with zero silently lost.
- **SC-004**: A caregiver whose account is deactivated mid-session is signed out within one subsequent app interaction — never stuck in a retry loop.
- **SC-005**: Every user-facing string a caregiver encounters is available in all three supported languages at launch.

## Assumptions

- **No explicit offline-queue size limit in Phase 1**: this feature is only required to prove correctness up to the 50-action scenario in SC-003; there is no stated maximum queue size or dedicated low-storage handling beyond that. A genuinely unbounded backlog (many days offline) is expected to be rare in this domain (a tablet reconnects within a shift or two in practice) and is deferred to a later feature if it ever becomes a real operational problem.
- **Which organisation a caregiver authenticates against**: the login screen includes an explicit organisation field (the same slug directors use), since no other client in this codebase yet resolves an organisation from an email address alone, and no such lookup mechanism exists on the backend. A director communicates the organisation slug to caregivers when provisioning their account (e.g., alongside their invitation).
- **What "the children in their assigned group for today" means**: no feature yet assigns a caregiver to a specific group for a specific day (caregiver scheduling, a later feature, only assigns caregivers to locations/shifts, not groups). This feature therefore shows a caregiver every active child at every location the caregiver is eligible to work at (per their existing staff location eligibility), rather than filtering to one specific group — a stricter per-group, per-day view can be layered on once a real caregiver-to-group daily assignment exists.
- **Read access for caregivers**: the children/groups read endpoints this feature depends on currently only authorize director-level accounts. This feature extends their authorization to also allow caregiver-level accounts, additionally scoped so a caregiver only ever sees data for locations they are eligible to work at — without this, the caregiver app would have no data to show at all, so this is treated as necessary underlying plumbing rather than a separate feature.
- Push notification token registration, caregiver scheduling views, the parent app, and biometric app-unlock are explicitly out of scope for this feature (tracked separately, per the backlog).
- **Temperature alert data doesn't exist yet**: recording a child's temperature is feature 009's job (child events), which hasn't shipped when this feature builds the group view. This feature still builds the fever-icon UI element (so 009 has a slot to plug into), but until 009 ships and starts recording temperatures, that icon has no data to ever trigger on — this is expected, not a defect, and is not separately tested by this feature beyond confirming the element renders correctly with no alert present.
