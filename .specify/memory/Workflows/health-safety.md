# Health & Safety Workflow

## Purpose

Keep safety-critical child information accurate and reachable, and handle safety-related
events consistently. Covers ongoing information (allergies, medication, emergency contacts)
as well as one-off events (incidents, injuries, health alerts).

### Trigger

- Ongoing: a child's medical/emergency profile is created or updated (enrollment, or a parent-
  or director-initiated change).
- Event: a caregiver observes something safety-relevant — an incident, injury, or a reason to
  raise a health alert (e.g., fever recorded above threshold).

### Actors

- Caregiver
- Director
- Parent
- System

### Flow — ongoing information

1. Allergy, medical-condition, medication, and emergency-contact fields live on the child
   profile (feature 006).
2. Caregivers get read-only, always-available access to this data for children they're
   responsible for (feature 008's medical quick-access), scoped to their eligible locations
   (FR-007a).
3. Only a director can edit it.

### Flow — incident/event

1. Caregiver records the incident.
2. System timestamps and stores the record.
3. Director reviews.
4. Parent receives appropriate communication.
5. Resolution is tracked.

### Applications

Caregiver Tablet:

- Medical quick-access (read-only) from a child's card.
- Fast incident capture.

Director Web:

- Edit medical/emergency profile fields.
- Review and reporting on incidents.

Parent Mobile:

- Communication and acknowledgement.

### Data

- Child medical profile: allergies, severity, medical conditions, dietary restrictions,
  medication, emergency contacts (feature 006, `Child` entity).
- Incident record: child, caregiver, timestamp, description, resolution status.
