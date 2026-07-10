# Research: Parent Communication (013)

## R1 — Parent account provisioning mirrors StaffProfile/StaffInvitation exactly

**Decision**: Add a nullable `TenantUserId` FK to `Contact` (one Contact ↔ at most one `TenantUser` with `Role = Parent`). Add a new `ParentInvitation` entity (`ContactId`, `Email`, `TokenHash`, `ExpiresAt`, `CreatedAt`) — a direct structural copy of `StaffInvitation` (`backend/ChildCare.Domain/Entities/StaffInvitation.cs`). The accept flow copies `AcceptStaffInvitationCommandHandler`'s shape exactly: anonymous + tenant-exempt (organisation slug travels in the emailed link's query string, same as `AcceptStaffInvitationCommand` and feature 003's `ResetPasswordCommand`), looks up the invitation by token hash, checks `ExpiresAt` and an empty `PasswordHash` as the single-use guard (no separate `UsedAt` column, same reasoning as `StaffInvitation`'s own comment), then sets the password on the pre-created `TenantUser`.

**Rationale**: This is a second instance of an already-proven pattern (create `TenantUser` with empty `PasswordHash` at invite time → invitation row references the not-yet-activated profile/contact → accept sets the password), not a new design. Reusing it exactly avoids inventing a second invitation mechanism (`Invitation` for org onboarding, `StaffInvitation` for staff, `ParentInvitation` for parents — three, not a shared generic one, matching the codebase's existing choice not to generalize `Invitation`/`StaffInvitation` into one table).

**Alternatives considered**: A generic `Invitation` table with a polymorphic target — rejected; the codebase already had this choice available when `StaffInvitation` was built (feature 005) and chose a second concrete table over generalizing `Invitation`, so a third concrete table is the consistent move, not a fork in approach.

## R2 — Push token write path reuses `Contact.PushToken`, not a new column

**Decision**: `Contact.PushToken` (added by feature 009, currently never written by any client) is the field FR-014's registration endpoint writes to. The endpoint is `ParentOnly`-authenticated; the handler resolves the caller's linked `Contact` via `Contact.TenantUserId` and updates `Contact.PushToken` in place (single active token, matching the existing single-column design — no new multi-device table).

**Rationale**: Feature 009's `TemperatureAlertService` and feature 011's `ClosureNotificationService` already read `Contact.PushToken` for their recipient queries. Writing through the same column means both existing consumers start working the moment this feature ships its registration endpoint, with zero changes to either. A second token table would fork "where is a contact's push token" into two places for no benefit, since one person = one `Contact` record (feature 006: contacts are modeled once and shared across sibling children via the `ChildContact` junction, never duplicated).

## R3 — Push sending reuses `IExpoPushSender` unchanged

**Decision**: Inject the existing `IExpoPushSender` (`backend/ChildCare.Application/Common/IExpoPushSender.cs`) directly for new-message and announcement pushes. Follow `ClosureNotificationService`'s exact structure: a per-locale label dictionary (nl/fr/en), resolve recipients, write the in-app record first, attempt the push, catch and log failures without throwing, never block the triggering write.

**Rationale**: This port is already generic (`SendAsync(pushToken, title, body, ct)`) with two working consumers (009, 011) proving the pattern. No new abstraction needed.

## R4 — Notification centre is a new generic table; closure notices stay out of scope

**Decision**: A new `Notification` entity — `Id, TenantUserId (recipient), Type (NewMessage | Announcement | TemperatureAlert), SourceId, TitleKey, BodyKey, ArgumentsJson, CreatedAt, ReadAt` — modeled after `ParentClosureMessage`'s shape (feature 011) but generalized across types instead of being closure-specific. Feature 009's `TemperatureAlertService` gains one new call at the end of its existing `NotifyAsync` to also write a `Notification` row (`Type = TemperatureAlert`) — today it only fires a push with zero in-app fallback, which is a real gap this feature's FR-010 requires closing. `ParentClosureMessage` (011) is left untouched and is deliberately NOT unioned into this feature's notification centre — the original feature prompt's notification list ("new message, request approved, temperature alert") and this feature's own FR-010 never named closures, so closure notices remain on their existing, separately-shipped mechanism. A future feature can unify them if that becomes a real complaint.

**Rationale**: Matches the constitution's existing preference for one generic table over one-table-per-type (the same reasoning already applied to `child_events`, feature 009). Touching `TemperatureAlertService` is small, additive plumbing needed to satisfy an FR this spec explicitly states — the same class of "minor backend plumbing a feature can't function without" the standing process rules say to do without pausing. Not touching `ParentClosureMessage` avoids an unscoped migration of an already-shipped, working feature.

## R5 — Daily summary: extend and re-expose, don't rebuild

**Decision**: `GetDailySummaryQuery`/`GetDailySummaryQueryHandler` (`backend/ChildCare.Application/ChildEvents/GetDailySummaryQuery.cs`) already does the correct `VisibleToParent`-filtered aggregation for naps/bottles/diapers/mood/temperature/medication. Extend its response with `activities` (from `ChildEventType.Activity` payloads) to satisfy this feature's FR-001. Photos are explicitly out of scope (see spec.md Clarifications correction — feature 009's own clarification session already deferred all photo attachment; no `Photo` entity exists anywhere in the domain, so there is nothing to aggregate). Expose the extended query through a **new** `GET /api/parent/children/{childId}/daily-summary` endpoint under `ParentOnly`, which authorizes that the caller's linked `Contact` is actually a contact of `childId` before delegating to the existing query — the query itself needs no auth-aware changes, since the existing `GetDailySummaryQuery` handler already assumes its caller has established that authorization (today, the device-token caregiver route does this implicitly via device→group→child scoping).

