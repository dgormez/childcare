# Platform Administration Workflow

## Purpose

Give a platform-admin a self-service way to onboard new KDV customers and see every organisation
already on the platform, without any manual database or ops-tool operation. Distinct from every
other workflow in this map, which is scoped to a single tenant's own operations — this workflow
sits above all tenant boundaries.

### Trigger

A platform-admin has agreed to onboard a prospective KDV director (sales/onboarding
conversation), needs to check on an invitation's status, or needs to answer "how many
organisations do we have and when did each sign up."

### Actors

- **Platform Admin** — an existing director account (`TenantUser`, still scoped to its own
  tenant like any other director) flagged `IsPlatformAdmin`. Granted out-of-band via the
  `grant-platform-admin` CLI command (feature 013h) — no in-app way to grant this flag exists.
- **Prospective director** — has no account yet; interacts only with the emailed invitation
  link and the public registration page (features 001, 032). Becomes a regular, tenant-scoped
  director account the moment registration completes.
- System (sends the invitation email in the platform-admin's chosen language; derives every
  invitation's status from token/expiry/revoke/resulting-organisation facts, never a stored
  field that could drift; enforces `PlatformAdminOnly` on every management endpoint).

### Flow — invitation & registration (features 001, 032)

1. Platform-admin opens the Invitations screen, enters a prospective director's email (plus an
   optional organisation-name note and an email language), and sends the invitation.
2. If a still-usable (Pending or Expired) invitation already exists for that email, it becomes
   Revoked first — there is never more than one usable invitation per email at a time.
3. The prospective director receives the email and opens the registration link. The page looks
   up the token, pre-fills and locks the invited email, and shows a registration form
   (organisation name, director name, password) — or a single generic "no longer valid" message
   if the token is expired, revoked, already used, or unrecognized (never revealing which).
4. On submit, the organisation is created and the director's account is immediately usable — no
   approval step, no waiting period. This is unchanged from feature 001's original behavior.
5. The platform-admin can see every invitation's status (Pending / Accepted / Expired / Revoked,
   derived, not stored) and resend or revoke anything not yet Accepted.

### Flow — organisation directory (feature 032)

1. Platform-admin opens the Organisations screen: every organisation on the platform, with name,
   plan, provisioning status, KBO number, creation date, and the email that registered it.
2. This is read-only visibility — no suspend, deactivate, edit, or delete action exists here.
   Tenant suspension/deletion tooling remains a deliberately separate, not-yet-built capability
   (feature 002's own deferral).

### Flow — shared platform data management (feature 013h)

1. Platform-admin manages shared, platform-wide reference data — today, the vaccine catalog
   (`vaccine_types`, feature 013g/013h): create, rename, reorder, deactivate entries. Changes are
   immediately visible to every tenant's existing read-only view of that data.
2. Every platform-admin capability (invitations, organisation directory, shared reference data)
   lives behind one shared navigation section in director-web, hidden entirely from any director
   account without the flag.

### Applications

Director Web only:

- Invitations screen (create/list/resend/revoke, feature 032).
- Organisations screen (read-only directory, feature 032).
- Vaccine catalog management screen (feature 013h).
- A shared platform-admin navigation section wrapping all of the above.

A public, unauthenticated registration page also exists (`/register`, feature 032) — reachable
only via an emailed invitation link, not part of the director-web app's authenticated surface.

Parent Mobile / Caregiver Tablet / Staff Mobile: no interaction — this workflow has no
caregiver-, parent-, or staff-facing surface at all.

### Principles

- A platform-admin is not a new account type — it's an existing director account with an
  additional flag granting cross-tenant authority. No separate authentication mechanism exists
  or is planned.
- Every invitation/revoke/resend action is attributed (who, when), resolved server-side from the
  acting platform-admin's own session — never a client-supplied value. A resend/duplicate-create
  supersede shares the same attribution fields as an explicit revoke; the data model draws no
  distinction between the two triggers.
- The organisation directory is strictly read-only. Any future tenant-suspension or
  organisation-editing capability is an explicit, separate decision — never silently folded into
  this directory.
- Registration activates an organisation immediately, with no manual-approval gate — this
  workflow's job is removing manual steps, not adding new ones.
