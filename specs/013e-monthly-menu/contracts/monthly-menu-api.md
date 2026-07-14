# API Contract: Monthly Menu

## Director endpoints (`DirectorOnly`)

### `GET /api/locations/{locationId}/monthly-menus/{year}/{month}`

Returns the current menu for authoring (draft or published) — creates nothing; a location/year/
month with no menu yet returns an empty shell (`exists: false`) so the web form can render blank
inputs.

**Response 200**:

```jsonc
{
  "exists": true,
  "isPublished": true,
  "publishedAt": "2026-07-01T08:00:00Z",   // null if draft
  "days": [
    { "date": "2026-07-01", "soup": "Tomatensoep", "mainCourse": "Kip met puree", "dessert": "Yoghurt", "notes": null },
    { "date": "2026-07-02", "soup": null, "mainCourse": null, "dessert": null, "notes": "Geen warme maaltijd" }
  ]
}
```

### `PUT /api/locations/{locationId}/monthly-menus/{year}/{month}`

Upsert — creates the `MonthlyMenu` row (as a draft) if none exists for this location/year/month,
otherwise updates it; always replaces the full `days` array (director-web form submits the whole
month's grid, not a per-day diff — mirrors 013d's `UpsertMealPreferenceCommand`'s
"whole-record replace on write" shape rather than a partial PATCH). Does **not** change publish
state — publishing/unpublishing is separate (below) so a director can save a draft correction to
an already-published menu without accidentally unpublishing it mid-edit, then explicitly choose
whether to re-publish.

**Request**:

```jsonc
{
  "days": [
    { "date": "2026-07-01", "soup": "Tomatensoep", "mainCourse": "Kip met puree", "dessert": "Yoghurt", "notes": null }
  ]
}
```

**Validation**: every `date` must fall within the URL's `year`/`month`; field lengths per
data-model.md (500 chars each).

**Response 200**: same shape as the `GET` above, reflecting the saved state.

### `POST /api/locations/{locationId}/monthly-menus/{year}/{month}/publish`

Sets `PublishedAt = NOW()`. **404** if no menu exists yet for this location/year/month (nothing to
publish — the director must save at least a draft first).

**Response 200**: `{ "isPublished": true, "publishedAt": "..." }`

### `POST /api/locations/{locationId}/monthly-menus/{year}/{month}/unpublish`

Sets `PublishedAt = null`. **404** if no menu exists.

**Response 200**: `{ "isPublished": false, "publishedAt": null }`

### `GET /api/meal-preference-requests?status=pending`

Director review queue. `status` optional, defaults to `pending`; also accepts `approved`,
`rejected`, or omitted for all.

**Response 200**:

```jsonc
[
  {
    "id": "uuid",
    "childId": "uuid",
    "childName": "Emma Peeters",
    "requestedByName": "Sofie Peeters",
    "newTexture": "normal",             // null if unchanged
    "newDietaryType": ["halal"],        // null if unchanged
    "notes": "Ze kan nu goed kauwen.",
    "status": "pending",
    "createdAt": "2026-07-10T09:00:00Z",
    "decidedAt": null,
    "decisionNotes": null,
    "activeHealthRecords": [            // FR-013 — context for the decision, not filtered/matched
      { "id": "uuid", "recordType": "doctor_note", "title": "Slikprobleem", "validFrom": "2026-06-01", "validUntil": null }
    ]
  }
]
```

### `POST /api/meal-preference-requests/{id}/approve`

Applies the request via `UpsertMealPreferenceCommand` (research.md R1), marks `Approved`, sends
the decision notification (research.md R3). **409** if the request is not currently `Pending`
(already decided).

**Response 200**: the updated request, same shape as the queue item above.

### `POST /api/meal-preference-requests/{id}/reject`

**Request**: `{ "reason": "..." }` (optional, max 2000 chars).

Marks `Rejected`, leaves `MealPreference` untouched, sends the decision notification with the
reason if given. **409** if not currently `Pending`.

**Response 200**: the updated request.

## Parent endpoints (`ParentOnly`)

### `GET /api/parent/monthly-menu?year=&month=`

Defaults to the current Europe/Brussels calendar year/month if omitted (mirrors 009b's group-
activities gallery default, per `ParentEndpoints.cs`'s existing convention). Returns one entry per
distinct location where any of the requesting parent's linked children holds an active contract
(research.md R5).

**Response 200**:

```jsonc
[
  {
    "locationId": "uuid",
    "locationName": "KDV Zonnebloem",
    "isPublished": false,                 // false → web/mobile render "Menu nog niet beschikbaar"
    "days": [
      { "date": "2026-07-01", "soup": "Tomatensoep", "mainCourse": "Kip met puree", "dessert": "Yoghurt" }
    ],
    "closureDates": ["2026-07-21"]        // research.md R4 — greyed out, still shown
  }
]
```

### `GET /api/parent/children/{childId}/meal-preference`

**Response 200**:

```jsonc
{
  "texture": "normal",             // null if no MealPreference row yet ("Geen voorkeur")
  "dietaryType": ["halal"],
  "hasPendingRequest": false       // true → parent app disables/relabels "Voorkeur aanpassen"
}
```

**403** (`errorKey: "errors.parent.not_a_contact"`) if the requester is not linked to `childId` —
same shape as `GET /api/parent/children/{childId}/daily-summary`'s existing failure response.

### `POST /api/parent/children/{childId}/meal-preference-requests`

**Request**: `{ "newTexture": "normal", "newDietaryType": ["halal"], "notes": "..." }` — at least
one of `newTexture`/`newDietaryType` required.

**Response 201**: the created request (`status: "pending"`).

**403** — not linked to this child (same shape as above).
**409** (`errorKey: "errors.meal_preference_requests.duplicate_pending"`) — a pending request
already exists for this child (research.md R6, FR-012).
