# Contract: Staff Self-Service (`GET /api/staff/me`)

Standalone route, **not** inside the existing `/api/staff` `DirectorOnly` group (research.md R6 — ASP.NET Core composes group + route policies additively, so a more permissive per-route policy cannot live inside a stricter group). Requires the `StaffOrDirector` policy and is **not** tenant-exempt.

## `GET /api/staff/me`

- `200` — `StaffMeResponse { staffProfileId, firstName, lastName, role, eligibleLocationIds[] }` — resolved via the caller's JWT `ClaimTypes.NameIdentifier` claim matched against `StaffProfile.TenantUserId`.
- `404 errors.staff.profile_not_found` — the authenticated `TenantUser` has no associated `StaffProfile` (should not occur for `staff`/`director` roles in practice, since both provisioning paths create one, but handled rather than 500ing).
