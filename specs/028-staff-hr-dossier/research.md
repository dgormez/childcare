# Research: Staff HR Dossier & Time Registration

## R1: Function representation is new, not a reuse of `QualificationLevel`

**Decision**: Add a new `StaffTimeEntryFunction` enum (`Kinderbegeleider`, `Logistiek`,
`Verantwoordelijke`) and a new `StaffProfile.TimeEntryFunctions: List<StaffTimeEntryFunction>`
field (director-configured, at least one required to clock in — FR-010).

**Rationale**: Confirmed by reading `backend/ChildCare.Domain/Entities/StaffProfile.cs` and
`backend/ChildCare.Domain/Enums/QualificationLevel.cs` — the existing `QualificationLevel` enum
(`QualifiedCaregiver`, `Auxiliary`, `StudentVolunteer`) is a training-level concept used only by
`GetBkrRatioQuery`'s ratio calculation. It has no values matching the medewerkersbeleid subsidy's
required categories. Reusing it would conflate two different regulatory concepts.

**Alternatives considered**: Extending `QualificationLevel` with the three new values — rejected,
would make `GetBkrRatioQuery`'s existing filter (`!= StudentVolunteer`) ambiguous against values
it was never designed to classify.

**List-of-enum storage**: Mirrors `StaffProfile.ContractedDays: List<DayOfWeek>` (feature 027) —
same EF Core value-conversion idiom (`HasConversion` to a Postgres `text[]`), same file, same
pattern already reviewed and shipped once.

## R2: Clock in/out identity resolution mirrors feature 027's `GetStaffMeQuery` exactly

**Decision**: `ClockInCommand`/`ClockOutCommand` take no client-supplied staff ID. The endpoint
resolves `TenantUserId` from `ctx.User.FindFirst(ClaimTypes.NameIdentifier)`, exactly as
`StaffEndpoints.cs:137` already does, then looks up the `StaffProfile` by `TenantUserId` inside
the handler (same as `GetStaffMeQuery`).

**Rationale**: Staff-mobile has no device-token concept (confirmed: `staff-mobile/services/
apiClient.ts` comment states this explicitly) — the JWT *is* the identity boundary. Trusting a
client-supplied staff ID for a write action would let one staff member clock in as another.

## R3: Document storage mirrors `IHealthAttachmentStorage`, not `IProfilePhotoStorage`

**Decision**: New `IStaffDocumentStorage` port, same shape as `IHealthAttachmentStorage`
(`backend/ChildCare.Application/Common/IHealthAttachmentStorage.cs`): `CreateUploadUrlAsync(Guid
staffId, string contentType, string category = "staff-documents", ...)`,
`CreateDownloadUrlAsync`, `CreateAttachmentDownloadUrlAsync`, `DeleteAsync`.
`SetStorageClassAsync` is omitted — feature 031's cost-tiering lifecycle is scoped to
child/staff-photo and health-attachment objects; extending it to HR documents is out of scope
here and not requested.

**Rationale**: `IProfilePhotoStorage` hardcodes a single-photo-per-subject `.jpg` path — wrong for
a dossier with multiple, variably-typed documents per staff member (contracts, training
certificates). `IHealthAttachmentStorage`'s `(subjectId, contentType, category)` shape already
solves exactly this problem for a different document class.

## R4: Time-entry lock is computed, with an explicit, non-expiring unlock override

**Decision**: `StaffTimeEntry.UnlockedAt: DateTime?`. Lock state is computed as
`UnlockedAt is null && DateTime.UtcNow - ClockedInAt > TimeSpan.FromDays(7)`. `UnlockTimeEntry
Command` sets `UnlockedAt = DateTime.UtcNow`; a corrected entry stays unlocked until a director
explicitly re-locks it (`RelockTimeEntryCommand` clears `UnlockedAt` back to `null`) — no
scheduled sweep job.