**Rationale**: Avoids duplicating a working, tested aggregation. The only real gap is that no parent-authenticated route calls it yet (confirmed: today only the device-authenticated caregiver route at `GET /api/child-events/daily-summary` exists).

## R6 — Message thread sharing model: participants keyed by `TenantUser.Id`, not `Contact.Id`

**Decision**: `message_thread_participants.user_id` (per the original prompt's own schema) references `TenantUser.Id` uniformly for parent, staff, and director participants — not a mix of `Contact.Id` for parents and `TenantUser.Id` for staff. When a thread is created for a child, every parent contact of that child **with an active parent account** (`Contact.TenantUserId IS NOT NULL`) is added as a participant (FR-003a). When a second parent later completes their invitation for a child who already has an active thread, they are backfilled as a participant with access to full history (FR-006a) — implemented as: on `AcceptParentInvitationCommand` success, look up existing threads for any child this contact is linked to and insert a `message_thread_participants` row for each.

**Rationale**: A single ID type for the participants table avoids a nullable-either-column design or a discriminator column, keeping the join table exactly as simple as the original prompt specified.

## R7 — `messages.read_at` is a single "read by the other side" marker, not per-participant

**Decision**: Keep the original prompt's schema literally — one `read_at` column per message, not a per-participant read-receipt table. Semantics: for a parent-authored message, `read_at` is set the first time any director/staff participant opens the thread; for a staff-authored message, `read_at` is set the first time any parent participant (either shared-thread parent) opens it. FR-013's "staff-facing unread indicator" is a `COUNT` of parent-authored messages with `read_at IS NULL`, scoped to threads visible to that director — no separate staff notification table.

**Rationale**: The two-party (family ↔ KDV) mental model holds even with two parents on one side; a full N-participant read-receipt matrix is more structure than any requirement (FR-005, FR-011) actually asks for, and the original schema draft in the BACKLOG prompt already specified a single `read_at` column, not one per participant.

## R8 — Announcement delivery is bounded to contacts with an active parent account

**Decision**: FR-008 ("every parent/guardian contact... within scope") is satisfied for every contact who **has** completed a parent-app invitation (`Contact.TenantUserId IS NOT NULL`) and matches the location/group scope. A contact who was never invited has no account, no notification centre, and no push token to reach — they are not a gap this feature introduces, they simply aren't reachable by any digital channel yet (same limitation the original prompt's own edge case already accepts: "one parent has no app installed... sees it next time they log in").

**Rationale**: Directly follows from R1 — reachability requires an account, and account creation is director-invitation-gated (per this feature's own Clarifications). No behavior change needed to any other feature.

## R9 — Parent app is a new, standalone Expo project (not a mode inside `mobile/`)

**Decision**: `mobile/` is a single-purpose Expo project — `app.config.js` hardcodes `orientation: "landscape"`, bundle id `com.dgit.childcare`, and its whole navigation tree (`(auth)`, `(room)`, `(room-setup)`, `(app)`) is caregiver/kiosk-specific. The parent app is a new top-level Expo project (`parent-mobile/`, portrait, its own bundle id, its own `app.config.js`), following the same scaffold shape feature 008 established for `mobile/` (SecureStore auth, openapi-fetch generated client, i18n via `expo-localization` + `react-i18next`) — not a role-switch inside the existing app.

**Rationale**: Orientation lock and navigation shell are structural, not cosmetic — retrofitting `mobile/` to support both a locked-landscape kiosk mode and a portrait personal-login mode in one Expo config is more invasive than standing up a second project using the already-proven scaffold pattern. This is also what the spec's own Assumptions section already commits to.

## R10 — Web admin: new `/messages` and `/announcements` routes replace two of the existing inert placeholders' siblings

**Decision**: Add `web/app/(app)/messages/` (thread list + detail/reply) and `web/app/(app)/announcements/` (compose + sent history) as new real screens with real sidebar entries, following the same table/detail pattern `web/app/(app)/staff/` (007a) and `web/app/(app)/waiting-list/` (012a) already established. No existing inert placeholder route needs replacing — 007a's placeholders were `locations`/`contracts`/`children`, none of which is `messages`.

**Rationale**: Consistent with 007a's own stated precedent ("if a future feature's spec needs a web admin screen... treat that as a hard dependency, not something to defer").
