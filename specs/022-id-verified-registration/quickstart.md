# Quickstart: ID-Verified Registration

## Prerequisites

- Backend running locally (`dotnet run` in `backend/ChildCare.Api`) against a Docker PostgreSQL
  instance with the new `AddIdentityVerificationAndNrn` migration applied (script generated, run
  manually per this repo's convention — research.md R7).
- `web/` running (`npm run dev`), API types regenerated (`npm run generate-api-client`) after the
  backend contract change.
- A director account. At least one enrolled child and one linked contact with no identity
  verification recorded yet.

## Scenario 1 — Director verifies a child's identity (US1)

1. Log into `web/` as a director, open an unverified child's `/children/{id}` detail screen,
   "Profiel" tab.
2. In the new "Identiteit bevestigen" section, attempt to confirm with no document type selected.
   **Expect**: blocked with a validation message; nothing saved (Acceptance Scenario 2).
3. Select "Birth certificate", leave the note empty, confirm.
4. **Expect**: the section now shows the document type, the confirming director, and today's
   timestamp as read-only (Acceptance Scenario 1/3).
5. Reload `/children` — **expect** this child no longer shows the "Niet geverifieerd" badge
   (FR-007a).

## Scenario 2 — Director verifies a contact's identity, once, for a shared sibling contact (US2)

1. Open a child's "Contacts" tab (`ChildContactsTab`) where a linked contact is unverified.
2. Use the row's verify action, select a document type, confirm.
3. **Expect**: the contact now shows a verified indicator on this row.
4. Open a second child linked to the same contact (a sibling, feature 030).
5. **Expect**: that contact already shows as verified here too — no second verification needed
   (Acceptance Scenario 2).

## Scenario 3 — Director corrects a verification without losing the original attribution (US3)

1. On a child already verified as "Birth certificate", open "Identiteit bevestigen" again and
   change the document type to "eID", confirm.
2. **Expect**: the section now shows "eID" as current, with today's date/current director as the
   most recent verification, **and** the original document type's verifying director/date is
   still visible and unchanged (data-model.md's `FirstIdVerifiedAt`/`FirstIdVerifiedByEmail`).

## Scenario 4 — Admin-home unverified count and per-child badge stay in sync (US4)

1. Note the "missing_identity_verification" count in the `/dashboard` Data Completeness section
   for a set of enrolled-but-unverified children (some with no attendance history yet — a
   brand-new enrolment).
2. **Expect**: a child with zero `AttendanceRecord`s but no `DeactivatedAt` still appears in the
   flag list (research.md R5 — this flag's scoping is independent of the query's other four,
   attendance-linked checks).
3. Verify one of the flagged children (Scenario 1). Reload `/dashboard`.
4. **Expect**: the count drops by one and that child's flag row disappears.
5. Deactivate a still-unverified child (existing `/children/{id}` deactivate action).
6. **Expect**: that child's flag also disappears, without being verified (Acceptance Scenario 2
   under US4 — inactive children never count).

## Scenario 5 — National Register Number is captured, encrypted, and always masked (US5)

1. On a child's "Identiteit bevestigen" section, enter an 11-digit NRN (formatted with dots/dash
   or plain digits, e.g. `85.07.30-033.71` or `85073003371`). Save.
2. **Expect**: the field now displays only the last 4 digits (`•••••••33.71`-style mask) — reload
   the page and confirm it's still masked, never the full value.
3. Attempt to save an NRN with the wrong digit count (e.g., 9 digits).
4. **Expect**: blocked with a validation message; no partial value persisted.
5. Inspect application logs from step 1's save request. **Expect**: no plain-text NRN appears
   anywhere in the log output (FR-011).
