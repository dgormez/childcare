# Contract: Caregiver Read Access to Children & Groups

`GET /api/children` and `GET /api/groups` are each split into their own `StaffOrDirector`-authorized route group, separate from their existing `DirectorOnly` write-route groups in the same endpoint file (`ChildrenEndpoints.cs`/`GroupsEndpoints.cs`). Every other route (create/update/deactivate/reactivate/photo-upload, group creation) remains exactly `DirectorOnly`, unchanged.

## `GET /api/children`

New optional query params: `groupId` (`Guid?`).

- `200` — `ChildResponse[]`.
  - Called by a `Director`: identical behavior to feature 006 — every child in the tenant (or every child in `groupId`, if supplied), subject to the existing `includeDeactivated` filter.
  - Called by a `Staff` caregiver: results are additionally restricted to children with a currently-active `ChildGroupAssignment` (`EndDate IS NULL`) whose group's location is one the caller is eligible for (`StaffLocationEligibility`). If `groupId` is supplied and that group is not at one of the caller's eligible locations, the response is `200` with an empty array (the group simply isn't in the caller's scope) rather than an error — consistent with treating out-of-scope data as invisible, not as a client error.

## `GET /api/groups`

Existing optional query param: `locationId`.

- `200` — `GroupResponse[]`.
  - Called by a `Director`: unchanged from feature 006 — every group in the tenant (or filtered to `locationId`, if supplied).
  - Called by a `Staff` caregiver: results are restricted to groups whose `LocationId` is one the caller is eligible for, regardless of whether `locationId` is also supplied (an explicit `locationId` outside the caller's eligible set yields an empty array, same reasoning as above).

## Mobile call sequence (group view, US2)

1. `GET /api/staff/me` (once at login, cached in the auth slice for the session) → the caregiver's own name/role for display purposes (e.g. an app-shell greeting). **Not required** for the calls below to be correctly scoped — that scoping happens entirely server-side from the JWT identity, regardless of what the client does or doesn't know about its own eligible locations.
2. `GET /api/groups` (server-scoped automatically per the caller's role — the client does not need to pass `locationId`, and does not need to know its own `eligibleLocationIds`, for this to be correctly scoped).
3. For each returned group: `GET /api/children?groupId={id}`.
4. Client-side, flatten and de-duplicate the per-group child lists into the single group-view list (a child cannot have two simultaneous active assignments per feature 006's own data model, so no de-duplication should actually be needed in practice — implemented defensively regardless).
