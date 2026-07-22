# Quickstart: Digital Contract E-Signature

Validation scenarios proving this feature works end-to-end. Run against local dev
(`docker compose up` PostgreSQL + API) unless noted.

## Prerequisites

- A tenant (`orgSlug` known) with `Tenant.SepaCreditorIdentifier` set, a director account, and a
  `Draft` contract for a child whose primary contact has a valid email.
- SMTP configured (or Mailpit locally) to observe the signing-invitation and signed-PDF emails.

## Scenario 0 — Creditor ID gate

1. On a tenant with no `SepaCreditorIdentifier` set, as director, `POST
   /api/contracts/{id}/signing-invitation` for a `Draft` contract — expect `422
   errors.contract_signing.creditor_id_not_configured` (User Story 4, FR-016).
2. `PUT /api/organisation/settings/sepa-creditor-id` with a value, then repeat step 1 — expect
   `200`.

## Scenario 1 — Send invitation → sign → activation unaffected

1. As director, send a signing invitation for a `Draft` contract — expect `200`, and confirm the
   contract's derived signing status is `pending` (FR-001, FR-002).
2. Confirm an email was sent to the primary contact, in that contact's locale, containing a link
   shaped `/sign?org={orgSlug}&token={token}` (FR-003).
3. `GET /api/public/contracts/sign?org={orgSlug}&token={token}` — expect `200` with the contract's
   terms (child, location, days, rate, consent), matching what `IContractPdfGenerator` renders
   (FR-005).
4. `POST` the same URL with a drawn signature and a valid Belgian IBAN (e.g.
   `BE68539007547034`) — expect `200 {"signed": true}`.
5. Confirm the contract now has `SignedAt`, `SignatureData`/`SignatureType`, `SignedByIp`,
   `SepaMandateReference`, `SepaAuthorisedAt` set, and `SepaIbanEncrypted` is not the plaintext
   IBAN (FR-009, FR-010).
6. Confirm `Contract.Status` is still `Draft` — signing did **not** activate it (spec.md's
   Clarifications, FR-015). Separately call `POST /api/contracts/{id}/activate` and confirm it
   succeeds exactly as it would for an unsigned contract.
7. Confirm a signed PDF exists in storage and was emailed to both the parent and the director
   (FR-011, FR-013).
8. Re-fetch `GET /api/public/contracts/sign?org={orgSlug}&token={token}` with the **same** token —
   expect `404 errors.contract_signing.invalid_or_expired` (FR-012, token is single-use).

## Scenario 2 — Expired link, resend

1. Send an invitation, then advance the contract's `SigningTokenExpiresAt` into the past directly
   (test-only setup) — `GET` the signing link now — expect `404
   errors.contract_signing.invalid_or_expired` (FR-003).
2. As director, `POST /api/contracts/{id}/signing-invitation` again (same endpoint serves resend)
   — expect `200`, a new email sent, and the **old** token now `404`s while the **new** one
   `200`s (FR-004, SC-005).

## Scenario 3 — Revision invalidates an outstanding link

1. Send an invitation (do not sign).
2. As director, `PUT /api/contracts/{id}` with a revised `dailyRateCents`.
3. `GET` the previously issued signing link — expect `404
   errors.contract_signing.invalid_or_expired` (FR-013) — confirm no new invitation was
   auto-sent; the director must call the signing-invitation endpoint again explicitly.

## Scenario 4 — Invalid IBAN doesn't discard the signature

1. Fetch a valid signing link (Scenario 1, step 3).
2. `POST` with a syntactically-invalid IBAN (bad checksum) — expect `422
   errors.contract_signing.invalid_iban` (FR-008).
3. `POST` again with the same signature and a corrected IBAN — expect `200 {"signed": true}`
   (confirms the earlier rejection didn't consume the token or discard the signing session).

## Scenario 5 — Concurrent double-submit

1. Fetch a valid signing link.
2. Fire two `POST` requests against it concurrently with valid, distinct bodies.
3. Confirm exactly one succeeds (`200 {"signed": true}`) and the other receives `404
   errors.contract_signing.invalid_or_expired` (FR-009) — confirm only one `SepaMandateReference`
   and one signed PDF were created for the contract, not two.

## Scenario 6 — Tampered/guessed token

1. `GET /api/public/contracts/sign?org={orgSlug}&token=not-a-real-token` — expect `404
   errors.contract_signing.invalid_or_expired`, with no contract/child/parent data in the
   response body (FR-018, SC-002).
