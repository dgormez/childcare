# Interface Contract: Monthly Menu Import CSV Format

This feature has no new HTTP API surface (it reuses 013e's existing `PUT /api/locations/
{locationId}/monthly-menus/{year}/{month}` unchanged). The interface it *does* introduce is a
file-format contract: what a director's CSV must look like to be imported. This document is that
contract, analogous to 013e's `contracts/monthly-menu-api.md` but for a file format instead of an
HTTP endpoint.

## Expected format

- Standard comma-separated CSV, UTF-8-compatible encoding, one header row followed by one row per
  calendar day.
- Header row (case-insensitive match): `date,soup,main_course,dessert,notes`
- Quoted fields (`"..."`) are supported for values containing a comma, quote, or newline
  (standard RFC 4180 quoting, handled by the `papaparse` library — see `research.md`).
- Unrecognized/extra columns are ignored (FR-004).
- Missing optional columns (`soup`, `main_course`, `dessert`, `notes`) are treated as blank —
  identical to leaving a cell blank in the manual day grid.

## Example

```csv
date,soup,main_course,dessert,notes
2026-08-01,Tomatensoep,Kip met puree,Yoghurt,
2026-08-02,,Pasta bolognese,Fruit,Geen warme maaltijd voor kind met allergie
2026-08-03,Erwtensoep,"Vis, aardappelen, groenten",Pudding,
```

## Column contract

| Column | Required | Maps to | Validation |
|---|---|---|---|
| `date` | Yes | day identity (which grid row this row applies to) | Must parse as a real calendar date (FR-005); must fall within the currently-selected year/month (FR-006); must not duplicate another row's date in the same file (FR-007) |
| `soup` | No | `DayFields.soup` | Max 500 characters (FR-008) |
| `main_course` | No | `DayFields.mainCourse` | Max 500 characters (FR-008) |
| `dessert` | No | `DayFields.dessert` | Max 500 characters (FR-008) |
| `notes` | No | `DayFields.notes` | Max 500 characters (FR-008) |

## Template download

The "Download template" action (FR-016) produces exactly this format: the header row above plus
one example data row dated the first day of the grid's currently-selected month, so the
downloaded file is immediately valid if re-uploaded unmodified.

## Failure modes

| Condition | Behavior |
|---|---|
| File is not valid CSV at all (wrong delimiter, missing header, binary content) | Single top-level `fileLevelError`; no rows processed (FR-015) |
| File has a header row but zero data rows | Treated as zero valid rows — rejection message, grid untouched (FR-014) |
| Every row fails validation | Same as above — rejection message, grid untouched (FR-014) |
| Some rows valid, some invalid | Valid rows proceed to the preview as applicable; invalid rows are listed with their specific reason and excluded (FR-009, FR-010) |

## Stability

This is a first version — no versioning scheme exists yet for the CSV format itself (unlike the
HTTP API, which has no version header either, per existing `web/lib/apiClient` conventions). If
the column set changes in a future feature, the template download and the parser must be updated
together; there is no backward-compatibility contract with previously-downloaded templates beyond
"same column names, same order-independent CSV header matching."
