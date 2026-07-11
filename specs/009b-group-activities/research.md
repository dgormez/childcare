# Research: Group Activities

## R1 — `recorded_by` resolution (reuse, not new)

**Decision**: Reuse `IShiftAttributionService.ResolveRecordedByAsync(locationId, groupId, occurredAtUtc)` (feature 008a/009, `backend/ChildCare.Application/RoomShifts/IShiftAttributionService.cs`) verbatim. `GroupActivity.RecordedBy` is a `List<Guid>` populated the same way `ChildEvent.RecordedBy` is — the set of caregivers checked into that location/group at `OccurredAt`, possibly empty, never null.

**Rationale**: This is the exact problem feature 009 already solved (spec.md's Assumptions section documents the same reasoning). Building a second resolver would duplicate logic for an identical query shape.

**Alternatives considered**: A dedicated `GroupActivityAttributionService` — rejected, no behavioral difference from the existing service; would only add an unnecessary interface for the sake of feature-local ownership.

## R2 — Photo storage: new port, not an extension of `IProfilePhotoStorage`

**Decision**: Add a new port, `IGroupActivityPhotoStorage`, rather than extending `IProfilePhotoStorage`.

**Rationale**: `IProfilePhotoStorage` (`backend/ChildCare.Application/Common/IProfilePhotoStorage.cs`) has a load-bearing invariant — exactly one deterministic object per `(category, subjectId)` (`{category}/{subjectId}/photo.jpg`), so a re-upload overwrites cleanly (feature 005 FR-013). Group activities need zero-to-ten photos per activity, each independently addressable and deletable. Bending the existing port's contract to support a list would break that invariant for its two existing callers (staff/child profile photos). A second, narrow port keeps both contracts simple and honest about what each guarantees.

**Alternatives considered**: (a) Generalize `IProfilePhotoStorage` to `(category, subjectId, photoId)` — rejected, forces every existing single-photo call site to pass a meaningless constant `photoId`, and still doesn't solve R3 below (resize). (b) A generic "blob bag" port with no resize awareness — rejected as under-specified; resize is a hard functional requirement (FR-004), not an add-on.

## R3 — Server-side resize/thumbnail requires bytes to pass through the API, not a direct-to-GCS signed PUT

**Decision**: Photo bytes are uploaded from the client to the API (`POST /api/group-activities/{id}/photos`, multipart), not via a client-side signed PUT straight to GCS. The API resizes (max 1920px long edge) and generates a 400px thumbnail in-process using `SixLabors.ImageSharp`, then writes both objects to GCS directly using the service's own GCS credentials (`Google.Cloud.Storage.V1`'s `UploadObjectAsync`, already a project dependency).

**Rationale**: Every existing photo flow (`IProfilePhotoStorage`) is signed-URL, direct-to-GCS, specifically so the API "never proxies image bytes" (its own doc comment). That pattern works because those photos need no server-side processing. FR-004 requires actual resizing, which has to happen somewhere with compute — and constitution Principle VII (Monolith-First Simplicity) rules out standing up a separate resize service (a GCS-trigger Cloud Function, a Pub/Sub worker) for one feature. Doing the resize inside the existing monolith API, synchronously on upload, is the simplest option consistent with that principle. This is a deliberate, documented deviation from the "never proxy bytes" convention for profile photos — the two use cases have genuinely different requirements (identity/immutable single photo vs. processed, multi-photo album), not an accidental inconsistency.

