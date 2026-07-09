# Health & Safety Workflow

## Purpose

Keep safety-critical child information accurate and reachable, and handle safety-related
events consistently. Covers ongoing information (allergies, medication, emergency contacts)
as well as one-off events (incidents, injuries, health alerts).

**Extended by feature `009-child-events`** (2026-07-08) — added the temperature-alert path and
medication event recording described below; the ongoing-profile flow (feature 006) is unchanged.

### Trigger

- Ongoing: a child's medical/emergency profile is created or updated (enrollment, or a parent-
  or director-initiated change).
- Event: a caregiver observes something safety-relevant — a temperature reading, a medication
  administration, an incident, injury, or a reason to raise a health alert.

### Actors

- Caregiver
- Director
- Parent (push-notification recipient, once a registration path exists — see below)
- System (temperature-threshold detection, push dispatch)

### Flow — ongoing information

1. Allergy, medical-condition, medication, and emergency-contact fields live on the child
   profile (feature 006).
2. Caregivers get read-only, always-available access to this data for children they're
   responsible for (feature 008's medical quick-access), scoped to their eligible locations
   (FR-007a).
3. Only a director can edit it.

### Flow — temperature alert (feature 009)

1. Caregiver records a `temperature` event via the quick-action sheet.
2. If the reading exceeds 38.0°C, the system (`ITemperatureAlertService`) resolves every
   `ChildContact` with `CanPickup = true` and a registered push token, and dispatches an Expo
   push notification to each, in that contact's own locale — never from the client.
3. A dispatch failure (no recipients, or a transport error) never fails the temperature event's
   save; both are logged, not surfaced as an error to the caregiver.
4. **Known limitation, by design**: no parent-facing client exists yet to register a push
   token, so this alert currently has zero deliverable recipients in production — the
   detection/dispatch mechanism is built ahead of that registration UI so the eventual parent
   app only needs to add token registration, not alerting logic.

### Flow — medication administration (feature 009)

1. Caregiver records a `medication` event (name, dose, reason).
2. Optionally, before submitting, the caregiver confirms who is administering it via the same
   select-then-PIN flow feature 008a built for shift check-in/out (`AdministratorConfirmation`)
   — a UX confirmation of identity, not a second authentication step; skipping is always allowed
   and a director can fill it in later.
3. This is deliberately distinct from *who logged the entry* (`recorded_by`, resolved from the
   room's shift register) — `administered_by` is who actually gave the dose.

### Flow — incident/event

1. Caregiver records the incident.
2. System timestamps and stores the record.
3. Director reviews.
4. Parent receives appropriate communication.
5. Resolution is tracked.

*(Not yet built as its own event type — `note`/`activity` events serve this today; a dedicated
incident-record concept, if ever needed beyond that, would be a future feature's decision.)*

### Applications

Caregiver Tablet:

- Medical quick-access (read-only) from a child's card.
- Temperature/medication quick-entry with administrator confirmation.
- Fast incident capture (via `note`/`activity` events today).

Director Web:

- Edit medical/emergency profile fields.
- Review and reporting on incidents.
- A medication event's `administered_by` can be filled in retroactively via the API — no screen
  in this app exposes it yet (spec.md Assumptions, feature 009).

Parent Mobile:

- Communication and acknowledgement.
- Fever-alert push notifications — mechanism built (feature 009), no client to register a token
  yet.

### Data

- Child medical profile: allergies, severity, medical conditions, dietary restrictions,
  medication, emergency contacts (feature 006, `Child` entity).
- Incident record: child, caregiver, timestamp, description, resolution status.
- Temperature/medication events: `child_events` table (feature 009) — see `dailycare.md` for the
  shared event-recording mechanics; `Contact.PushToken` (feature 009, nullable, not yet
  populated by any client) is the alert-recipient field.
