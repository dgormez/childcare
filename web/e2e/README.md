# E2E tests (Playwright)

Full browser tests that drive the real director UI against a real backend — complements the
mocked component tests in `web/__tests__`, which don't catch cross-screen or integration bugs.

## Prerequisites

1. Postgres running: `docker compose up -d` (repo root).
2. Backend API running: `dotnet run` in `backend/ChildCare.Api` (defaults to `http://localhost:5001`).
3. `web/.env.e2e` present — copy `.env.e2e.example` and fill in `E2E_SUPERADMIN_API_KEY` from
   `backend/ChildCare.Api/appsettings.Development.json` (`SuperAdmin:ApiKey`).

The web app itself (`npm run dev`) is started for you by `playwright.config.ts`.

## Running

```bash
npm run test:e2e        # headless, CI-style
npm run test:e2e:ui     # Playwright's interactive UI mode — best for writing/debugging specs
```

If the backend API was *just* started, the first batch of parallel seed calls can hit it before
it's finished warming up (connection-refused/aggregate errors from `seedDirector`). Give it a
few seconds after `dotnet run` prints "Application started" before running the suite, or just
re-run — it's not a real failure.

Occasionally a single test fails a "no duplicate element" strict-mode check (e.g. an empty-state
message matching twice) under heavy parallel load, then passes cleanly alone or on rerun. This is
React 19 dev-mode Strict Mode double-invoking effects, not a real bug — see `staff.spec.ts`'s
"a failed load..." test for how that's worked around where it actually matters (always-fail a
route rather than fail-once-then-pass, since the second Strict Mode invocation would otherwise
race the first).

## Maintenance: cleaning up test orgs

Every run provisions a brand-new Postgres schema per seeded org and never tears it down mid-run
— unique orgs mean tests never collide or need a reset step. Left unchecked across many runs
this accumulates hundreds of schemas, which measurably slows every tenant-scoped request (real
incident: a run started timing out after ~200 had piled up). Run this periodically — it's a
manual step, not wired into the test run automatically, since it does a wildcard `DROP SCHEMA
CASCADE` against your local Postgres:

```bash
npm run test:e2e:cleanup
```

## How seeding works

`e2e/support/seed.ts` creates a brand-new organisation + director per test run, entirely through
the backend's own HTTP API (superadmin invitation → `/api/organisations/register`) — no direct
DB access, so there's nothing to reset between runs and no shared fixture data to go stale.

`e2e/support/fixtures.ts` extends Playwright's `test` with two fixtures:

- `director` — a seeded org/director, if a spec wants the credentials without a UI login.
- `directorPage` — a `page` already logged in and sitting on `/staff`, for specs that need an
  authenticated starting point.

## What's here so far

- `login.spec.ts` — director login: valid credentials, wrong password, unknown org, unknown
  email, empty-field client validation, session persistence across reload.
- `staff.spec.ts` — search, PIN reset (valid/invalid format), deactivate/reactivate, empty
  state, load-error/retry. No create flow — see KNOWN_GAPS.md.
- `locations.spec.ts` — list/detail navigation, editing (valid/empty-name), empty state,
  load-error/retry. No create flow — see KNOWN_GAPS.md.
- `closures.spec.ts` — add (future date), reject past date, reject duplicate date, publish,
  cancel, remove draft, load-error/retry.
- `waiting-list.spec.ts` — add entry, required-field validation, reorder, offer→enroll,
  withdraw, enroll→create-new-child-record, load-error/retry.
- `scheduling.spec.ts` — add shift, overlap rejection, mark/unmark absent, delete, copy week,
  empty state, load-error/retry.
- `attendance.spec.ts` — checked-in child shows present, correct check-out time, switch to
  absent with justification, empty state, load-error/retry. Seeds a real attendance record by
  acting as a paired kiosk device (director JWT can't check a child in directly).
- `announcements.spec.ts` — compose/send, disabled-until-filled validation, empty state,
  load-error/retry.
- `messages.spec.ts` — invite an eligible parent contact, a parent-started thread appears and
  can be replied to, empty state, load-error/retry. Seeds a real parent session via
  `seedParent()` (see "Maintenance" below).
- `requests.spec.ts` — approve/reject a pending day-reservation request, empty state,
  load-error/retry. Seeds a real pending request via a parent session + an active Mon–Fri
  contract (`seedActiveContract()`).

Still to add: devices, groups.

## Next

Same pattern extends to the remaining director flows — add a `<feature>.spec.ts` per area,
using `directorPage` as the starting fixture and extending `support/seed.ts` when a flow needs
data the UI itself can't create (see KNOWN_GAPS.md for what that currently covers). Caregiver
and parent personas live in `mobile/` and `parent-mobile/`; those will use Maestro instead of
Playwright, against the same seeded-backend approach, once the director suite is complete.
