# Quickstart: Closure Calendar

## Prerequisites

- PostgreSQL/Testcontainers available for backend integration tests.
- Node dependencies installed under `web/`.
- Existing features through 010 are present.

## Backend Validation

```sh
dotnet test backend/ChildCare.sln
```

Expected coverage:

- Director can create/list/update draft closures by location/year.
- Duplicate `(locationId, date)` is rejected.
- Past dates are rejected using Europe/Brussels calendar day.
- Staff/parent tokens cannot access closure management endpoints.
- Publishing a notify-enabled closure sends push attempts and creates parent closure messages.
- Publishing with `notify_parents = false` creates attendance closure records but no parent messages.
- Same-day closure with checked-in children returns confirmation-required until explicitly confirmed.
- Published closure cancellation sends cancellation messages and releases system-generated closure attendance records.
- Check-in remains rejected against closure attendance records.
- Billable-exclusion query returns only published, non-cancelled closure dates.

## Web Validation

```sh
cd web
npm test -- --run
npm run typecheck
```

Expected coverage:

- Closure page loads locations and closure days for selected year.
- Empty/error/loading states render with i18n strings.
- Creating/editing a closure keeps user-entered values on failure.
- Publish confirmation warns about parent notification and checked-in attendance where applicable.
- Calendar uses type labels/icons in addition to color.

## Manual Smoke Scenario

1. Log in to director web.
2. Open closure calendar.
3. Select a location and year.
4. Add a future holiday closure with notify enabled.
5. Publish it.
6. Verify the calendar shows published state and notification summary.
7. Open attendance for the closure date and confirm child records are marked closure.
8. Cancel the closure and verify cancellation summary plus released system-generated attendance records.
