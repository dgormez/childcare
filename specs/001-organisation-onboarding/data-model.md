# Phase 1 Data Model: Organisation Onboarding

Two schemas are involved: the shared **public** schema (via `PublicDbContext`) and a freshly-provisioned **tenant** schema (via the provisioning-only `TenantDbContext` — see [research.md](research.md) R6). Naming note: the constitution states "Tenant = Organisation"; the entity below is named `Tenant` to match the physical `tenants` table BACKLOG.md specifies, while spec.md's business language says "Organisation" — both refer to the same registry record.

## Public schema

### `Tenant` (table: `tenants`)

Registry record for one organisation. Created at the start of registration, before the isolated workspace exists.

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `Name` | `text` | Organisation name, as submitted at registration |
| `Slug` | `text` | Unique. Derived from `Name`; see research.md R14 for collision handling |
| `SchemaName` | `text` | Unique. The Postgres schema identifier, e.g. `tenant_<slug>` |
| `Plan` | `text` | `CHECK (Plan IN ('trial','starter','pro'))`. Defaults to `trial` (FR-016) |
| `ProvisioningStatus` | `text` | `CHECK (ProvisioningStatus IN ('provisioning','ready','failed'))`. See state transitions below |
| `CreatedFromInvitationId` | `uuid` | **UNIQUE**, FK → `invitations.Id`. Not just traceability — this is the concurrency guard for FR-015 and the sole source of truth for whether an invitation has been used (research.md R10) |
| `CreatedAt` | `timestamptz` | |

**State transitions** (supports FR-010, FR-014):

```
(row does not exist)
       │  registration begins: Tenant row inserted (atomically, guarded by
       │  the UNIQUE constraint on CreatedFromInvitationId — research.md R10)
       ▼
 provisioning ──────────────► ready       (schema created + migrated + director user created)
       │
       └──(transient failure)──► failed ──(registration retried against the
                                            SAME Tenant row)──► provisioning ──► ready
```

A `Tenant` is only usable for login once `ProvisioningStatus = 'ready'` (FR-010, FR-011 from spec.md). An invitation is considered "already used" (FR-004) iff a `Tenant` exists with `CreatedFromInvitationId` pointing at it **and** `ProvisioningStatus = 'ready'` — not a separate mutable flag on `Invitation` itself (research.md R10 explains why: a flag set at attempt-start rather than attempt-success would permanently burn the invitation on a transient failure, breaking FR-014's retry guarantee).

### `Invitation` (table: `invitations`)

Single-use, time-limited, email-locked credential. Exists only in the public schema — created before any tenant/organisation exists.

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `Email` | `text` | Normalized lowercase. The *only* email registration may use (FR-018) |
| `TokenHash` | `bytea` | SHA-256 hash of the opaque token; plaintext token is never stored (research.md R4) |
| `ExpiresAt` | `timestamptz` | |
| `CreatedAt` | `timestamptz` | |

**Validity rule**: an invitation may be *attempted* iff `ExpiresAt > now()` (FR-003). Whether it has already led to a *completed* registration is determined via the `Tenant.CreatedFromInvitationId` relationship above, not a column on this table — deliberately, to keep "used" derived from a single source of truth (research.md R10).

**Relationship**: `Invitation` is referenced by at most one `Tenant` (enforced by the `UNIQUE` constraint on `Tenant.CreatedFromInvitationId`), but a fresh invitation has no `Tenant` yet.

## Tenant schema (baseline, provisioned per-organisation)

### `Users` (table: `users`, inside the new tenant schema)

Minimal baseline sufficient for FR-009 (director account exists) and R8 (JWT issuance). This is intentionally the smallest possible shape — feature `003-auth` is expected to *extend* this table (roles, OAuth fields, refresh tokens) via additive migrations, not replace it.

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `Email` | `text` | Unique within this tenant schema (schemas are isolated, so no cross-tenant uniqueness check is needed) |
| `PasswordHash` | `text` | BCrypt, per research.md R12 |
| `Name` | `text` | Director's name, as submitted at registration |
| `CreatedAt` | `timestamptz` | |

No `IsDirector`/role column is added here: with exactly one user in a brand-new tenant schema, role modeling is feature 005's (`Staff`) concern per BACKLOG.md — adding an unused role enum now would be speculative.

## Validation rules (enforced by FluentValidation on `RegisterOrganisationCommand`, not the database alone)

- Organisation name: required, non-empty.
- Director name: required, non-empty.
- Email: required, valid format, must exactly match (case-insensitive) the resolved invitation's `Email` (FR-018).
- Password: required, minimum 8 characters (research.md R12).
- Invitation token: required, must resolve to an `Invitation` with `ExpiresAt > now()` (FR-003, FR-005), and must not already correspond to a `ready` `Tenant` via `Tenant.CreatedFromInvitationId` (FR-004) — see the state-transition note above for why "used" is derived this way rather than a column on `Invitation` itself.

## Out of scope for this data model

- Any column or table needed only by later features (locations, staff roles beyond "director exists", contracts, etc.) — not created here.
- `TenantMiddleware`-facing concerns (resolving `Tenant` from a JWT claim on *arbitrary* subsequent requests) — feature 002 owns the read-side of `Tenant`; this feature only owns the write-side (creating it).
