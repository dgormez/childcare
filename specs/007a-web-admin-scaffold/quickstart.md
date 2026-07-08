# Quickstart: Web Admin Scaffold

## Prerequisites

- Backend running locally (`dotnet run --project backend/ChildCare.Api`), Postgres available
  (Docker Compose, per existing `SETUP_CHECKLIST.md`).
- A seeded tenant with at least one Director account, a few Staff profiles (mix of active and
  deactivated, at least one with a PIN already set), and at least one paired device (some
  active, one revoked) — reuse the seed/test-support fixtures already established by features
  005/008a's integration tests, or seed manually via those existing endpoints.
- `web/.env.local` with `NEXT_PUBLIC_API_BASE_URL` pointing at the local backend.

## Setup

```bash
cd web
npm install
npm run generate-api-client   # openapi-typescript against the running backend's /openapi/v1.json
npm run dev
```

## Validation scenarios

### 1. Login (email/password) → sidebar shell (User Story 1)

1. Open `http://localhost:3000` → redirected to `/login`.
2. Submit valid director credentials.
3. **Expect**: redirected into the app shell; sidebar shows the organisation name (from
   `GET /api/organisations/me`) and the director's name (from the login response's
   `user.name`).
4. Refresh the browser tab.
5. **Expect**: still signed in (session restored via the httpOnly refresh cookie), no re-login
   prompt.
6. Click "Sign out".
7. **Expect**: redirected to `/login`; refreshing no longer restores a session.

### 2. Login failure

1. Submit an incorrect password.
2. **Expect**: a clear, localized inline error message; still on the login screen; no raw
   HTTP status or stack trace shown.

### 3. Staff list, search, and actions (User Story 2)

1. Navigate to Staff (default landing content).
2. **Expect**: a table of staff members — name, role, location(s), status — loaded from
   `GET /api/staff`.
3. Type a partial name into the search field.
4. **Expect**: the table filters client-side with no full page reload.
5. Choose "Reset PIN" on a caregiver row, enter a new 4-digit PIN, confirm.
6. **Expect**: a success confirmation; the underlying `PUT /api/staff/{id}/pin` call succeeds.
7. Attempt to set a PIN that collides with another caregiver's PIN at the same location.
8. **Expect**: the `409 errors.pin.not_unique_at_location` response surfaces as a clear inline
   form error, not a generic failure.
9. Choose "Deactivate" on an active staff member, confirm in the dialog.
10. **Expect**: the row updates to a deactivated visual state; `POST /api/staff/{id}/deactivate`
    was called only after explicit confirmation.
11. Reactivate the same staff member.
12. **Expect**: the row returns to active.

### 4. Staff empty/error states

1. Against a tenant with zero staff, open the Staff screen.
2. **Expect**: an empty state (icon + one sentence), not a blank table.
3. Stop the backend, reload the Staff screen.
4. **Expect**: a retryable inline error state, not a blank screen or unhandled exception.

### 5. Devices list and revoke (User Story 3)

1. Navigate to Devices.
2. **Expect**: a table of paired devices — location, group, paired-by, paired-at — loaded from
   the new `GET /api/devices` endpoint.
3. Choose "Revoke" on an active device, confirm in the dialog.
4. **Expect**: the device is marked revoked (visually distinguished or removed from the active
   view, per implementation choice); `POST /api/devices/{id}/revoke` was called only after
   explicit confirmation.
5. Against a tenant with zero paired devices, open the Devices screen.
6. **Expect**: an empty state, not a blank table.

### 6. i18n

1. Switch the browser/app locale to French, then Dutch (mechanism per `next-intl` setup chosen
   during implementation).
2. **Expect**: login form, sidebar labels, table headers, empty/error states, and confirmation
   dialogs all render in the selected language — no leftover hardcoded English strings.

## Automated coverage (Vitest, backend xUnit)

- `web/__tests__/auth.test.ts` (extended): login success/failure, session persistence/restore,
  logout.
- `web/__tests__/staff.test.ts` (new): table render, search/filter, PIN reset (success +
  conflict), deactivate/reactivate, empty state, error state.
- `web/__tests__/devices.test.ts` (new): table render, revoke, empty state.
- `backend/ChildCare.Api.Tests/DeviceListingTests.cs` (new): `GET /api/devices` returns correct
  summaries, tenant isolation, empty tenant → `[]`.
- `backend/ChildCare.Api.Tests/OrganisationEndpointTests.cs` (new): `GET /api/organisations/me`
  returns the correct tenant name, `DirectorOnly` enforcement.
- `backend/ChildCare.Api.Tests/AuthEndpointTests.cs` (extended): all four auth flows return
  `user.name` correctly.
