# Contract: Create Invitation

`POST /api/admin/invitations`

Operator-only. Gated by a shared-secret header, not organisation-user JWT auth (research.md R11 — an explicitly temporary Phase 1 measure).

## Request

**Headers**

| Header | Required | Notes |
|---|---|---|
| `X-Superadmin-Key` | Yes | Compared (constant-time) against the configured operator credential. Missing/wrong → `401`. |

**Body**

```json
{
  "email": "director@example.com"
}
```

| Field | Type | Rules |
|---|---|---|
| `email` | string | Required, valid email format |

## Responses

**201 Created** — invitation created.

```json
{
  "invitationId": "b2e6...-uuid",
  "email": "director@example.com",
  "token": "opaque-single-use-token",
  "expiresAt": "2026-07-09T00:00:00Z"
}
```

The plaintext `token` is returned **exactly once**, in this response, and is never retrievable again (only its hash is stored — research.md R4). It is the operator's responsibility to deliver it to the invitee (e.g., paste into an email) — dispatch itself is out of scope (spec.md Assumptions).

**401 Unauthorized** — missing or incorrect `X-Superadmin-Key`. Body uses the standard i18n error-key envelope (constitution Principle IV):

```json
{ "errorKey": "errors.unauthorized" }
```

**422 Unprocessable Entity** — validation failure (e.g., malformed email). Body:

```json
{ "errorKey": "errors.validation", "fieldErrors": { "email": "errors.invitation.email_invalid" } }
```

## Notes

- No rate limiting policy is attached in this feature (single, low-volume, credential-gated operator action). Revisit if this endpoint becomes higher-traffic.
- Does not check whether the email already has a pending, unexpired invitation or an existing organisation — out of scope; not required by any FR in spec.md. An operator issuing a redundant invitation simply results in an extra, eventually-expiring row.
