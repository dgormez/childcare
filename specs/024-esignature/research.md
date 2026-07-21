# Phase 0 Research: Digital Contract E-Signature

## R1: Tenant resolution for the public (unauthenticated) signing endpoints

**Decision**: The signing link carries both an organisation slug and the signed token
(`/sign?org={slug}&token={token}`, mirroring `EmailLinkBuilder`'s existing `?token=...&org={slug}`
unsubscribe-link shape) — tenant resolved via `OrganisationSlugResolver` before touching any
tenant data, exactly like feature 023's public enrollment endpoints and feature 020's
unsubscribe/resubscribe endpoints. Each new public endpoint is marked `.RequireTenantExempt()`.

**Rationale**: This is the only existing pattern in the codebase for a public, no-login route
that still needs tenant-schema data (research confirmed via feature 023's own R1). Reusing it
keeps `TenantExemptAttribute`'s deny-by-default guarantee intact.

**Alternatives considered**: Encoding the tenant into the signed token payload itself (so the URL
needs no separate `org` parameter) — rejected; every existing signed-token service in this
codebase (`IUnsubscribeTokenService`, `ITourInvitationTokenService`) carries only the target
entity's id, resolving tenant separately via the URL's own slug segment. Diverging from that
shape for this one token would be a second, inconsistent convention for no real benefit.

## R2: Signing token mechanism

**Decision**: A new `IContractSigningTokenService` (`CreateToken(Guid contractId) : string` /
`TryParseToken(string) : Guid?`, fails closed), implemented via ASP.NET Core Data Protection's
`ToTimeLimitedDataProtector`, purpose string `"Contract.Signing"`, lifetime 72 hours (FR-003) —
directly mirrors `ITourInvitationTokenService`'s exact shape (feature 023), which itself mirrors
`IUnsubscribeTokenService` (feature 020). Single-use enforcement is a separate concern from the
token's own cryptographic validity: `Contract.SigningToken` stores the *current* valid token's
value; on each signing attempt, the presented token must both (a) decrypt successfully to this
contract's id and (b) match the stored `SigningToken` value exactly. Resending (FR-004),
revising (FR-013), or successfully signing all clear/replace `SigningToken`, which is what
actually invalidates a still-cryptographically-valid-but-superseded token — Data Protection
tokens cannot be revoked by themselves.

**Rationale**: Reuses a proven, already-reviewed pattern (three prior features now use this exact
token shape) rather than introducing JWT or a new signing mechanism, per the BACKLOG prompt's
"signed JWT" being illustrative intent ("signed, time-limited link"), not a literal library
requirement — no other feature in this codebase issues a raw JWT for a link purpose.

**Alternatives considered**: A raw JWT signed with a dedicated key — rejected; would introduce a
second unrelated signing mechanism (this codebase's JWTs are exclusively for the auth session,
via ASP.NET Core Identity) alongside the existing Data-Protection-based link-token pattern, for
no functional gain. A database-only opaque token (random string, no cryptographic signing) —
rejected; loses the tamper-evidence and fails-closed-on-corruption properties the existing
pattern already provides for free.

## R3: IBAN encryption at rest

**Decision**: A new `IIbanProtector` (`Protect`/`Unprotect`), implemented via Data Protection
with its own purpose string (`"Contract.SepaIban"`) — directly mirrors `INrnProtector` (feature
022) and `IPaymentTokenProtector` (feature 014a), each scoped to its own purpose so ciphertexts
are never interchangeable across features.

**Rationale**: Same mechanism, third use in this codebase for a different sensitive-financial/PII
field — no new encryption library or key-management surface introduced.

**Alternatives considered**: A custom AES implementation keyed from GCP Secret Manager directly —
rejected; Data Protection already satisfies Constitution Principle VI (no hardcoded
secrets/keyvault-backed) and is the codebase's established convention for exactly this class of
problem.

## R4: IBAN display after capture

**Decision**: `ContractResponse` (director-facing) exposes only a masked IBAN (e.g.
`BE68 •••• •••• 0166`, last 4 digits visible) — never the decrypted full value in any API
response after initial capture, per spec.md FR-020. The signing submission endpoint itself never
echoes the IBAN back either; the signing page's own client-side state (not a server response) is
what the parent sees immediately after typing it.

**Rationale**: Matches this codebase's general PII-minimization posture (e.g. `INrnProtector`'s
consumers never round-trip the plaintext NRN back into a list/detail response either) without
inventing a new pattern.

**Alternatives considered**: Full IBAN visible to directors (who legitimately need it for SEPA
batch generation, feature 026) — deferred to 026, which will decrypt via `IIbanProtector` only at
the point of generating the actual collection batch, not for routine display; this feature's own
UI never needs the full value outside that future batch-generation path.

## R5: Contract-activation interaction

**Decision**: Signing does not gate or trigger the existing `Draft → Active` transition
(`ActivateContractCommand`/`ContractActivationChecker`, feature 007) — confirmed in spec.md's
Clarifications. A director activates a contract exactly as they do today, independent of whether
it has been signed yet, in either order.

**Rationale**: Avoids a breaking behavioral change to four already-shipped features (010, 012a,
014, plus 007 itself) that assume today's Draft/Active/Ended semantics are unconditional. See
spec.md's Clarifications for the full reasoning.

