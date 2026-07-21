# Contract: Digital Contract E-Signature API

Public (anonymous, `.RequireTenantExempt()`) endpoints resolve tenant from an `org` slug query
parameter (research.md R1), never a JWT claim. Director-facing endpoints reuse the existing
`DirectorOnly` policy on the `/api/contracts` group — no new authorization policy invented.

## `GET /api/public/contracts/sign?org={orgSlug}&token={token}`

Anonymous, tenant-exempt. Lets the signing page render the contract, or a generic error, before
any signature is attempted.

- Resolves `org` → tenant (`OrganisationSlugResolver`), then `token` →
  `IContractSigningTokenService.TryParseToken` → contract id. Either failing, or the token not
  matching the contract's current stored `SigningToken`, or `SigningTokenExpiresAt` in the past,
  or `SignedAt` already set — all collapse to the same response (FR-005, fails closed, no
  distinction observable to the caller):
  - `404 errors.contract_signing.invalid_or_expired`
- `200` (valid, unused, unexpired token):
  ```json
  {
    "childName": "string",
    "locationName": "string",
    "contractedDays": [{ "weekday": "Monday", "startTime": "08:00", "endTime": "18:00" }],
    "dailyRateCents": 4500,
    "consent": { "photosInternal": true, "photosWebsite": false, "photosSocialMedia": false, "videoInternal": true, "photosPress": false },
    "locale": "nl"
  }
  ```
  Same field set `IContractPdfGenerator`'s `ContractPdfModel` already renders (research.md R7) —
  nothing beyond what the presented token already authorizes for this one contract (FR-018).

## `POST /api/public/contracts/sign?org={orgSlug}&token={token}`

Anonymous, tenant-exempt, no rate-limit policy beyond the token's own single-use/expiry
enforcement (unlike feature 023's public-enrollment `POST`, this endpoint has no honeypot/spam
concern — a signing link is only ever reachable via a specific emailed token, not an open form).

Request:
```json
{
  "signatureType": "Drawn" | "Typed",
  "signatureData": "string",
  "sepaIban": "string"
}
```

Validation (FluentValidation pipeline behavior, per Constitution III):
- `signatureType`/`signatureData`: required.
- `sepaIban`: required, valid IBAN format + checksum (mod-97) — `422
  errors.contract_signing.invalid_iban` on failure (FR-008), signature data is **not** discarded
  client-side on this rejection (the client re-submits with the same drawn/typed signature once
  the IBAN is corrected).

Server-side, inside a single transaction (FR-009/FR-010):
1. Re-validates the token exactly as the `GET` above (fails closed, same generic error) — the
   single-use check here is the operative one; the `GET`'s check is UX-only.
2. Records `SignedAt = now`, `SignatureData`, `SignatureType`, `SignedByIp`, a newly generated
   unique `SepaMandateReference`, `SepaIbanEncrypted` (`IIbanProtector.Protect`),
   `SepaAuthorisedAt = now`.
3. Clears `SigningToken`/`SigningTokenExpiresAt` (invalidates the token — it cannot be reused even
   within its remaining cryptographic lifetime).
4. Generates the final signed PDF (contract fields + signature block + SEPA mandate, extending
   `IContractPdfGenerator`) and uploads it via `ISignedContractStorage.UploadAsync` (research.md
   R6).
5. Commits, then (outside the transaction, fire-and-forget per spec.md's Performance
   Considerations) emails the signed PDF to the parent and to the location's director(s) — FR-013.

Responses:
- `404 errors.contract_signing.invalid_or_expired` — same collapsing as the `GET` (expired, used,
  tampered, or a concurrent second submission that lost the race — FR-009's single-use guarantee;
  see Edge Cases).
- `422 errors.contract_signing.invalid_iban` — checksum/format failure.
- `200`:
  ```json
  { "signed": true }
  ```
  No IBAN or signature data echoed back (research.md R4).

## `POST /api/contracts/{id}/signing-invitation`

`DirectorOnly` (existing `/api/contracts` group).

- Requires `Contract.Status == Draft` — `409 errors.contract.not_draft` otherwise (only a Draft
  contract can be sent, per spec.md's Clarifications).
- Requires a resolvable primary contact with a non-null `Email` (research.md R9's
  `IsPrimary`-ordered join) — `422 errors.contract_signing.no_contact_email` otherwise (FR-001).
- Requires `Tenant.SepaCreditorIdentifier` to be set — `422
  errors.contract_signing.creditor_id_not_configured` otherwise (FR-016, directs the director to
  organisation settings).
- Generates a new token (`IContractSigningTokenService.CreateToken`), sets `SigningToken`/
  `SigningTokenExpiresAt = now + 72h` (overwriting any previous outstanding token — FR-002/FR-004,
  a resend and a first send are the same operation), sends the signing-invitation email in the
  resolved contact's `Locale` (research.md R9).
- `200`: the updated `ContractResponse` (now including the derived signing status — see
  data-model.md).
- `404 errors.contract.not_found` — unchanged convention.

This single endpoint serves both "send" and "resend" (User Stories 1 and 3) — there is no
separate resend route, since the operation is identical either way.

## `PUT /api/contracts/{id}` (existing, feature 007 — extended)

No new route. `UpdateContractCommandHandler` gains two additional steps (correction from an
earlier draft of this doc, caught by `/speckit-analyze`: since signing does **not** change
`Status` — FR-015 — the existing `Status != Draft` check alone does **not** block edits to a
signed-but-still-`Draft` contract; a dedicated check is required):

1. If `contract.SignedAt` is set, reject with a new `ContractFailure.AlreadySigned` (`409
   errors.contract.already_signed`) **before** the existing `Status != Draft` check even runs —
   this is what actually enforces FR-014's "a signed contract's terms are frozen; revise via
   amendment instead," independent of `Status`.
2. Otherwise, if the contract has a non-null `SigningToken` (an outstanding, unsigned
   invitation), clear `SigningToken`/`SigningTokenExpiresAt` as part of the same save (FR-013).

## `PUT /api/organisations/me` (existing, feature 014 — extended)

No new route. `UpdateOrganisationCommand`/`UpdateOrganisationRequest`/`OrganisationResponse` gain
`SepaCreditorIdentifier` alongside the existing `KboNumber` — same org-wide, `DirectorOnly`,
public-schema `Tenant` field, updated the same way `KboNumber` already is (User Story 4). No
dedicated single-field endpoint is introduced; `GetCurrentOrganisationQuery` (`GET
/api/organisations/me`) is extended the same way so the current value can be read back.

Request (extended):
```json
{ "kboNumber": "string | null", "sepaCreditorIdentifier": "string | null" }
```
- Required (non-empty) before `POST /api/contracts/{id}/signing-invitation` will succeed
  (FR-016).