**Alternatives considered**: (a) Client-side resize (Expo's `expo-image-manipulator`) before a direct-to-GCS signed PUT — rejected: cross-platform resize-quality/consistency is harder to guarantee client-side, and thumbnail generation still needs a second pass; also breaks offline-then-resume semantics (a signed PUT URL from `CreateUploadUrlAsync` expires in 15 minutes, too short to survive a multi-hour offline queue). (b) Async post-processing (upload raw, a background job resizes later) — rejected as unnecessary complexity for MVP; synchronous resize of a ≤10MB image is fast enough not to need a job queue, and simpler to reason about for the offline-retry story (R7).

**Licensing note** (flagged the same way feature 001 flagged MediatR's licensing, `BACKLOG.md` R18): `SixLabors.ImageSharp` is free under the Six Labors Split License for organisations under a revenue threshold; this project qualifies today. **Correction found during implementation**: version 4.0.0 requires a paid license key even to build (a hard build-time failure, `No Six Labors license found`) — a stricter posture than the Split License free tier this note originally assumed applied uniformly. Pinned to `3.1.12` (the latest 3.x release, confirmed still under the Split License with no build-time key requirement) instead. Revisit both this pin and the MediatR flag together if the project ever crosses the revenue threshold.

## R4 — Group timeline: new aggregation query, no existing one to extend

**Decision**: A new query, `GetGroupTimelineQuery(GroupId, DateOnly)`, merges `ChildEvent` rows (that group/date, `VisibleToParent` irrelevant here — this is the staff-facing timeline, same as `GET /api/child-events` today) and `GroupActivity` rows for the same group/date into one chronologically ordered response. Used by both the caregiver-tablet endpoint (today only, device-scoped) and the director-web endpoint (date-parameterized, `DirectorOnly`).

**Rationale**: Confirmed via research that no group-scoped timeline exists anywhere today — the only existing feed is per-child (`GetDailySummaryQuery`). Building one query reused by both surfaces (rather than two near-identical ones) follows this codebase's established pattern of one handler behind multiple endpoints with different auth (e.g. `GetDailySummaryQuery` reused by both the caregiver and parent endpoints).

**Alternatives considered**: Separate caregiver/director queries — rejected, no behavioral difference beyond auth and the optional date parameter, which the endpoint layer already handles per this codebase's convention (auth/claims resolved at the endpoint, not duplicated into the query).

## R5 — Parent daily feed: extend the existing daily-summary query, not a new endpoint

**Decision**: `GetDailySummaryQuery`/`GetParentDailySummaryQuery` (`backend/ChildCare.Application/ChildEvents/GetDailySummaryQuery.cs`, `backend/ChildCare.Application/Parent/GetParentDailySummaryQuery.cs`) is extended to also resolve the child's group as of the requested date (via `ChildGroupAssignment`, `EndDate IS NULL OR EndDate >= date`) and append that group's activities for the date, each with its photos consent-filtered per R6.

**Rationale**: `GetDailySummaryQuery`'s own code comment already flags "Photos are explicitly out of scope — see [013]'s spec.md" as a known gap this feature is meant to close, and feature 013's parent communication surface already renders this query's output as "today's feed" — extending it keeps activities in the same chronological feed parents already look at, rather than a second feed they'd have to know to check. This mirrors 009b's spec.md User Story 2 ("appear... alongside individual events").

**Alternatives considered**: A separate `GET /api/parent/group-activities` endpoint the client merges client-side — rejected: pushes chronological-merge logic into three separate frontends (parent app screens), when the backend already owns this merge for child events; also risks the two feeds visibly drifting out of sync (e.g. different date-boundary handling).

## R6 — Photo consent gating: query-time filter on the child's active contract, reusing the existing predicate shape

**Decision**: When serving photos to a parent (daily feed or gallery), resolve the viewing child's active contract at the relevant location/date (`Status == ContractStatus.Active`, date-range check — the same inline predicate shape used by `ClosureParentRecipientResolver` and `PlannedDurationCalculator`), and include photo URLs only if `Contract.Consent.PhotosInternal == true`. If no active contract exists, or the flag is false, the activity's title/description are still returned but `photos: []`.

**Rationale**: This is the first feature to actually gate something on `ContractConsent` (confirmed — every existing usage is read/write passthrough only). The active-contract lookup itself has no shared helper anywhere in the codebase; every consumer duplicates the predicate. Rather than inventing a premature shared `IActiveContractResolver` abstraction with only one real caller so far, this feature follows the existing duplication convention (consistent with the codebase's actual practice, not a new abstraction unproven by a second use case).

**Alternatives considered**: Extracting a shared `IActiveContractResolver` now — rejected as premature abstraction (violates the project's "no half-finished implementations / don't design for hypothetical future requirements" guidance); if a third consumer appears, that is the right time to extract it, not now.

## R7 — Offline: activity metadata via the existing queue, photos via a new binary upload queue

**Decision**: Activity creation (type/title/description/`occurredAt`) registers `'group_activity'` as a new `entity_type` in `mobile/services/syncEngine.ts` (`registerSyncHandler`), following the exact pattern `mobile/services/childEvents.ts` and `attendance.ts` already use — enqueued via `offlineQueue.ts`'s existing `enqueue()`, synced on reconnect.

Photos are **not** put through that same queue. `offlineQueue.ts`'s `offline_queue` table stores JSON-serialized text payloads (confirmed: no binary/blob support), and a 10-photo, up-to-10MB-each activity would be a poor fit for that mechanism even if it technically fit as base64. Instead, a new local table (`photo_upload_queue`: `id`, `activity_id` [client-generated, may still be pending sync itself], `local_uri`, `caption`, `uploaded_at`) tracks queued photos by local file URI (the picked/captured image stays on-device until upload succeeds), with its own uploader routine that runs alongside the existing sync engine — triggered on reconnect/foreground the same way, but uploading via `POST /api/group-activities/{id}/photos` (R3) rather than replaying a generic queued HTTP request. A photo whose parent activity hasn't synced yet waits for the activity's server id before it can upload (since the endpoint is keyed by server activity id).

**Rationale**: Confirmed via research that **no client-side photo upload exists anywhere in this codebase today** (backend upload-URL endpoints for staff/child profile photos are wired but never called by any client code) — this is genuinely new client infrastructure, not a reuse of an existing flow. Keeping it as a separate, purpose-built queue (rather than forcing binary content through the generic JSON queue) matches spec.md's own framing: "Photos are queued separately — upload resumes on reconnect" (FR-012) already anticipates two independent mechanisms.

**Alternatives considered**: Base64-encode photo bytes into the existing JSON `offline_queue` payload — rejected: bloats SQLite row size dramatically (a 10MB photo becomes ~13MB of base64 text), and the existing queue's replay-in-`created_at`-order semantics (built for small JSON command payloads) isn't designed for large binary transfer with resumability.

## R8 — Endpoint auth: mirrors `ChildEventEndpoints.cs` exactly

**Decision**: Caregiver-tablet write endpoints (create activity, add photo) go under a `DeviceAuthenticated` + `DeviceTokenRotationFilter` group, identical in shape to `ChildEventEndpoints.cs`'s `deviceGroup`. The director-web delete endpoint and the director-web group-timeline read endpoint go under `DirectorOnly` (no rotation filter — that filter is device-token-specific). The parent-facing read (daily feed extension, gallery) goes under the existing `ParentOnly` policy, consistent with `ParentEndpoints.cs`.

**Rationale**: Confirmed `DeviceTokenRotationFilter` is applied uniformly to every device-authenticated group in the codebase (child-events, attendance, room-shifts) — not a per-feature special case. Following it exactly avoids introducing a fourth, subtly different device-auth idiom.

## R9 — Testing approach

**Decision**: Backend integration tests under `backend/ChildCare.Api.Tests/GroupActivities/`, one file per concern (creation/recorded-by resolution, photo upload+resize, consent filtering, group-timeline merge ordering, director delete), using the existing `OrganisationOnboardingWebAppFactory` TestContainers-Postgres fixture (constitution Principle V — no InMemory provider). Mobile tests under `mobile/__tests__/services/groupActivities.test.ts` and a component test for the new creation form/timeline entry. Web tests as a new flat `web/__tests__/groups.test.tsx`, following the existing one-file-per-screen convention.

**Rationale**: Matches every prior feature's established test layout and infrastructure exactly — no new test infrastructure needed.
