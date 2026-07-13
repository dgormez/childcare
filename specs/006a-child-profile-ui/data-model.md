# Data Model: Child Profile UI

## Child (extended)

Existing entity (`backend/ChildCare.Domain/Entities/Child.cs`, feature 006), extended with two
new nullable fields. No other field changes.

| Field | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `Guid` | No | Unchanged. |
| `FirstName` | `string` | No | Unchanged. Required on create/edit. |
| `LastName` | `string` | No | Unchanged. Required on create/edit. |
| `DateOfBirth` | `DateOnly` | No | Unchanged. Required on create/edit. Must be ≤ today (existing validator rule). |
| `ProfilePhotoObjectPath` | `string?` | Yes | Unchanged — GCS object path only; set via the existing `RequestChildPhotoUploadUrlCommand` flow, not part of create/update's field set. |
| `Gender` | `Gender?` | Yes | Unchanged. |
| `Nationality` | `string?` | Yes | Unchanged. |
| `AllergiesDescription` | `string?` | Yes | Unchanged. Max length 2000. |
| `AllergySeverity` | `AllergySeverity?` | Yes | Unchanged. |
| `MedicalConditions` | `string?` | Yes | Unchanged. Max length 2000. |
| `DietaryRestrictions` | `string?` | Yes | Unchanged. Max length 2000. |
| `GpName` | `string?` | Yes | Unchanged. Max length 200. |
| `GpPhone` | `string?` | Yes | Unchanged. Max length 30. |
| **`PediatricianName`** | **`string?`** | **Yes** | **New.** Max length 200 (mirrors `GpName`). Independent of `GpName` — no cross-field requirement. |
| **`PediatricianPhone`** | **`string?`** | **Yes** | **New.** Max length 30 (mirrors `GpPhone`). Independent of `GpPhone`. |
| `HealthInsuranceNumber` | `string?` | Yes | Unchanged. Max length 50. |
| `Kindcode` | `string?` | Yes | Unchanged. Max length 20. |
| `DeactivatedAt` | `DateTime?` | Yes | Unchanged — soft-delete flag, untouched by this feature. |
| `CreatedAt` / `UpdatedAt` | `DateTime` | No | Unchanged. |

**Validation rules** (FluentValidation, mirroring `GpName`/`GpPhone`'s existing rules in
`CreateChildCommandValidator`/`UpdateChildCommandValidator`):

- `PediatricianName`: optional, `MaximumLength(200)`.
- `PediatricianPhone`: optional, `MaximumLength(30)`.
- No format validation on phone (matches `GpPhone`'s existing lack of format validation —
  Belgian/international phone formats vary; free text, same as every other contact-phone field
  in this codebase).
- No "at least one of GP/pediatrician" cross-field rule (spec Edge Cases — both independently
  optional).

**State transitions**: None new. Create → Update (repeatable) → Deactivate/Reactivate (existing
006 lifecycle, unaffected by this feature).

## Contracts touched (no new entities)

- `CreateChildCommand` / `CreateChildRequest` — add `PediatricianName`, `PediatricianPhone`
  parameters, appended after `GpPhone` to keep the GP/pediatrician pair visually adjacent.
- `UpdateChildCommand` / `UpdateChildRequest` — same addition.
- `ChildResponse` — add `PediatricianName`, `PediatricianPhone` to the response shape, same
  position.
- `ChildHealthSummaryResponse` — **unchanged** (see research.md R3).

## Web/mobile view models (no new backend contracts — client-side shaping only)

- Web create/edit form state: a plain object mirroring `CreateChildRequest`/`UpdateChildRequest`
  field-for-field (see research.md R5 — `useState`, no react-hook-form).
- Mobile caregiver summary: reads `PediatricianName`/`PediatricianPhone` directly off the
  already-cached `ChildResponse` object (see research.md R3/R4) — no new client-side type beyond
  the two new fields on the existing generated `ChildResponse` type.
