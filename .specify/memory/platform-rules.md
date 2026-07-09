# Platform Rules

Per-surface interaction and layout rules. See `design-system.md` for the shared visual
language (color, spacing, motion) that applies across all three surfaces. Theming (light/dark)
is one of those shared rules, not a per-surface choice: all three surfaces always follow the
device/system setting — see `design-system.md`'s Theming section.

## Caregiver Tablet

The caregivers are busy, often standing and multitasking.

Design principles:

- Large touch targets — minimum 48pt, no exceptions. A dedicated PIN/numeric-entry keypad
  (kiosk mode, feature 008a) uses 64pt, since it's the single highest-frequency interaction
  on this surface.
- One-handed operation.
- Quick actions — the common case is one tap, not a multi-step form.
- Glanceable information — a caregiver should get what they need from a half-second look.
- Minimal text entry — prefer selection (taps, toggles, presets) over typing.
- Landscape orientation, locked (tablet is mounted or laid flat on a counter, not handheld
  portrait).

Think:

- Attendance board.
- Daily schedule.
- Incident logging.
- Meal tracking.
- Nap tracking.

References: Brightwheel, HiMama.

## Parent Mobile App

Parents are emotional users. They want:

- Reassurance.
- Photos.
- Updates.
- Notifications.
- Communication.

The app should feel warm, calm, and delightful.

Think:

- Timeline feed.
- Messages.
- Daily reports.
- Invoices.

References: Brightwheel, ClassDojo.

## Director Web App

This is basically an operations dashboard. It needs:

- Density.
- Reporting.
- Administration.
- Multi-column layouts.
- Tables.
- Filtering.

Design principles:

- Desktop-first, minimum supported viewport `1280px`. Multi-column layouts collapse to a
  single column below that — directors are not expected to run this on mobile web (per
  `reference-products.md`'s persona), so narrower breakpoints aren't a design priority.
- Mouse and keyboard, not touch — no 48pt touch-target floor here (see caregiver tablet
  above), but every interactive element still needs a visible focus ring; keyboard-only
  navigation must reach every action, per the Linear reference below.
- Keyboard shortcuts for frequent actions (search, filters, primary create/save action) —
  the thing that actually earns the Linear/Notion reference, not just their visual density.
- High-density tables per `design-system.md`'s Density section (`40px` row height, `8px`/
  `12px` cell padding) — favor a full-row click/select affordance over small inline icon
  buttons where the row itself represents one record.

Think:

- Staff scheduling and room assignment.
- Billing and invoicing.
- Enrollment and family records.
- Compliance and reporting.
- Cross-room/cross-child dashboards.

References: Airtable, Notion, Monday.com, Linear (keyboard-driven interaction, not just
visual density).