**Rationale**: Feature 013b's `UpdateIncidentReportCommand` (`isLocked = UtcNow - CreatedAt >
24h`) is the closest precedent for a computed time-based lock, but it has no unlock mechanism at
all — this feature's FR-007 explicitly requires one, which 013b never needed. A boolean-via-
timestamp override (rather than a separate `IsUnlocked` bool) doubles as an audit trail (when was
this last unlocked) at no extra cost.

## R5: Child-hours for the subsidy report reads `AttendanceRecord`, excluding incomplete records

**Decision**: Child-hours for a location/period = `Σ (CheckOutAt - CheckInAt)` over
`AttendanceRecord` rows at that location with `Date` in the period and both `CheckInAt` and
`CheckOutAt` non-null. Records with `CheckOutAt == null` (still present, or an uncorrected
same-day gap) are excluded — mirrors FR-019's identical treatment of open staff time entries, for
the same reason: an incomplete record's duration is unknown, not zero.

**Rationale**: `AttendanceRecord` (`backend/ChildCare.Domain/Entities/AttendanceRecord.cs`,
feature 010) is the only existing source of child presence duration in the codebase — confirmed
no other entity tracks child-hours.

## R6: Report and CSV export follow feature 018's `ReportingEndpoints`/`ExportAttendanceSummaryQuery` shape

**Decision**: Add `GetStaffHoursReportQuery` (on-screen JSON) and `ExportStaffHoursReportQuery`
(CSV only — no PDF, unlike 018's attendance-summary export, since spec.md's FR-020 only asks for
CSV) to the existing `ReportingEndpoints.cs` group (`/api/reports/staff-hours`,
`/api/reports/staff-hours/export`), `DirectorOnly`, alongside 018's existing report endpoints.
The CSV export reuses the same aggregation the on-screen query uses (via `IMediator.Send` inside
the export handler), exactly as `ExportAttendanceSummaryQueryHandler` already does, so the two
never disagree.

**Rationale**: This is an established, already-reviewed pattern in this exact file — no reason to
invent a new reporting module or endpoint group for one more report.

## R7: New tenant migration needs the recurring revert-helper fix

**Decision**: The new `staff_time_entries`/`staff_documents` migration must extend both
`backend/ChildCare.Api.Tests/TenantMigrationRolloutTests.cs` and
`backend/ChildCare.Api.Tests/VaccineRecords/LegacyVaccinationMigrationTests.cs`'s schema-revert
helpers (drop the two new tables, in FK-safe order — `staff_time_entries`/`staff_documents`
before nothing, since neither is referenced by an earlier table).

**Rationale**: Every migration-adding feature since 012a has hit this exact gap (012a, 013c, 006a,
013d, 013g, 013h, 014, 014a, 015, 025, 026, 027 — all logged in `process-next-feature.md`'s
progress log). Naming it here up front, per that log's own recurring advice to grep broadly
rather than trust memory of "the two usual files."

## R8: Contract-expiry dashboard block mirrors `DueSoonBlock.tsx` exactly

**Decision**: New `web/components/staff/ContractExpiryBlock.tsx`, structurally identical to
`web/components/health/DueSoonBlock.tsx` (feature 013c) — `loading`/`loaded`/`error` states,
`EmptyState`/`ErrorState`/`Badge` reuse, a clickable list navigating to the staff member's new
detail page. Mounted in `web/app/(app)/dashboard/page.tsx` alongside the existing
`DueSoonBlock`/reporting sections.

**Rationale**: `DueSoonBlock` is this codebase's established, already-reviewed pattern for
exactly this shape of alert (a list of "things expiring soon," clickable to the detail record).

## R9: First staff detail screen (`staff/[id]`) — new, mirrors `children/[id]`'s tab pattern

**Decision**: `web/app/(app)/staff/[id]/page.tsx` is a new screen (staff currently has no detail
page — `web/app/(app)/staff/page.tsx` is list-only, with edits handled via inline dialogs). Two
tabs: **Dossier** (document upload/list, contract dates, the `TimeEntryFunctions` multi-select)
and **Tijdsregistraties** (time-entry list, clock-out correction, lock/unlock). Mirrors
`web/app/(app)/children/[id]/page.tsx`'s `Tabs`/`TabsList`/`TabsTrigger`/`TabsContent` structure
(006a "Profiel" / 013c "Gezondheid").

**Rationale**: Same lesson feature 013c's shipped-note already drew ("a feature whose spec only
lightly implies a screen can still require building the whole screen category from scratch if
none exists yet") — FR-007/008/009 (director corrects/unlocks time entries) and FR-011/012 (dossier
document management) both need a real UI surface, and no existing screen has room for either.

## R10: Staff-mobile clock in/out UI location

**Decision**: Add the clock in/out action to `staff-mobile/app/(app)/index.tsx` (the existing
staff-mobile home screen) rather than a new route — it's the single highest-frequency action on
this surface (platform-rules.md's frontline-speed principle), so it belongs on the screen staff
land on first, not behind navigation.

**Rationale**: Consistent with `platform-rules.md`'s Caregiver Tablet section applied to
staff-mobile's equivalent "frontline speed" concern, and with feature 027's own `report-sick.tsx`
precedent of using connectivity-gated, always-online actions with no offline queue (confirmed:
`staff-mobile/hooks/useIsOffline.ts` is a point-in-time check only).
