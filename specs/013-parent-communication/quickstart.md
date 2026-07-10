# Quickstart: Parent Communication (013)

Validates the feature end-to-end against a local dev stack (`docker-compose.yml` Postgres, backend on its usual dev port, `web/` dev server, and the new `parent-mobile/` Expo project).

## Prerequisites

- Backend running with a seeded tenant (any existing seed/test-support tenant works), a director account, a child with at least one enrolled contact (`CanPickup = true`, real email).
- `web/` running (`npm run dev`), logged in as that director.
- `parent-mobile/` running in Expo Go or a simulator, pointed at the same backend dev URL as `mobile/` already is.

## Scenario 1 — Provision a parent account (User Story 0)

1. In web admin, open `/messages` and click "Invite parent" (no per-child Contacts management screen exists in `web/` yet — `children/page.tsx` is still a placeholder — so the invite dialog searches the tenant-wide contact list instead, per `web/components/InviteParentDialog.tsx`'s own doc comment). Confirm the send action is disabled for a contact with no email on file.
2. Send the invite. Confirm `POST /api/parent-invitations` returns 201 and (in dev) the invitation link is retrievable from logs/dev inbox.
3. Open the invite link in `parent-mobile/`, complete registration with a password.
4. Confirm login succeeds afterward with those credentials.
5. Re-use the same (now-consumed) link — confirm it's rejected with `errors.invitation.not_found`, not a specific "already used" message.

## Scenario 2 — Daily summary (User Story 1)

1. As a caregiver (existing 008a kiosk flow), record a mix of `visible_to_parent = true` events (a nap, a bottle, a diaper change) and one internal-only note for the child, all dated today.
2. In `parent-mobile/`, log in as the invited parent. Confirm the home screen shows a summary reflecting the visible events and the internal note is absent anywhere in the response (inspect the network payload, not just the rendered screen).
3. Confirm a child with zero events today shows a clear empty state, not a blank/broken screen.

## Scenario 3 — Two-way messaging with shared family thread (User Story 2, 0-Scenario 5)

1. Invite a second contact (the other parent) for the same child; complete their registration too.
2. As parent A, start a new thread tied to the child with a message.
3. Confirm parent B (separately logged in) can see the same thread and the same message, without having sent anything themselves (validates FR-003a).
4. As the director in web admin, open `/messages`, find the thread, reply.
5. Confirm both parent A and parent B see the reply in the same thread.

## Scenario 4 — Announcement (User Story 3)

1. As director, compose an announcement scoped to the child's location.
2. Confirm every invited parent contact at that location receives it (check `GET /api/parent/announcements` list / notification centre).
3. Confirm there is no reply affordance on the announcement view.
4. Repeat scoped to a group with zero enrolled children — confirm the send completes without error.

## Scenario 5 — Notification centre + push (User Story 4, 5)

1. Trigger a message reply, an announcement, and a temperature event (`celsius > 38.0`) for the same parent.
2. Open the notification centre — confirm all three appear, most recent first, correctly typed, each navigating to its source.
3. Mark one read; confirm the others' read state is unaffected.
4. With a registered push token (`PUT /api/parent/push-token`), confirm a push notification arrives for the message and announcement cases (temperature already worked pre-013; confirm it now also appears in-app, per research.md R4).
5. Set an invalid push token directly in the DB, repeat step 1's message trigger — confirm the send is logged as failed and the in-app notification still appears.

## Expected outcome

All five scenarios pass without any cross-tenant, cross-family, or `visible_to_parent=false` leakage, and without any manual director action beyond the initial invitations.