**Alternatives considered**: Auto-activating on signing (re-running
`ContractActivationChecker.CheckAndActivateAsync` from the signing handler) — rejected per
spec.md's Clarifications; would make `Active` mean two different things depending on which path a
contract took to get there, and silently changes what "Active" has guaranteed to every consumer
since 007 shipped.

## R6: Signed PDF storage and immutability

**Decision**: A new `ISignedContractStorage` port (`UploadAsync(Guid contractId, byte[] pdfBytes)`
→ deterministic object path `signed-contracts/{contractId}.pdf`, `CreateDownloadUrlAsync`),
implemented as `GcsSignedContractStorage` following the exact `GcsProfilePhotoStorage`/
`GcsFiscalAttestationStorage` adapter shape (V4 signed URLs, ADC credentials, no public URLs).
Immutability (FR-011: never regenerated) is enforced at the **application layer**, not the
storage layer: `SubmitContractSigningCommandHandler` only ever calls `UploadAsync` once per
contract, gated by the same single-use signing-token check (R2) that already prevents a second
successful submission — there is no code path that re-invokes it for an already-signed contract.
This mirrors how no existing `Gcs*Storage` port self-enforces write-once semantics either
(`IFiscalAttestationStorage.UploadAsync` explicitly documents itself as overwrite-on-regenerate);
the *behavioral* guarantee comes from what calls the port, not from the port refusing a write.

**Rationale**: A storage-layer "refuse to overwrite" check would duplicate a guarantee the
application layer already provides via single-use tokens, adding complexity (existence-check
round-trip to GCS on every upload) without closing any gap a determined caller with direct
database access couldn't already bypass either way.

**Alternatives considered**: A storage-level conditional-write (GCS `ifGenerationMatch: 0`,
"create-only") — considered stronger defense-in-depth, but rejected for this feature since no
existing storage port in this codebase uses conditional writes and introducing the pattern here
alone would be inconsistent; worth revisiting codebase-wide if a real double-write incident ever
occurs.

## R7: Contract-for-signing rendering on the public page

**Decision**: The public signing page fetches contract-for-signing data (child name, location,
contracted days, daily rate, consent flags — the same fields `IContractPdfGenerator`'s
`ContractPdfModel` already renders) via a new public query, and renders it as HTML/React content
directly in the page — not by embedding the existing `GET /api/contracts/{id}/pdf` endpoint's
output in an iframe. The **final signed PDF** (post-submission) is still generated via
`IContractPdfGenerator`-style QuestPDF rendering, extended with a signature block and SEPA
mandate section.

**Rationale**: `GET /api/contracts/{id}/pdf` is `DirectorOnly` (not tenant-exempt) — reusing it
for the public page would mean either weakening its authorization or building a second,
parallel-but-different tenant-exempt PDF endpoint. Rendering the same underlying fields as page
content (not a PDF-in-an-iframe) also gives the scroll-to-bottom gating (FR-006) a normal DOM
scroll event to hook into, which a browser-rendered PDF embed does not reliably expose.

**Alternatives considered**: A second, tenant-exempt PDF-generating endpoint just for the
signing page preview — rejected; doubles the PDF-generation code path for a preview that doesn't
need to be byte-identical to the final signed artifact (only the final, post-signature PDF is the
legal record, per FR-011).

## R8: Signature capture control

**Decision**: A new client-side signature component in `web/` (canvas-based drawing, using the
pointer-events API — no new dependency needed, since native `<canvas>` + pointer events cover
this without a signature-pad library) with a typed-name fallback (a styled text input rendered
in a script-like font), per FR-007's "draw or type." Confirmed via search: no signature-pad
component exists anywhere in this codebase to reuse (`web/`, `mobile/`, `parent-mobile/`).

**Rationale**: A canvas-based signature pad is a well-understood, ~100-line component; adding an
external signature-pad npm package for this single use would be disproportionate given the native
Canvas API already covers it, consistent with this codebase's general preference against
unnecessary dependencies.

**Alternatives considered**: A third-party signature-pad library (e.g. `signature_pad`) —
rejected as an avoidable dependency for functionality the Canvas Pointer Events API already
provides directly.

## R9: Locale resolution for the signing page and its emails

**Decision**: The signing invitation email and the signing page itself default to the contract's
resolved primary `Contact.Locale` (the same `IsPrimary`-ordered `ChildContact` → `Contact` join
`GenerateInvoicePdfQuery` already uses to find the billing-relevant contact for a child), falling
back to `"nl"` if no contact or locale is set — matching this codebase's existing default-locale
convention. The signing page also offers a manual language toggle (consistent with feature 023's
FR-019 precedent), since the parent opening the link may not be the primary contact.

**Rationale**: Reuses an existing, proven contact-resolution query rather than introducing a new
one; a manual toggle covers the case where `Contact.Locale` doesn't match the actual reader.

**Alternatives considered**: Deriving locale from the signing request's `Accept-Language` header —
rejected; every other locale-sensitive flow in this codebase resolves locale from stored data
(`Contact.Locale`), not request headers, and introducing a header-based fallback here would be a
new, inconsistent mechanism.
