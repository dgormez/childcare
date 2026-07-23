# Quickstart: Platform-Admin Portal — Invitations, Registration & Organisation Directory

## Prerequisites

- Local dev stack running (`backend/` API + `web/` Next.js app + Docker PostgreSQL).
- A director account with `IsPlatformAdmin = true` — grant one via the existing CLI:

  ```bash
  dotnet run --project backend/ChildCare.Api -- grant-platform-admin you@example.com
  ```

  (Feature 013h's existing mechanism — this feature does not add a new way to grant it.)

## Scenario 1 — Send and complete an invitation end-to-end

1. Log into `web/` as the platform-admin account above.
2. Navigate to **Platform Administration → Invitations**.
3. Create an invitation: email `newdirector@example.com`, organisation note "Test KDV", locale
   Dutch.
4. Confirm the new row shows status **Pending**.
5. In local dev, retrieve the emailed link from the dev SMTP catch-all (e.g. Mailhog/console
   log, per this repo's existing local email setup) — it should look like
   `http://localhost:3000/register?token=...`.
6. Open that link in a private/incognito window (unauthenticated). Confirm the registration
   page loads with the email pre-filled and non-editable.
7. Fill in organisation name, director name, and password; submit.
8. Confirm: the new director can immediately log in with the credentials just entered — no
   approval step (spec.md FR-010).
9. Back in the platform-admin's Invitations screen, confirm the same row now shows **Accepted**
   and no longer offers Resend/Revoke.
10. Navigate to **Platform Administration → Organisations**. Confirm the new organisation
    appears, with `registeredByEmail` matching `newdirector@example.com`.

## Scenario 2 — Revoke and resend

1. Create a second invitation for `another@example.com`.
2. Click **Revoke**. Confirm status becomes **Revoked**.
3. Attempt to open that invitation's emailed link — confirm the registration page shows a
   generic "this invitation link is no longer valid" message (not "revoked" specifically —
   FR-011's indistinguishability requirement).
4. Create a third invitation for `third@example.com`, then click **Resend** before it's used.
   Confirm: the original row becomes **Revoked**, a new row appears as **Pending**, and a new
   email was sent.

## Scenario 3 — Access control

1. Log in as a director WITHOUT `IsPlatformAdmin`.
2. Confirm no "Platform Administration" section appears in the sidebar at all.
3. Attempt to call `GET /api/platform-admin/invitations` directly (e.g. via curl with that
   director's bearer token) — confirm `403 Forbidden`.

## Scenario 4 — Shared shell

1. As the platform-admin, confirm the sidebar's Platform Administration section lists exactly
   three entries: Invitations, Organisations, Vaccine Types.
2. Click each; confirm all three render inside the same shared heading/nav shell.

## Automated validation

- Backend: `dotnet test` — new tests should cover invitation lifecycle (create/resend/revoke/
  status-derivation for all four states), the `PlatformAdminOnly` policy boundary on every new
  endpoint, the organisation-directory read, and a regression test on the existing
  `TenantMigrationRolloutTests`/`LegacyVaccinationMigrationTests` pattern is NOT needed here
  (Public-schema migration only, per data-model.md).
- Web: `npm test` — new component tests for the Invitations table/dialog, the Organisations
  table, the registration page's valid/expired/revoked/already-used states, and the extracted
  shared platform-admin shell/nav.
