# API Contract: Meal List

## `GET /api/locations/{locationId}/meal-list`

**Query params**: `date` (required, `yyyy-MM-dd`), `includeExpected` (optional bool, default
`false` — powers the "Inclusief verwacht" toggle).

**Authorization**: `DeviceOrStaffOrDirector`.
- Director/Staff (user-JWT): may query any `locationId` within their own tenant.
- Device token (caregiver tablet, feature 008a): the endpoint ignores any group filter the
  client might pass and instead scopes the response to the device's own `GroupId` claim
  (`DeviceTokenClaims.GroupId`) — the device can only ever see its own room's meal list,
  regardless of `locationId` requested, mirroring `RoomShiftEndpoints`' existing scoping pattern.

**Response 200** — `MealListResponse`:

```jsonc
{
  "date": "2026-07-13",
  "groups": [
    {
      "groupId": "uuid",
      "groupName": "Butterflies",
      "children": [
        {
          "childId": "uuid",
          "firstName": "Emma",
          "lastName": "Peeters",
          "texture": "pureed",           // pureed | mixed | pieces | normal
          "dietaryType": ["halal"],       // [] if none
          "portionSize": "normal",        // small | normal | large
          "additionalNotes": null,
          "hasPreference": false,         // false → render "Geen voorkeur" regardless of defaults above
          "allergySeverity": "severe",    // severe | mild_moderate | none
          "hasStandingMedication": true
        }
      ]
    }
  ],
  "expected": {                          // present only when includeExpected=true
    "children": [
      { "childId": "uuid", "firstName": "Liam", "lastName": "Janssens", "...": "same shape as above, no groupId nesting" }
    ]
  }
}
```

**Response 404**: `locationId` not found in the caller's tenant (`errors.location.not_found`).

**Never present**: any child whose `AttendanceRecord.Status` for `date` is `Absent` or `Closure`,
outside the `expected` block (which itself only ever contains children with **no** attendance
record yet, never `Absent`/`Closure` children).

## `GET /api/children/{childId}/meal-preferences`

**Authorization**: `DirectorOnly`.

Additive read added during implementation (not in the original plan) — the child-profile edit
form needs the child's *current* meal preference to pre-fill, since the `PUT` below is a
partial-upsert (an omitted field means "no change", so the form must know what "current" is).

**Response 200** — `MealPreferenceResponse`. A child with no `child_meal_preferences` row
returns the column defaults (`texture: "normal"`, `dietaryType: []`, `portionSize: "normal"`,
`additionalNotes: null`), not a `404` — mirrors FR-005's "Geen voorkeur" default for this
single-child read too.

**Response 404**: `childId` not found in the caller's tenant (`errors.child.not_found`).

## `PUT /api/children/{childId}/meal-preferences`

**Authorization**: `DirectorOnly`.

**Request** — `UpsertMealPreferenceRequest`:

```jsonc
{
  "texture": "mixed",
  "dietaryType": ["vegetarian", "gluten_free"],
  "portionSize": "small",
  "additionalNotes": "Prefers no dairy at breakfast"
}
```

All fields optional in the request; omitted fields fall back to their column defaults
(`normal`/`[]`/`normal`/`null`) only on first creation — on update, an omitted field is treated as
"no change" (partial upsert), not reset to default.

**Response 200** — the resulting `MealPreferenceResponse` (mirrors the request shape plus
`updatedAt`/`updatedBy`).

**Response 404**: `childId` not found in the caller's tenant, or child is deactivated
(`errors.child.not_found`).

**Response 422**: validation failure (e.g. `additionalNotes` exceeds max length) —
`errors.meal_preferences.*` per this codebase's standard FluentValidation → `422
Unprocessable Entity` mapping (confirmed against `HealthRecordValidationTests`' precedent).

## Non-goals

- No endpoint reachable by the parent app — this API is never registered under any
  `ParentOnly`-adjacent policy group.
- No PDF endpoint — printing is client-side CSS only (`web/app/(app)/meal-list/`'s print
  stylesheet), per spec.md's explicit "no PDF needed."
