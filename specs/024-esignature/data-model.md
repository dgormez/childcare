# Phase 1 Data Model: Digital Contract E-Signature

## Contract (existing, feature 007 — extended)

New nullable columns, all defaulting every existing row to "not sent":

| Field | Type | Notes |
|---|---|---|
| `SigningToken` | `string?` | The *current* valid signing token's value (opaque string, the Data-Protection-protected payload — see research.md R2). Cleared/replaced on resend, on revision, and on successful signing. Not the cryptographic secret itself — the token's tamper-evidence comes from Data Protection; this column is purely the single-use/invalidation marker. |
| `SigningTokenExpiresAt` | `DateTime?` (UTC) | Set to `now + 72h` whenever `SigningToken` is (re)issued. |
| `SignedAt` | `DateTime?` (UTC) | Set once, on successful signing. Non-null is the source of truth for "this contract is signed" — no separate boolean. |
| `SignatureData` | `string?` | Base64 PNG (drawn) or the typed name text (FR-007) — stored as-is, distinguished by a `SignatureType` enum (below), not by sniffing the string's shape. |
| `SignatureType` | `SignatureType?` (enum: `Drawn`, `Typed`) | New enum, `backend/ChildCare.Domain/Enums/SignatureType.cs`. |
| `SignedByIp` | `string?` | Captured from the signing request (`HttpContext.Connection.RemoteIpAddress`), stored as text (not a Postgres `inet` column — no other entity in this codebase uses `inet`, and a plain string avoids introducing EF Core's less-common `IPAddress` mapping for a field that's audit-only, never queried by network range). |
| `SepaIbanEncrypted` | `string?` | `IIbanProtector`-protected ciphertext (research.md R3). Column named `*Encrypted` to make at-rest state unambiguous at the call site, mirroring `Child.NationalRegisterNumberEncrypted`'s existing naming convention. |
| `SepaMandateReference` | `string?` | System-generated, unique per signing (FR-017) — format: `MND-{8 uppercase alphanumeric chars}`, same unambiguous-alphabet generator feature 023 already established for its reference codes (excludes `0/O`, `1/I/l`), re-rolled on collision. |
| `SepaAuthorisedAt` | `DateTime?` (UTC) | Set together with `SignedAt` in the same transaction — the spec's single signing session captures both (FR-009). |

No new `ContractStatus` value. Signing status shown to a director is **derived**, not stored:

```text
not_sent   := SigningToken is null AND SignedAt is null
pending    := SigningToken is not null AND SignedAt is null AND SigningTokenExpiresAt > now
expired    := SigningToken is not null AND SignedAt is null AND SigningTokenExpiresAt <= now
signed     := SignedAt is not null
```

## Tenant (existing, feature 001 — extended)

| Field | Type | Notes |
|---|---|---|
| `SepaCreditorIdentifier` | `string?` | Director-entered once at the organisation level (User Story 4), mirrors the existing `KboNumber` field's shape (nullable, free-text, director-set via an organisation-settings command). Required (validated at the command level, not a DB constraint) before `SendContractSigningInvitationCommand` will succeed (FR-016). |

## New enum: `SignatureType`

`backend/ChildCare.Domain/Enums/SignatureType.cs` — `Drawn | Typed`.

## Relationships (unchanged)

`Contract` → `Child` (existing `ChildId`) → `ChildContact` (join, `IsPrimary`) → `Contact`
(existing `Email`, `Locale`) is the existing path used to resolve "the parent to email" — the
same `IsPrimary`-ordered join `GenerateInvoicePdfQuery` already uses (research.md R9). No new
relationship is introduced; this feature only reads through the existing one.

## Migration

One EF Core migration, `AddContractSigningAndSepaMandate`, touching `Contract` and `Tenant` (both
in the tenant schema — `Tenant`'s per-tenant `SepaCreditorIdentifier` is a column on the shared
`tenants` table in `PublicDbContext`, matching where `KboNumber` already lives). Manually-run SQL
script under `specs/024-esignature/migrations/`, per `.claude/CLAUDE.md` and the `008a` precedent
research.md's R1 already cites.
