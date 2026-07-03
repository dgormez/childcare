# Contract: Register Organisation

`POST /api/organisations/register`

Public endpoint (no auth header) — access is gated entirely by possession of a valid invitation token, not by any pre-existing session. Synchronous: does not return success until the organisation, workspace, and director account all exist (spec.md Clarifications, FR-008).

## Request

**Body**

```json
{
  "invitationToken": "opaque-single-use-token",
  "organisationName": "Kinderdagverblijf De Zonnebloem",
  "directorName": "Marie Peeters",
  "email": "director@example.com",
  "password": "a-strong-password"
}
```

| Field | Type | Rules |
|---|---|---|
| `invitationToken` | string | Required. Must resolve to an unused, unexpired invitation (FR-003, FR-004, FR-005) |
| `organisationName` | string | Required, non-empty |
| `directorName` | string | Required, non-empty |
| `email` | string | Required, valid email, must exactly match (case-insensitive) the invitation's target email (FR-018) |
| `password` | string | Required, minimum 8 characters (research.md R12) |

Belgian regulatory identifiers (Opgroeien location reference, KBO/company number) are **not** part of this request — FR-012 requires they remain fillable later, elsewhere.

## Responses

**201 Created** — organisation, workspace, and director account all created and ready.

```json
{
  "accessToken": "jwt...",
  "organisation": {
    "id": "uuid",
    "name": "Kinderdagverblijf De Zonnebloem",
    "slug": "kinderdagverblijf-de-zonnebloem",
    "plan": "trial"
  },
  "director": {
    "id": "uuid",
    "email": "director@example.com",
    "name": "Marie Peeters"
  }
}
```

`accessToken` is a ready-to-use JWT including a `tenant_id` claim (research.md R8) — the director is logged in immediately (FR-011, SC-002); no separate login call is required.

**404 Not Found** — the invitation token does not exist, has expired, or has already been used to complete a registration. All three cases return the **same** status code and the **same** generic error key — deliberately: distinguishing them in the response (even just via a different `errorKey`) would let a caller enumerate which tokens have ever existed, expired, or been claimed. Security takes priority over giving the caller a more specific reason here.

```json
{ "errorKey": "errors.invitation.not_found" }
```

This resolves the status-code question research.md/this contract previously deferred: **404 for all three cases** (not-found, expired, already-used), decided in favor of not revealing token existence over more specific error semantics.

**422 Unprocessable Entity** — validation failure on a *token the caller does possess*, i.e. the submitted email doesn't match the invitation's target email (FR-018). This is intentionally **not** folded into the 404 above: the caller already holds a real, valid invitation in this case, so a specific "wrong email" message doesn't leak anything about *other* tokens — it only tells the caller about the one they already have. Body:

```json
{ "errorKey": "errors.validation", "fieldErrors": { "email": "errors.registration.email_mismatch" } }
```

**500** — provisioning failed partway through (e.g., transient DB error during schema creation/migration). No organisation is left in `ready` state (data-model.md state transitions); the response uses the generic, non-leaking error envelope per constitution Principle VI ("Internal errors... MUST NOT be exposed to end users"):

```json
{ "errorKey": "errors.unexpected" }
```

The **same invitation token remains valid for a retry** in this case — "used" is determined by whether a `ready` `Tenant` exists for this invitation (research.md R10), not by a flag set at attempt-start, so a partially-failed attempt does not burn the invitation (supports FR-014). Retrying resumes work against the same, already-created `Tenant` row rather than creating a second one.

## Concurrency (FR-015)

If two requests submit the same `invitationToken` at nearly the same time, a Postgres `UNIQUE` constraint on `Tenant.CreatedFromInvitationId` (research.md R10) ensures only one request's insert succeeds; that request proceeds to provisioning while the other is redirected to the outcome above — resume-as-retry if the winner hasn't finished yet, or "already used" (`404`, same generic `errors.invitation.not_found` key as the other invalid-invitation cases) if it has. Exactly one organisation is ever created per invitation, never two.
