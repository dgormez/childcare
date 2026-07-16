# Research: Fiscal Attestations

## R1: Persisted PDF vs. on-demand rendering

**Decision**: Fiscal attestations are rendered once and persisted to GCS, read back via a
time-limited signed URL — not rendered fresh on every view like 014's invoice PDF or 014a's
betalingsbewijs.

**Rationale**: BACKLOG.md's own 015 prompt block specifies a `pdf_gcs_path` field, diverging from
014/014a's explicit "never persisted" precedent. This divergence is deliberate, not an oversight:
a fiscal attestation is a document a parent files with the tax authority — it benefits from being
a stable, byte-identical snapshot of what was declared at generation time, and User Story 3's
"correct and re-send" flow implies an explicit act of replacing a specific prior document, not a
live recomputation that could silently drift if paid-invoice data changes after the fact (e.g. a
later correction to an unrelated invoice must never retroactively alter an attestation a parent
already filed, until the director explicitly regenerates it).

Two GCS write patterns already exist in this codebase:
- Client-signed-upload (`IHealthAttachmentStorage`/`GcsHealthAttachmentStorage`, 013c): the
  client uploads directly to a signed PUT URL; the backend never touches the bytes. Wrong shape
  here — nothing is client-uploaded.
- Server-side direct write (`IGroupActivityPhotoStorage`/`GcsGroupActivityPhotoStorage`, 009b):
  the backend computes bytes in-process (image resize) and writes them directly to GCS via
  `StorageClient.UploadObjectAsync`, using the API's own credentials — reads still go through
  `UrlSigner`. This is the correct precedent: the backend renders the PDF (QuestPDF, in-process)
  and writes it directly, exactly like 009b's resize-then-upload.

**Alternatives considered**: On-demand rendering (014/014a's pattern) — rejected because it
doesn't give a stable snapshot for a filed legal document, and the spec's explicit "regenerate to
correct" flow (User Story 3) only makes sense against a persisted prior version to replace.

## R2: PDF generator port

**Decision**: New `IFiscalAttestationPdfGenerator` (`ChildCare.Application/Common/`) +
`QuestPdfFiscalAttestationGenerator` (`ChildCare.Infrastructure/Pdf/`), structurally identical to
`IInvoicePdfGenerator`/`QuestPdfInvoiceGenerator` — a `GenerateAsync(FiscalAttestationPdfModel)
-> Task<byte[]>` port, with a per-locale `Labels` dictionary for the declaration/certification
text (Constitution IV).

**Rationale**: QuestPDF is the constitution's only permitted PDF library; `QuestPdfInvoiceGenerator`
is the closest existing precedent for a KDV-identity + person + period-breakdown document.

## R3: Period aggregation from Paid invoices

**Decision**: A new `FiscalAttestationAggregator` (Application layer, shared by both the bulk and
single-child/location commands) computes, for one (ChildId, LocationId, TaxYear):

1. Query `Invoice` rows where `ChildId`/`LocationId` match, `Status == Paid`, and `PeriodMonth`
   falls within the tax year (Jan–Dec).
2. For each invoice, read its already-stored `LineItems.DailyRateCents` (no join to `Contract`
   needed — the rate actually charged that month is already on the invoice, per `InvoiceLineItems`,
   014) and its `PresentDays + UnjustifiedAbsentDays` as that month's billable day count (the
   same day count `Invoice.SubtotalCents` was itself derived from).
3. Sort by `PeriodMonth` and merge consecutive months sharing the same `DailyRateCents` into one
   period: `PeriodStart` = the first merged month's first day, `PeriodEnd` = the last merged
   month's last day, `Days` = summed billable days, `AmountCents` = summed `Invoice.TotalCents`
   (the actual amount paid, including any extra charges — not `Days * DailyRateCents`, which
   would silently drop extra charges), `DailyRateCents` = the shared rate.
4. If merging produces more than four periods, consolidate the oldest overflow periods into the
   earliest retained period (sum `Days`/`AmountCents`, leave `DailyRateCents` null for the merged
   period — spec.md Edge Cases/FR-004) — an intentionally rare case.
