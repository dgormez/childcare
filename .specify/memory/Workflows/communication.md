# Communication Workflow

## Purpose

Maintain a trusted relationship between childcare staff and parents.

### Trigger

A parent or caregiver needs to communicate.

### Actors

- Parent
- Caregiver
- Director

### Flow

1. Message or update created.
2. Recipient notified.
3. Conversation continues.
4. History preserved.

Feature 011 adds one-way closure notices to this workflow: when a director publishes a
notify-enabled KDV closure day, affected parents receive an immediate push notification and an
in-app closure message; cancelling an already-notified closure sends a cancellation notice.
These notices are not full two-way conversations — feature 013 owns parent messaging.

Feature 020 adds email as a delivery channel alongside push/in-app, reaching contacts a
parent-app account can't (no login required for any of it): a director can compose and send a
one-off bulk email to a location or group (with an optional attachment); every guardian contact
with an email on file automatically receives a daily report email at 19:00 Europe/Brussels,
independently unsubscribable via a no-login link in that email's footer; a director or caregiver
can trigger an on-demand resend of one child's daily report, unaffected by that unsubscribe
state; and closure notices (feature 011, above) and announcements now also email every resolved
contact with an address on file, not just the ones with an active parent-app account.

### Principles

- Warm language.
- Clear ownership.
- Privacy aware.
- Avoid unnecessary formalism.
