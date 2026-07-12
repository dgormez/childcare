# Known UI gaps found while writing E2E tests

Found by sweeping every `app/(app)/*/page.tsx` for real `apiClient.POST/PUT/PATCH/DELETE` calls
vs read-only pages, while building out the director E2E suite. Not fixed — documented so the gap
between spec and implementation doesn't get lost. Re-check this file's claims (`grep -rn` the
page in question) before relying on it; UI work may have landed since.

## No web UI at all (backend + spec exist, `<NotYetAvailable />` placeholder in the page)

- **Children** (`specs/006-children`, `web/app/(app)/children/page.tsx`) — full CRUD API exists
  (`ChildrenEndpoints.cs`), nothing in the web app to drive it.
- **Contracts** (`specs/007-contracts`, `web/app/(app)/contracts/page.tsx`) — same story
  (`ContractsEndpoints.cs`).

## Partial UI — view/manage only, no "create" form

- **Staff** (`specs/005-staff`, FR-001 requires directors be able to create a staff profile) —
  `web/app/(app)/staff/page.tsx` only lists staff, resets PINs
  (`PUT /api/staff/{id}/pin`), and deactivates/reactivates. No "Add Staff" entry point anywhere
  in `web/`, even though `POST /api/staff` and the invite-acceptance flow are fully implemented
  backend-side.
- **Locations** (`specs/004-locations`) — `web/app/(app)/locations/[id]/page.tsx` supports
  editing an existing location (`PUT`). No "Add Location" form; `web/app/(app)/locations/page.tsx`
  is list/search only.

## RESOLVED — parent invitations can be completed via API

Staff and organisation invitations both return the raw invitation token directly in their
`POST` response, so E2E seeding could accept them immediately. Parent invitations didn't —
`POST /api/parent-invitations` only ever returned `InvitationId`, `ContactId`, `Email`,
`ExpiresAt`; the token is hashed before storage and the plaintext only ever went out via the
real `IEmailSender` (Gmail SMTP in dev). Fixed by adding
`backend/ChildCare.Api/Endpoints/E2ESupportEndpoints.cs` — a `Development`-only
`POST /api/e2e-support/parent-invitations` that calls the same `CreateParentInvitationCommand`
and additionally returns the raw token (now carried on `ParentInvitationResult.Token`, never on
the public `ParentInvitationResponse` the real endpoint returns). `web/e2e/support/seed.ts`'s
`seedParent()` uses it. This unblocked `web/e2e/messages.spec.ts`'s "reply to a real thread"
test and `web/e2e/requests.spec.ts` entirely, and clears the way for the parent-mobile Maestro
suite's invitation-acceptance tests later.

Staff invitations have the same shape of problem (`POST /api/staff` never returns its
invitation token either) — not yet hit in practice since no web test has needed to log in *as* a
caregiver, but the caregiver-mobile Maestro phase will need it. `E2ESupportEndpoints.cs` is
where a `staff-invitations` sibling endpoint should go when that's needed.

## FIXED — Requests page's "All statuses" filter was silently a no-op

`app/(app)/requests/page.tsx` sends `status=all` to `GET /api/day-reservations`, but
`ListPendingDayReservationsQueryHandler` only recognized `pending`/`approved`/`rejected`/
`cancelled` — an unparseable value (including `"all"`) silently fell back to `Pending`, so
selecting "All statuses" in the real app showed exactly the same rows as "Pending". Confirmed by
comparing against `ListWaitingListEntriesQuery`, which already treats `"all"` as its own case.
Fixed in `ChildCare.Application/DayReservations/ListPendingDayReservationsQuery.cs` to match that
precedent; added `ListPending_WithStatusAll_ReturnsEveryStatus` to
`ChildCare.Api.Tests/DayReservations/DayReservationEndpointsTests.cs`.

## Minor UX gap: Requests table has no Status column

`components/DayReservationsTable.tsx` has columns Child/Type/Date/Reason/Actions — no Status.
An approved and a rejected request render identically once decided (both just lose their
Approve/Reject buttons); the only exception is auto-approved (informational-mode) rows, which
get an "Auto-approved" badge. Not fixed (a UI/design call, not a functional bug) —
`web/e2e/requests.spec.ts` asserts on the actions disappearing rather than a status label, since
there isn't one to check.

## E2E coverage taken as a result

`web/e2e/staff.spec.ts` and `web/e2e/locations.spec.ts` cover what's actually in the UI (search,
PIN reset validation, deactivate/reactivate, edit-location validation) and seed any data those
flows need directly through the API rather than through a UI that doesn't exist. Children and
contracts have no E2E spec — there's nothing to drive.
