# Data Model: ID-Verified Registration

## `IdDocumentType` (new enum, `ChildCare.Domain.Enums`)

```csharp
public enum IdDocumentType
{
    BirthCertificate,
    KidsId,
    Eid,
    Passport,
    Other,
}
```

Wire representation: lowercase snake_case string (`birth_certificate`, `kids_id`, `eid`,
`passport`, `other`), matching this codebase's existing enum-to-string convention elsewhere
(`ChildMapper`'s `Gender`/`AllergySeverity` mapping).

## `Child` (extended)

New columns, all nullable (additive migration, no backfill — every existing row is legitimately
unverified):

| Column | Type | Notes |
|---|---|---|
| `IdVerifiedAt` | `timestamptz?` | Current verification timestamp. Set/overwritten on every verify/correct action (FR-001, FR-005). |
| `IdVerifiedByUserId` | `uuid?` | Current verifying director. No DB-level FK (research.md R1 — attribution field, not a queried relationship, mirrors `VaccineType.DeactivatedByUserId`). |
| `IdVerifiedByEmail` | `text?` | Denormalized director email at time of verification — avoids a join to render "verified by X" (research.md R1). |
| `IdDocumentType` | `IdDocumentType?` | Required alongside `IdVerifiedAt` to consider the record verified (FR-003) — enforced by the command validator, not a DB constraint (this codebase has no precedent for cross-column DB constraints; `AllergySeverity`/`Gender` nullable enums use the same plain-nullable-column shape). |
| `IdDocumentNote` | `text?` | Optional free note, max 500 chars. |
| `FirstIdVerifiedAt` | `timestamptz?` | Set once, on the very first verification. Never overwritten by a later correction (FR-006). |
| `FirstIdVerifiedByUserId` | `uuid?` | First verifying director. Never overwritten. |
| `FirstIdVerifiedByEmail` | `text?` | Denormalized, never overwritten. |
| `EncryptedNrn` | `text?` | Ciphertext (ASP.NET Core Data Protection, research.md R3). Never decrypted by any endpoint in this feature. |
| `NrnLast4` | `text?` | Last 4 digits of the validated, normalized NRN, stored in plaintext at write time (FR-012). |

**Verified state**: a `Child` is considered verified when `IdVerifiedAt` is not null (implies
`IdDocumentType` is also set, per FR-003's enforcement in the command validator — the two are
always set together).

## `Contact` (extended)

Same shape as `Child`'s verification fields, no NRN (spec.md: NRN is child-only):

| Column | Type | Notes |
|---|---|---|
| `IdVerifiedAt` | `timestamptz?` | |
| `IdVerifiedByUserId` | `uuid?` | |
| `IdVerifiedByEmail` | `text?` | |
| `IdDocumentType` | `IdDocumentType?` | |
| `IdDocumentNote` | `text?` | |
| `FirstIdVerifiedAt` | `timestamptz?` | |
| `FirstIdVerifiedByUserId` | `uuid?` | |
| `FirstIdVerifiedByEmail` | `text?` | |

Verification lives on `Contact` itself (not `ChildContact`, the link table) — per spec.md User
Story 2, verifying a contact once covers every child they're linked to.

## State transitions

```text
Unverified (IdVerifiedAt = null)
   │  VerifyChildIdentityCommand / VerifyContactIdentityCommand
   │  (DocumentType required, Note optional)
   ▼
Verified (IdVerifiedAt = now, IdVerifiedByUserId = caller,
          FirstIdVerifiedAt = now, FirstIdVerifiedByUserId = caller)
   │  VerifyChildIdentityCommand / VerifyContactIdentityCommand (again)
   │  (correction — e.g. child turns 12, or fixing a mistaken entry)
   ▼
Verified, corrected (IdVerifiedAt = now, IdVerifiedByUserId = caller — updated;
                      FirstIdVerifiedAt / FirstIdVerifiedByUserId — unchanged)
```

There is no "unverify" transition — not requested by spec.md, and reversing a legal attestation
that identity was seen on a given date would misrepresent history rather than correct it.

## Command/Query shapes

- `VerifyChildIdentityCommand(Guid ChildId, IdDocumentType DocumentType, string? Note, Guid
  VerifiedByUserId, string VerifiedByEmail) : IRequest<ChildResult>` — loads the child, sets
  `First*` fields only if currently null, always sets the current triplet, returns the existing
  `ChildResponse` shape (extended).
- `VerifyContactIdentityCommand(Guid ContactId, IdDocumentType DocumentType, string? Note, Guid
  VerifiedByUserId, string VerifiedByEmail) : IRequest<ContactResult>` — same shape, on `Contact`.
- `SetChildNrnCommand(Guid ChildId, string Nrn) : IRequest<ChildResult>` — validates format
  (research.md R4), encrypts via `INrnProtector`, stores `EncryptedNrn`/`NrnLast4`.
- `GetDataCompletenessQuery` (extended) — adds a `missing_identity_verification` flag per active
  (`DeactivatedAt == null`) child without `IdVerifiedAt`, using its own independent child-scoping
  query rather than the handler's existing attendance-linked `childIds` (research.md R5).

## Contract (response) additions

`ChildResponse` gains: `IdVerifiedAt`, `IdVerifiedByEmail`, `IdDocumentType`, `IdDocumentNote`,
`FirstIdVerifiedAt`, `FirstIdVerifiedByEmail`, `NrnLast4`. Never `EncryptedNrn` or
`IdVerifiedByUserId`/`FirstIdVerifiedByUserId` (email is sufficient for display; raw user IDs
aren't rendered anywhere else in this codebase's responses either).

`ContactResponse` and `ChildContactResponse` both gain: `IdVerifiedAt`, `IdVerifiedByEmail`,
`IdDocumentType`, `IdDocumentNote`, `FirstIdVerifiedAt`, `FirstIdVerifiedByEmail` — the latter so
`ChildContactsTab`'s existing per-row list can render the badge without a second request.