5. A child/location combination with zero `Paid` invoices in the tax year produces no periods and
   is skipped entirely (FR-003), not an empty/zero attestation.

**Rationale**: Reuses data already computed and stored by 014 rather than re-deriving from
`Contract` (which would require re-implementing 014's own billable-day calculation). Sourcing
`AmountCents` from `Invoice.TotalCents` (not a days×rate recomputation) guarantees the
attestation always matches what was actually collected, per spec.md FR-002.

**Alternatives considered**: Joining to `Contract` for the daily rate — rejected as redundant and
riskier (a contract's `DailyRateCents` could theoretically be edited after the fact in ways the
already-invoiced `LineItems.DailyRateCents` snapshot is immune to; using the invoice's own stored
value is strictly more accurate to what was actually billed that month).

## R4: Bulk-generation execution model

**Decision**: `GenerateFiscalAttestationsCommand(int TaxYear)` runs synchronously within the
triggering HTTP request, iterating every child with at least one `Paid` invoice in that tax year
(across all of the organisation's locations), calling the aggregator + PDF generator + storage
write per (child, location) pair, with each iteration wrapped in its own try/catch so one child's
failure doesn't abort the batch (FR-010) — the same per-item-loop shape as 014's
`GenerateInvoicesCommand`, scaled from "one location/month" to "one organisation/tax year."

**Rationale**: This codebase has no recurring/background job runner (014a's research confirmed
this — only manually-invoked CLI commands exist). A year-end, director-initiated action for a
realistic KDV's child count (tens to low hundreds, not thousands) stays within an acceptable
synchronous HTTP request bound, matching the existing UX precedent of 014's own bulk generation
button. If a future organisation's scale makes this impractical, the CLI/background-job pattern
014a introduced is the natural next step — not needed for this feature's scope.

**Alternatives considered**: An async job + polling endpoint — rejected as introducing new
infrastructure (job queue, status polling) with no existing precedent in this codebase, for a
once-a-year action whose realistic scale doesn't yet require it.

## R5: Notification integration

**Decision**: `FiscalAttestationNotificationService`, structurally identical to
`InvoiceNotificationService` (014) — for every contact linked to the child, writes an in-app
`Notification` row (`TenantUserId` present) with dedicated `TitleKey`/`BodyKey`
(`parent.notifications.fiscal_attestation_ready.*`) and attempts a best-effort Expo push if the
contact has a push token. A new `NotificationType.FiscalAttestationGenerated` enum value is
added. Called on both initial generation and regeneration (spec.md FR-016, Clarifications).

**Rationale**: Directly follows the Clarifications session's resolved answer — reusing the exact
established pattern rather than inventing a new one.

## R6: Multi-location attestations

**Decision**: `FiscalAttestation` is keyed by (ChildId, LocationId, TaxYear), not just
(ChildId, TaxYear) — if a child has `Paid` invoices at two locations within the same tax year,
the aggregator (R3) runs once per location, producing two independent attestation rows/PDFs, each
carrying only that location's `Name`/`Address`/`KboNumber` (from `Tenant`, org-wide, identical on
both)/`Erkenningsnummer` (per-`Location`, differs).

**Rationale**: `Location.Erkenningsnummer` (the childcare operating licence number) is
per-location, not per-organisation (`Location.cs:45-49`) — the official attest must state the
correct site's licence number, so a single cross-location attestation would be factually wrong
for whichever location it didn't name. This resolves the spec.md Edge Cases item on child
transfers.

## R7: Director-web and parent-mobile placement

**Decision**: Director web gets a new top-level sidebar entry (`/fiscal-attestations`,
`FileCheck2` lucide icon), matching `invoices`' existing flat placement — `Sidebar.tsx`'s
`REAL_NAV` has no "Billing" parent grouping to nest under (every feature area is a flat sibling
entry). Parent-mobile gets a new `fiscal-attestations` screen under the app's existing
authenticated route group, with a `services/fiscalAttestations.ts` service mirroring
`services/invoices.ts`'s exact `downloadInvoicePdf`-via-`expo-file-system`/`expo-sharing`
pattern (014).

**Rationale**: Matches this codebase's established navigation conventions on both platforms
exactly — no new UI pattern introduced for either.
