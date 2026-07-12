# Research: Incident Reports

## R1 — `reported_by` resolution reuses `IShiftAttributionService`, not a new mechanism

**Decision**: `FileIncidentReportCommandHandler` calls the existing `IShiftAttributionService.
ResolveRecordedByAsync(locationId, groupId, occurredAtUtc, ct)` (feature 009,
`backend/ChildCare.Application/RoomShifts/ShiftAttributionService.cs`) to populate
`IncidentReport.ReportedBy` (`List<Guid>`), exactly the same call `RecordChildEventCommand` already
makes for `ChildEvent.RecordedBy`.

**Rationale**: This spec's Clarifications session already ruled out a PIN-confirmed single
identity (contradicts offline filing). Reusing the exact existing service — rather than writing a
second query against `RoomShifts` — means one code path computes "who was on shift" for both
`child_events` and `incident_reports`, and any future bugfix to that resolution logic (e.g. a
future edge case around overlapping shifts) automatically applies to both.

**Alternatives considered**: A dedicated `IIncidentReportAttributionService` wrapping the same
query — rejected as needless duplication of an existing, already-tested service with an identical
signature need (`locationId`, `groupId`, `occurredAtUtc` → `List<Guid>`).

## R2 — PDF generation mirrors `IContractPdfGenerator` exactly

**Decision**: New `IIncidentReportPdfGenerator` interface
(`backend/ChildCare.Application/Common/IIncidentReportPdfGenerator.cs`), implemented by
`QuestPdfIncidentReportGenerator` (`backend/ChildCare.Infrastructure/Pdf/`), following
`QuestPdfContractGenerator`'s exact structure: a `{Feature}PdfModel` DTO built by the query handler
from `IncidentReport` + `Location` (name/address/`Dossiernummer`), rendered via QuestPDF, returned
as `byte[]` and streamed directly via `Results.File(bytes, "application/pdf")` from
`IncidentReportEndpoints.cs` — no GCS upload, matching feature 007's contract-PDF precedent (a
generated document is regenerated on each request, not stored).

**Rationale**: Feature 007 already established this project's one PDF pattern
(port/adapter split, locale-keyed labels via `?locale=nl|fr|en`); constitution's Technology Stack
Constraints name QuestPDF as the only PDF library. Introducing a second PDF-generation shape would
violate that precedent for no benefit.

**Alternatives considered**: none seriously — this is a documented, established pattern this
project's own shipping notes call out for future PDF features (007's shipped-notes) to reuse.

## R3 — Director "notification" substitutes an in-app reviewed/unreviewed flag

**Decision**: `IncidentReport.ReviewedAt` (`DateTimeOffset?`, null = unreviewed) is set the first
time `GetIncidentReportQuery` (the detail-view read) is called by a director for that report — no
separate "mark reviewed" user action is required, though `MarkIncidentReportReviewedCommand` also
exists for the (unlikely but harmless) case of a future bulk-review UI. `ReviewedAt` is never reset
by subsequent edits (spec Clarifications).

**Rationale**: Already resolved in spec.md's Clarifications/Assumptions — no director push channel
exists (`TenantUser` has no push token; feature 013f reached the identical conclusion for an
analogous ask). Setting `ReviewedAt` as a side effect of the detail-view read (rather than a
separate click) keeps the caregiver/director interaction to "open it to see it's handled," matching
how an unread-email-style indicator behaves elsewhere in consumer software, and avoids adding a
UI-only "mark as read" button that has no other purpose.

**Alternatives considered**: A distinct `POST /incident-reports/{id}/mark-reviewed` the web UI
calls on open (client-triggered) — rejected in favor of the read-query itself setting it
server-side, since a client-triggered call adds a network round-trip and a failure mode (the mark
call fails silently) for no benefit over doing it atomically in the same query.

## R4 — Offline sync entity registration follows `childEvents.ts` exactly

**Decision**: `mobile/services/incidentReports.ts` calls `registerSyncHandler("incident_report",
{ onConflict: () => "discard" })` — identical shape to `child_event`'s registration. No
`onBeforeEnqueue` needed (no merge-into-pending-create scenario like child-event's sleep-end
PATCH — an incident report has no comparable in-flight-edit-before-sync case since it's rare enough
that no realistic double-submission scenario needs special handling beyond the sync engine's
existing append-only replay).

**Rationale**: Feature 008's sync engine is generic infrastructure precisely so new entity types
register a handler rather than reimplementing queueing/replay — reusing the exact registration
call this codebase already established for `child_event`/`child_event_batch`/`attendance_record`/
`group_activity` is the established convention, not a new decision.

**Alternatives considered**: none — this is infrastructure reuse, not a design choice with
tradeoffs.

## R5 — `Location.Dossiernummer` is the PDF's identifier field (premise reconciliation)

**Decision**: The PDF prints `Location.Name`, `Location.Address`, and `Location.Dossiernummer`
(nullable) where the original brief said "erkenningsnummer." Already documented in spec.md
Assumptions — restated here since it affects `IncidentReportPdfModel`'s field mapping directly.

**Rationale**: No field literally named `erkenningsnummer` was ever shipped (004's actual fields:
`NaamLocatie`, `Dossiernummer`, `Verantwoordelijke`, `FlexPermission`, `BoPermission`).
`Dossiernummer` (Opgroeien location identifier) is the closest existing analog and the field a
director would actually have filled in for exactly this kind of regulatory-identifier purpose.

**Alternatives considered**: Adding a new `Location.Erkenningsnummer` field — rejected as scope
creep onto feature 004's entity for a single PDF label; `Dossiernummer` already serves the same
real-world purpose (a government-issued childcare identifier) closely enough that a second,
functionally-duplicate field would just be confusing to a director filling in location settings.
