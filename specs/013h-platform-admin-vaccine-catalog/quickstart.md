# Quickstart: Platform-Admin Vaccine Catalog Management

## Prerequisites

- Local backend running against Docker PostgreSQL (constitution's local-dev database setup).
- At least one `Ready` tenant with a director account (e.g. seeded via an earlier feature's
  integration-test fixtures, or a locally registered organisation).
- `web/` dev server running (`npm run dev`), pointed at the local backend.

## Grant the flag (one-time, per account)

```bash
cd backend
dotnet run --project ChildCare.Api -- grant-platform-admin dgormez@gmail.com
```

Expected output: one line per `Ready` tenant scanned, and a summary
(`Summary: 1/N tenants matched.`) if the account exists in exactly one tenant, matching the
existing `migrate-tenants`/`backfill-growth-check` command output shape.

## Validate: platform-admin can manage the catalog

1. Log in to the web admin (`web/`) with the flagged director account.
2. Navigate to the platform-admin catalog management route (visible in the sidebar only for this
   account — contracts/platform-admin-vaccine-types-api.md's `GET
   /api/platform-admin/vaccine-types`).
3. Create a new entry (e.g. name "Test Vaccine", category "Recommended, not free"). Confirm it
   appears in the management list immediately.
4. In a separate browser/incognito session, call `GET /api/vaccine-types` as an ordinary director
   (013g's existing endpoint) for any tenant — confirm the new entry is visible there too, with no
   propagation delay (FR-011).
5. Rename the entry. Re-check `GET /api/vaccine-types` — confirm the new name appears.
6. Use the up/down reorder buttons to move the entry. Re-check `GET /api/vaccine-types` — confirm
   the ordering changed.
7. Deactivate the entry. Confirm: (a) it disappears from what a director's vaccine-record picker
   would offer (013g's existing active-only behavior, unchanged); (b) it still appears in the
   platform-admin's management list, marked inactive, showing who deactivated it and when.
8. Reactivate the entry. Confirm the audit fields clear and the entry is selectable again.

## Validate: non-platform-admin director is denied

1. Log in as an ordinary director (no `IsPlatformAdmin` flag).
2. Confirm the platform-admin catalog management route is not present in the sidebar.
3. Call `POST /api/platform-admin/vaccine-types` directly (e.g. via curl with that director's
   token) — confirm `403 Forbidden`.

## Validate: 013g's existing read endpoint is unaffected

Run 013g's existing `VaccineTypeEndpoints` test suite unmodified — it must still pass exactly as
before, confirming this feature added no regression to the tenant-facing read path (FR-010,
contracts' "Explicitly unchanged" section).
