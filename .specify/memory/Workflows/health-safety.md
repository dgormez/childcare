# Health & Safety Workflow

## Purpose

Keep safety-critical child information accurate and reachable, and handle safety-related
events consistently. Covers ongoing information (allergies, medication, emergency contacts)
as well as one-off events (incidents, injuries, health alerts).

**Extended by feature `009-child-events`** (2026-07-08) — added the temperature-alert path and
medication event recording described below; the ongoing-profile flow (feature 006) is unchanged.

**Extended by feature `013c-vaccine-health-records`** (2026-07-13) — added structured vaccination
history and categorized health records (see "Flow — vaccination & health record tracking" below),
alongside the existing free-text profile fields (feature 006). Also built director web's first
per-child detail screen (`/children/[id]`, Gezondheid tab), superseding the "no per-child detail
screen yet" note this file previously carried under Applications.

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

### Flow — vaccination & health record tracking (feature 013c)

1. Director opens a child's Gezondheid tab (director web's first per-child detail screen) and
   records a vaccination (vaccine name, dose, administered date, next due date, clinic, notes) or
   a categorized health record (allergy / chronic condition / standing medication / doctor's note
   / other — a title, description, optional validity window, optional signed-URL attachment).
   Both are structured, queryable records, distinct from feature 006's free-text profile fields.
2. The system computes, across every child, which vaccines are due or overdue within 30 days and
   surfaces them in a "Vaccinations due soon" block on the director's dashboard — a compliance
   reminder, not just a log. An overdue vaccine is never auto-dismissed; it clears only when a
   director records the vaccination as given or updates the due date.
3. Caregivers get the same read-only, location-scoped access pattern step 2 above already
   established (feature 008a's `StaffLocationEligibility` check) extended to this new data: a
   child's active health records and every due-soon/overdue vaccine flag for that child surface
   directly in the existing medical quick-access screen — no separate screen, no extra tap.
4. Caregivers can never create, edit, or delete a vaccine or health record — director-only, in
   every context.
5. Vaccine/health record data is excluded from any bulk export or automated summary by default
   (no such feature exists in this codebase yet — this is a forward-looking constraint on
   whichever future feature builds one, not an active behavior with a UI today).
6. Records survive a child's deactivation/departure indefinitely (legal retention) — no cascade
   delete, no deactivation guard registered by this feature (mirrors 013b's incident reports).

### Flow — incident/event

**Built as its own record type by feature `013b-incident-reports`** (2026-07-12) — superseding the
`note`/`activity`-event placeholder this section previously described.

1. Caregiver opens the child's profile on the tablet and files an incident report: what happened,
   injury type, first-aid/doctor/parent-notification details. `reported_by` is resolved server-side
   from the room shift register (who was checked in at `occurred_at`) — the same mechanism feature
   009 uses for `recorded_by` — rather than a PIN-confirmed single identity, so offline filing is
   never blocked by an unreachable confirmation step.
2. System timestamps (`occurred_at`, separately trackable from `created_at` for backdated reports)
   and stores the record. Works offline via feature 008's offline-queue/sync mechanism.
3. Director reviews via a dedicated Incidents screen (cross-location, filterable by date/location/
   child); opening a report marks it reviewed — this in-app indicator is the mechanism a director
   learns of a new incident (no director push-notification channel exists in this codebase; see
   013b spec.md Assumptions).
4. Parent receives communication out-of-band (phone/app/in-person) — the caregiver records how, but
   no digital acknowledgment or in-app parent notification is sent by the system for this feature.
5. Resolution is tracked via a `follow_up` note, addable at any time, including after the record's
   24-hour immutability window locks every other field.

### Applications

Caregiver Tablet:

- Medical quick-access (read-only) from a child's card — now also shows active health records
  and due-soon/overdue vaccine flags (feature 013c), same screen, same one-tap reach.
- Temperature/medication quick-entry with administrator confirmation.
- Incident report filing ("Incident melden" on the child's profile, feature 013b), works offline.

Director Web:

- Edit medical/emergency profile fields.
- Child Gezondheid tab (feature 013c, `/children/[id]`) — the first per-child detail screen in
  this app: vaccine record and health record CRUD, attachment upload for health records.
- Dashboard "Vaccinations due soon" block (feature 013c) — cross-child, 30-day window, overdue
  flagged distinctly.
- Dedicated Incidents screen (feature 013b): cross-location review, filtering, reviewed-state
  tracking, PDF export for inspection. Now that `/children/[id]` exists (013c), a future pass
  could link an incident's child directly to their Gezondheid tab, but 013b's own child filter
  remains the primary incident-lookup path — not changed by this feature.
- A medication event's `administered_by` can be filled in retroactively via the API — no screen
  in this app exposes it yet (spec.md Assumptions, feature 009).

Parent Mobile:

- Communication and acknowledgement.
- Fever-alert push notifications — mechanism built (feature 009), no client to register a token
  yet.

### Data

- Child medical profile: allergies, severity, medical conditions, dietary restrictions,
  medication, emergency contacts (feature 006, `Child` entity).
- Incident record (feature 013b, `incident_reports` tenant table): child, reporting caregiver
  (nullable), `occurred_at`/`created_at`, description, injury type, first-aid/doctor/parent-
  notification details, reviewed state, follow-up note. Immutable (aside from follow-up) 24 hours
  after creation. Never cascade-deleted when its child is deactivated.
- Temperature/medication events: `child_events` table (feature 009) — see `dailycare.md` for the
  shared event-recording mechanics; `Contact.PushToken` (feature 009, nullable, not yet
  populated by any client) is the alert-recipient field.
- Vaccine record (feature 013c, `vaccine_records` tenant table, replaces feature 006's unused
  `vaccination_records`): child, vaccine name, dose number, administered/next-due dates,
  administering clinic, notes, recording director (nullable — legacy-migrated rows have none),
  soft-delete. Never cascade-deleted when its child is deactivated.
- Health record (feature 013c, `health_records` tenant table): child, category (allergy/chronic
  condition/standing medication/doctor's note/other), title, description, optional validity
  window, optional GCS-signed attachment, recording director, soft-delete. Distinct from feature
  006's free-text `Child` medical fields — this is the structured, categorized counterpart.
