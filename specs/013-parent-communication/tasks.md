# Tasks: Parent Communication

**Input**: Design documents from `specs/013-parent-communication/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api.md, quickstart.md

**Tests**: Required by constitution Principle V (happy path + key negative/security flows) and spec.md's Success Criteria (SC-003, SC-007 explicitly require automated coverage).

**Organization**: Tasks are grouped by user story (US0–US5, per spec.md) to enable independent implementation and testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Contracts, i18n scaffolding, route registration, and the new `parent-mobile` Expo project skeleton shared across all stories.

- [ ] T001 [P] Add `ParentInvitationRequests`/`ParentInvitationResponses` DTOs in `backend/ChildCare.Contracts/Requests/ParentInvitationRequests.cs` and `backend/ChildCare.Contracts/Responses/ParentInvitationResponses.cs`
- [ ] T002 [P] Add `MessageThreadRequests`/`MessageThreadResponses` DTOs in `backend/ChildCare.Contracts/Requests/MessageThreadRequests.cs` and `backend/ChildCare.Contracts/Responses/MessageThreadResponses.cs`
- [ ] T003 [P] Add `AnnouncementRequests`/`AnnouncementResponses` DTOs in `backend/ChildCare.Contracts/Requests/AnnouncementRequests.cs` and `backend/ChildCare.Contracts/Responses/AnnouncementResponses.cs`
- [ ] T004 [P] Add `NotificationResponses`/`ParentResponses` (daily summary, children list, push-token) DTOs in `backend/ChildCare.Contracts/Responses/NotificationResponses.cs` and `backend/ChildCare.Contracts/Responses/ParentResponses.cs`
- [ ] T005 [P] Add `parentCommunication.*` i18n keys (invite action, invitation errors, thread/message UI, announcement UI, notification centre, push errors) to `web/i18n/locales/en.json`, `fr.json`, `nl.json`
- [ ] T006 Register director web "Messages" and "Announcements" nav entries in `web/components/Sidebar.tsx`
- [ ] T007 Register `MapParentInvitationEndpoints()`, `MapMessageThreadEndpoints()`, `MapAnnouncementEndpoints()`, `MapNotificationEndpoints()`, `MapParentEndpoints()` in `backend/ChildCare.Api/Program.cs`
- [ ] T008 Scaffold the `parent-mobile/` Expo project (package.json as `childcare-parent`, `app.config.js` with `orientation: "portrait"` and its own bundle id `com.dgit.childcareparent`, TypeScript, NativeWind/Tailwind config) mirroring `mobile/`'s feature-008 scaffold shape
- [ ] T009 [P] Copy `mobile/theme/colors.js` to `parent-mobile/theme/colors.js` (design-decisions.md's established per-platform-copy convention)
- [ ] T010 [P] Wire i18n in `parent-mobile/` (`expo-localization` + `react-i18next`, NL/FR/EN locale files) mirroring `mobile/i18n/`
- [ ] T011 [P] Generate the openapi-fetch client into `parent-mobile/services/generated/api-types.ts` (openapi-typescript against the local backend, committed per the existing `mobile/`/`web/` precedent)
- [ ] T012 Add `lucide-react-native` dependency to `parent-mobile/` (design-system.md's icon set)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core entities, persistence, shared result types, and the parent-mobile app shell (auth infra, navigation, API client) that every user story depends on.

**CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T013 [P] Add `TenantUserId` (nullable) to `Contact` in `backend/ChildCare.Domain/Entities/Contact.cs`
- [ ] T014 [P] Create `ParentInvitation` entity in `backend/ChildCare.Domain/Entities/ParentInvitation.cs` (structural copy of `StaffInvitation`, per research.md R1)
- [ ] T015 [P] Create `MessageThread`, `MessageThreadParticipant`, `Message` entities in `backend/ChildCare.Domain/Entities/MessageThread.cs`, `MessageThreadParticipant.cs`, `Message.cs`
- [ ] T016 [P] Create `Announcement`, `AnnouncementRecipient` entities in `backend/ChildCare.Domain/Entities/Announcement.cs`, `AnnouncementRecipient.cs`
- [ ] T017 [P] Create `NotificationType` enum and `Notification` entity in `backend/ChildCare.Domain/Enums/NotificationType.cs` and `backend/ChildCare.Domain/Entities/Notification.cs`
- [ ] T018 Add all new DbSets, composite-key configuration (`MessageThreadParticipant`), and indexes (`Notification(TenantUserId, CreatedAt)`, `Message(ThreadId, SentAt)`) to `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`
- [ ] T019 Add tenant migration for `contacts.tenant_user_id`, `parent_invitations`, `message_threads`, `message_thread_participants`, `messages`, `announcements`, `announcement_recipients`, `notifications` in `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/`
- [ ] T020 Extend `TenantMigrationRolloutTests`' schema-revert helper for the new tables' FKs (to `contacts`, `children`, `locations`, `groups`) in `backend/ChildCare.Api.Tests/` per every prior migration-adding feature's standing requirement
- [ ] T021 Add all new DbSets to `backend/ChildCare.Application/Common/ITenantDbContext.cs`
- [ ] T022 [P] Create `ParentInvitationResult`/`ParentInvitationFailure`, `MessagingResult`/`MessagingFailure`, `AnnouncementResult`/`AnnouncementFailure` result types + response mappers in `backend/ChildCare.Application/ParentInvitations/ParentInvitationResult.cs`, `backend/ChildCare.Application/Messaging/MessagingResult.cs`, `backend/ChildCare.Application/Announcements/AnnouncementResult.cs`
- [ ] T023 Create `ICurrentParentContactResolver` (resolves the authenticated `ParentOnly` caller's `Contact` via `TenantUserId`, 403s if none linked) in `backend/ChildCare.Application/Common/ICurrentParentContactResolver.cs` and its implementation — the shared authorization primitive every `ParentOnly` handler in US1–US5 uses (FR-006, FR-017)
- [ ] T024 Create empty `ParentInvitationEndpoints`, `MessageThreadEndpoints`, `AnnouncementEndpoints`, `NotificationEndpoints`, `ParentEndpoints` route-group files (policies per contracts/api.md) in `backend/ChildCare.Api/Endpoints/`
- [ ] T025 Scaffold `parent-mobile/` navigation: `app/(auth)/_layout.tsx` (login + accept-invitation), `app/(app)/_layout.tsx` (tab bar: Home, Messages, Notifications), portrait-first per platform-rules.md
- [ ] T026 Implement `parent-mobile/` auth infra: SecureStore token storage, per-device refresh rotation, 401-intercept auto-refresh, logout — mirrors `mobile/`'s feature-008 auth module, reusing the same backend auth endpoints (feature 003) under `ParentOnly`/`Parent` role
- [ ] T027 Implement `parent-mobile/` login screen (`app/(auth)/login.tsx`) — email/password + Google/Apple sign-in per constitution's parent-app auth stack

**Checkpoint**: Foundation ready — user story implementation can now begin.

---

## Phase 3: User Story 0 - Director invites a parent to the app (Priority: P1) 🎯 MVP prerequisite

**Goal**: A director can invite an eligible contact to the parent app, and that person can complete registration and log in.

**Independent Test**: Invite a `CanPickup=true` contact with an email; confirm the invitation is created; complete registration with the resulting token; confirm login succeeds; confirm a reused/expired token is rejected generically.

### Tests for User Story 0

- [ ] T028 [P] [US0] Integration test: director invites an eligible contact, invitation created with correct `ExpiresAt`/`TokenHash` in `backend/ChildCare.Api.Tests/ParentInvitations/ParentInvitationEndpointsTests.cs`
- [ ] T029 [P] [US0] Integration test: invite rejected (`errors.parent_invitation.not_eligible`) for a contact with no email or `CanPickup=false` in `backend/ChildCare.Api.Tests/ParentInvitations/ParentInvitationEndpointsTests.cs`
- [ ] T030 [P] [US0] Integration test: invite rejected (`errors.parent_invitation.already_has_account`) when `Contact.TenantUserId` already set in `backend/ChildCare.Api.Tests/ParentInvitations/ParentInvitationEndpointsTests.cs`
- [ ] T031 [P] [US0] Integration test: accept flow sets `TenantUser.PasswordHash` and `Contact.TenantUserId`, login succeeds afterward in `backend/ChildCare.Api.Tests/ParentInvitations/ParentInvitationEndpointsTests.cs`
- [ ] T032 [P] [US0] Integration test: expired token and already-used token both return generic `404 errors.invitation.not_found` (non-enumerable, feature 001 precedent) in `backend/ChildCare.Api.Tests/ParentInvitations/ParentInvitationEndpointsTests.cs`
- [ ] T033 [P] [US0] Integration test: accepting an invitation backfills the contact as a participant on every existing `MessageThread` for their linked children, with full prior history visible (FR-006a) in `backend/ChildCare.Api.Tests/ParentInvitations/ParentInvitationEndpointsTests.cs`
- [ ] T034 [P] [US0] Web component test: "Invite to parent app" action visible for an eligible contact, disabled for one with no email in `web/__tests__/parentInvitations.test.tsx`

### Implementation for User Story 0

- [ ] T035 [US0] Implement `CreateParentInvitationCommand`+Validator+Handler (creates `TenantUser(Role=Parent, PasswordHash="")` + `ParentInvitation`, mirrors `CreateInvitationCommandHandler`/staff pattern) in `backend/ChildCare.Application/ParentInvitations/CreateParentInvitationCommand.cs`
- [ ] T036 [US0] Implement `AcceptParentInvitationCommand`+Validator+Handler (anonymous/tenant-exempt, organisation slug in body, sets password + `Contact.TenantUserId`, backfills thread participants per FR-006a) in `backend/ChildCare.Application/ParentInvitations/AcceptParentInvitationCommand.cs`
- [ ] T037 [US0] Implement `IEmailSender.SendParentInvitationAsync` + `EmailService` implementation, mirroring `SendStaffInvitationAsync`'s English-only raw-HTML precedent, in `backend/ChildCare.Application/Common/IEmailSender.cs` / `backend/ChildCare.Api/Services/EmailService.cs`
- [ ] T038 [US0] Map `POST /api/parent-invitations` (DirectorOnly) and `POST /api/parent-invitations/accept` (anonymous, tenant-exempt) in `backend/ChildCare.Api/Endpoints/ParentInvitationEndpoints.cs`
- [ ] T039 [US0] Regenerate OpenAPI types for parent-invitation endpoints in `web/lib/generated/api-types.ts` and `parent-mobile/services/generated/api-types.ts`
- [ ] T040 [US0] Add "Invite to parent app" row action to the existing child Contacts UI in `web/app/(app)/children/[id]/` (enabled only when `CanPickup=true` and email present)
- [ ] T041 [US0] Implement `parent-mobile/app/(auth)/accept-invitation.tsx` (deep-link token capture, password form, calls accept endpoint, redirects to login)

**Checkpoint**: A director can provision a parent account end-to-end; every later story can now assume an authenticated parent exists.

---

## Phase 4: User Story 1 - Parent sees today's summary (Priority: P1)

**Goal**: A logged-in parent sees an aggregated, `visible_to_parent`-filtered daily summary for each of their children.

**Independent Test**: Seed mixed-visibility events for today; log in as the parent; confirm only visible events are aggregated and internal notes never appear, for both single- and two-child parents.

### Tests for User Story 1

- [ ] T042 [P] [US1] Integration test: daily summary aggregates visible events correctly (naps/bottles/diapers/mood/temperature/medication/activities) in `backend/ChildCare.Api.Tests/Parent/ParentDailySummaryTests.cs`
- [ ] T043 [P] [US1] Integration test: `visible_to_parent=false` events never appear in the response, regardless of event type in `backend/ChildCare.Api.Tests/Parent/ParentDailySummaryTests.cs`
- [ ] T044 [P] [US1] Integration test: a parent cannot fetch a summary for a child they are not a contact of — 403 in `backend/ChildCare.Api.Tests/Parent/ParentDailySummaryTests.cs`
- [ ] T044a [P] [US1] Integration test: a parent authenticated against tenant A cannot fetch a daily summary for a child that exists only in tenant B, even with a structurally valid child id (FR-018) in `backend/ChildCare.Api.Tests/Parent/ParentDailySummaryTests.cs`
- [ ] T045 [P] [US1] Integration test: `GET /api/parent/children` returns only the caller's own children in `backend/ChildCare.Api.Tests/Parent/ParentChildrenTests.cs`
- [ ] T046 [P] [US1] Mobile test: home screen renders two children's summaries clearly separated, and a "no updates yet" empty state when a child has zero events in `parent-mobile/__tests__/home.test.tsx`

### Implementation for User Story 1

- [ ] T047 [US1] Extend `GetDailySummaryQuery`/Handler with an `activities` field (research.md R5) in `backend/ChildCare.Application/ChildEvents/GetDailySummaryQuery.cs`
- [ ] T048 [US1] Implement `GetParentDailySummaryQuery`+Handler (authorizes via `ICurrentParentContactResolver` + `ChildContact`, delegates to `GetDailySummaryQuery`) in `backend/ChildCare.Application/Parent/GetParentDailySummaryQuery.cs`
- [ ] T049 [P] [US1] Implement `GetParentChildrenQuery`+Handler in `backend/ChildCare.Application/Parent/GetParentChildrenQuery.cs`
- [ ] T050 [US1] Map `GET /api/parent/children/{childId}/daily-summary` and `GET /api/parent/children` (ParentOnly) in `backend/ChildCare.Api/Endpoints/ParentEndpoints.cs`
- [ ] T051 [US1] Regenerate OpenAPI types for parent endpoints in `parent-mobile/services/generated/api-types.ts`
- [ ] T052 [P] [US1] Implement `DailySummaryCard` component (per-child, icon-paired sections, "no updates yet" empty state per design-system.md) in `parent-mobile/components/DailySummaryCard.tsx`
- [ ] T053 [US1] Implement `parent-mobile/app/(app)/index.tsx` home screen (fetches children + summaries, renders one card per child, pull-to-refresh)

**Checkpoint**: A parent can log in and immediately see their child(ren)'s day — the feature's core reassurance value is now live.

---

## Phase 5: User Story 2 - Parent messages the KDV and receives a reply (Priority: P1)

**Goal**: A parent can start a thread and message the KDV; a director/staff member can reply; both parents of a child share one thread.

**Independent Test**: Parent starts a thread and sends a message; director replies from web admin; parent sees the reply; a second invited parent for the same child sees the same thread without sending anything themselves.

### Tests for User Story 2

- [ ] T054 [P] [US2] Integration test: parent creates a child-scoped thread, message visible to both the parent and every eligible participant in `backend/ChildCare.Api.Tests/Messaging/MessageThreadEndpointsTests.cs`
- [ ] T055 [P] [US2] Integration test: two parent accounts for the same child both land as participants on one shared thread — not two separate threads (FR-003a) in `backend/ChildCare.Api.Tests/Messaging/MessageThreadEndpointsTests.cs`
- [ ] T056 [P] [US2] Integration test: director/staff reply appears in the same thread, in chronological order, visible to all participants in `backend/ChildCare.Api.Tests/Messaging/MessageThreadEndpointsTests.cs`
- [ ] T057 [P] [US2] Integration test: a parent who is not a thread participant is denied access regardless of request shape (FR-006) in `backend/ChildCare.Api.Tests/Messaging/MessageThreadEndpointsTests.cs`
- [ ] T058 [P] [US2] Integration test: a general (non-child-specific) thread can be created and is accessible to the creating parent only in `backend/ChildCare.Api.Tests/Messaging/MessageThreadEndpointsTests.cs`
- [ ] T058a [P] [US2] Integration test: a general (non-child-specific) thread is visible and replyable by any director/staff member org-wide, the same as a child-scoped thread (FR-004) in `backend/ChildCare.Api.Tests/Messaging/MessageThreadEndpointsTests.cs`
- [ ] T059 [P] [US2] Integration test: thread list ordered by most-recently-active first, with an unread indicator in `backend/ChildCare.Api.Tests/Messaging/MessageThreadEndpointsTests.cs`
- [ ] T059a [P] [US2] Integration test: a director/staff caller from tenant A cannot view or reply to a thread belonging to tenant B, even with a structurally valid thread id (FR-018) in `backend/ChildCare.Api.Tests/Messaging/MessageThreadEndpointsTests.cs`
- [ ] T060 [P] [US2] Web component test: director thread list and reply flow in `web/__tests__/messages.test.tsx`
- [ ] T061 [P] [US2] Mobile test: parent thread list, thread detail, and compose flow in `parent-mobile/__tests__/messages.test.tsx`

### Implementation for User Story 2

- [ ] T062 [US2] Implement `CreateMessageThreadCommand`+Validator+Handler (resolves and adds every eligible parent contact of the child as participant, per FR-003a) in `backend/ChildCare.Application/Messaging/CreateMessageThreadCommand.cs`
- [ ] T063 [US2] Implement `SendMessageCommand`+Validator+Handler (shared by parent and staff/director callers; sets `MessageThread.LastActivityAt`; sets cross-side `ReadAt` per research.md R7) in `backend/ChildCare.Application/Messaging/SendMessageCommand.cs`
- [ ] T064 [P] [US2] Implement `ListParentThreadsQuery`, `GetThreadQuery` (parent-scoped, authorized via `ICurrentParentContactResolver`) in `backend/ChildCare.Application/Messaging/ListParentThreadsQuery.cs` and `GetThreadQuery.cs`
- [ ] T065 [P] [US2] Implement `ListOrgThreadsQuery` (director/staff-scoped, includes unread-from-parent count per FR-013) in `backend/ChildCare.Application/Messaging/ListOrgThreadsQuery.cs`
- [ ] T066 [US2] Wire `SendMessageCommand` to create a `Notification(Type=NewMessage)` for every other participant (parent recipients only — staff/director awareness is the unread-count query, research.md R4/R7) and dispatch a push via `IExpoPushSender` (research.md R3)
- [ ] T067 [US2] Map parent routes (`POST/GET /api/parent/message-threads`, `GET /api/parent/message-threads/{id}`, `POST /api/parent/message-threads/{id}/messages`) and director/staff routes (`GET /api/message-threads`, `GET /api/message-threads/{id}`, `POST /api/message-threads/{id}/messages`) in `backend/ChildCare.Api/Endpoints/MessageThreadEndpoints.cs`
- [ ] T068 [US2] Regenerate OpenAPI types in `web/lib/generated/api-types.ts` and `parent-mobile/services/generated/api-types.ts`
- [ ] T069 [P] [US2] Implement `web/app/(app)/messages/page.tsx` (thread list, high-density table per platform-rules.md) and `web/app/(app)/messages/[id]/page.tsx` (thread detail + reply)
- [ ] T070 [P] [US2] Implement `parent-mobile/app/(app)/messages/index.tsx` (thread list) and `parent-mobile/app/(app)/messages/[id].tsx` (thread detail + compose), warm/timeline styling per platform-rules.md Parent Mobile section
- [ ] T071 [US2] Implement `parent-mobile/app/(app)/messages/new.tsx` (start a new thread, optional child selector)

**Checkpoint**: Two-way messaging is fully functional — the feature's other core value is now live.

---

## Phase 6: User Story 3 - Director sends an announcement (Priority: P2)

**Goal**: A director can broadcast a read-only announcement to every eligible parent contact within a location or group scope.

**Independent Test**: Send a location-scoped announcement; confirm every eligible contact receives it; confirm no reply affordance exists; confirm a zero-recipient scope completes without error.

### Tests for User Story 3

- [ ] T072 [P] [US3] Integration test: location-scoped announcement reaches every eligible contact of currently-enrolled children at that location in `backend/ChildCare.Api.Tests/Announcements/AnnouncementEndpointsTests.cs`
- [ ] T072a [P] [US3] Integration test: a currently-enrolled child's contact who has NOT completed a parent-app invitation (no `Contact.TenantUserId`) is excluded from announcement recipients, even though they are otherwise in scope (FR-008) in `backend/ChildCare.Api.Tests/Announcements/AnnouncementEndpointsTests.cs`
- [ ] T073 [P] [US3] Integration test: group-scoped announcement reaches only contacts of children in that group in `backend/ChildCare.Api.Tests/Announcements/AnnouncementEndpointsTests.cs`
- [ ] T074 [P] [US3] Integration test: a scope with zero currently-enrolled children completes with zero recipients, not an error in `backend/ChildCare.Api.Tests/Announcements/AnnouncementEndpointsTests.cs`
- [ ] T074a [P] [US3] Integration test: sending an announcement dispatches a push (via `IExpoPushSender` test double) to every recipient with a valid `PushToken`, satisfying FR-012's announcement-push requirement in `backend/ChildCare.Api.Tests/Announcements/AnnouncementEndpointsTests.cs`
- [ ] T075 [P] [US3] Integration test: no endpoint allows a parent to reply to an announcement in `backend/ChildCare.Api.Tests/Announcements/AnnouncementEndpointsTests.cs`
- [ ] T076 [P] [US3] Integration test: a parent can only view an announcement they are a recipient of — 403/404 otherwise in `backend/ChildCare.Api.Tests/Announcements/AnnouncementEndpointsTests.cs`
- [ ] T076a [P] [US3] Integration test: a director from tenant A cannot target a location/group belonging to tenant B when composing an announcement, and a parent from tenant A cannot view an announcement belonging to tenant B (FR-018) in `backend/ChildCare.Api.Tests/Announcements/AnnouncementEndpointsTests.cs`
- [ ] T077 [P] [US3] Web component test: announcement compose flow (location/group scope picker, subject/body) in `web/__tests__/announcements.test.tsx`

### Implementation for User Story 3

- [ ] T078 [US3] Implement `SendAnnouncementCommand`+Validator+Handler (resolves recipients = eligible contacts in scope per research.md R8, creates `AnnouncementRecipient` + `Notification(Type=Announcement)` rows, dispatches pushes) in `backend/ChildCare.Application/Announcements/SendAnnouncementCommand.cs`
- [ ] T079 [P] [US3] Implement `ListAnnouncementsQuery` (director sent history) and `GetParentAnnouncementQuery` (parent read, marks `AnnouncementRecipient.ReadAt`) in `backend/ChildCare.Application/Announcements/ListAnnouncementsQuery.cs` and `GetParentAnnouncementQuery.cs`
- [ ] T080 [US3] Map `POST/GET /api/announcements` (DirectorOnly) and `GET /api/parent/announcements/{id}` (ParentOnly) in `backend/ChildCare.Api/Endpoints/AnnouncementEndpoints.cs`
- [ ] T081 [US3] Regenerate OpenAPI types in `web/lib/generated/api-types.ts` and `parent-mobile/services/generated/api-types.ts`
- [ ] T082 [US3] Implement `web/app/(app)/announcements/page.tsx` (compose form with location/group scope picker, sent history list)
- [ ] T083 [US3] Implement `parent-mobile/app/(app)/announcements/[id].tsx` (read-only announcement view, no reply UI)

**Checkpoint**: Directors can broadcast to families without messaging each one individually.

---

## Phase 7: User Story 4 - Parent sees all notifications in one place (Priority: P2)

**Goal**: A parent has a single notification centre listing new messages, announcements, and temperature alerts, most-recent-first, each navigable and independently mark-as-read.

**Independent Test**: Trigger one of each notification type for the same parent; confirm all three appear correctly typed and ordered; confirm marking one read doesn't affect the others.

### Tests for User Story 4

- [ ] T084 [P] [US4] Integration test: notification centre lists message, announcement, and temperature-alert notifications, most-recent-first, each correctly typed in `backend/ChildCare.Api.Tests/Notifications/NotificationEndpointsTests.cs`
- [ ] T085 [P] [US4] Integration test: marking one notification read does not affect another's read state in `backend/ChildCare.Api.Tests/Notifications/NotificationEndpointsTests.cs`
- [ ] T086 [P] [US4] Integration test: a parent cannot mark or view another parent's notification — 403/404 in `backend/ChildCare.Api.Tests/Notifications/NotificationEndpointsTests.cs`
- [ ] T086a [P] [US4] Integration test: a parent from tenant A cannot view or mark-read a notification belonging to a parent in tenant B, even with a structurally valid notification id (FR-018) in `backend/ChildCare.Api.Tests/Notifications/NotificationEndpointsTests.cs`
- [ ] T087 [P] [US4] Integration test: a temperature event over threshold creates a `Notification(Type=TemperatureAlert)` row (extends existing `TemperatureAlertServiceTests`) in `backend/ChildCare.Api.Tests/ChildEvents/TemperatureAlertServiceTests.cs`
- [ ] T088 [P] [US4] Mobile test: notification centre renders all three types and mark-read isolation in `parent-mobile/__tests__/notifications.test.tsx`

### Implementation for User Story 4

- [ ] T089 [US4] Implement `ListNotificationsQuery`+Handler and `MarkNotificationReadCommand`+Validator+Handler in `backend/ChildCare.Application/Notifications/ListNotificationsQuery.cs` and `MarkNotificationReadCommand.cs`
- [ ] T090 [US4] Extend `TemperatureAlertService.NotifyAsync` to write a `Notification(Type=TemperatureAlert, SourceId=childEvent.Id)` row per recipient before/alongside the existing push send (research.md R4) in `backend/ChildCare.Application/ChildEvents/TemperatureAlertService.cs`
- [ ] T091 [US4] Map `GET /api/parent/notifications` and `POST /api/parent/notifications/{id}/read` (ParentOnly) in `backend/ChildCare.Api/Endpoints/NotificationEndpoints.cs`
- [ ] T092 [US4] Regenerate OpenAPI types in `parent-mobile/services/generated/api-types.ts`
- [ ] T093 [US4] Implement `parent-mobile/app/(app)/notifications/index.tsx` (typed list, icon-paired per type per design-system.md Status indicators, tap-to-navigate, mark-read on open)

**Checkpoint**: A parent has one reliable place to check everything needing attention, including the fallback path for missed pushes.

---

## Phase 8: User Story 5 - Parent and KDV both get timely push notifications (Priority: P3)

**Goal**: Push notifications reach parents for new messages/announcements; failures degrade gracefully; tokens replace on reinstall; staff see an unread indicator without polling.

**Independent Test**: Send a message to a parent with a valid token — push received. Repeat with an invalid token — failure logged, no crash, in-app notification still created.

### Tests for User Story 5

- [ ] T094 [P] [US5] Integration test: `PUT /api/parent/push-token` overwrites `Contact.PushToken`, replacing any prior value (FR-014) in `backend/ChildCare.Api.Tests/Parent/PushTokenTests.cs`
- [ ] T095 [P] [US5] Integration test: a new message to a parent with a valid token triggers `IExpoPushSender.SendAsync` (via a test double/spy) in `backend/ChildCare.Api.Tests/Messaging/MessageThreadEndpointsTests.cs`
- [ ] T096 [P] [US5] Integration test: a push send failure is logged, does not throw/block the triggering write, and the in-app `Notification` row still exists (FR-015) in `backend/ChildCare.Api.Tests/Messaging/MessageThreadEndpointsTests.cs`
- [ ] T097 [P] [US5] Integration test: `GET /api/message-threads` (director) surfaces an unread-from-parent indicator without requiring a separate poll endpoint (FR-013) in `backend/ChildCare.Api.Tests/Messaging/MessageThreadEndpointsTests.cs`

### Implementation for User Story 5

- [ ] T098 [US5] Implement `RegisterPushTokenCommand`+Validator+Handler (resolves caller's `Contact` via `ICurrentParentContactResolver`, overwrites `PushToken`) in `backend/ChildCare.Application/Parent/RegisterPushTokenCommand.cs`
- [ ] T099 [US5] Map `PUT /api/parent/push-token` (ParentOnly) in `backend/ChildCare.Api/Endpoints/ParentEndpoints.cs`
- [ ] T100 [US5] Add unread-from-parent count to `ListOrgThreadsQuery`'s response (if not already covered by T065 — verify and close any gap) in `backend/ChildCare.Application/Messaging/ListOrgThreadsQuery.cs`
- [ ] T101 [US5] Add an unread-count badge to the web `/messages` nav entry in `web/components/Sidebar.tsx` and thread list in `web/app/(app)/messages/page.tsx`
- [ ] T102 [US5] Implement `parent-mobile/` push-token registration on login/launch using `expo-notifications`, calling `PUT /api/parent/push-token` in `parent-mobile/services/pushToken.ts`, wired into the auth flow from T026

**Checkpoint**: The feature is timely, not just functional — all five user stories are independently verifiable end-to-end.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Final consistency pass across all stories.

- [ ] T103 [P] Verify NL/FR/EN completeness for every new i18n key added in T005/T010 (no fallback-to-English gaps) across `web/i18n/locales/` and `parent-mobile/i18n/locales/`
- [ ] T104 [P] Final audit: confirm every parent-facing query (daily summary, threads, notifications, announcements) has the explicit cross-tenant and cross-family negative test coverage added in T044a/T057/T059a/T076/T076a/T086/T086a (SC-003, SC-007) — add any remaining gap found
- [ ] T105 Run `quickstart.md`'s five scenarios end-to-end against a local dev stack; fix any gap found
- [ ] T106 [P] Design-compliance pass on all new `parent-mobile/` and `web/(app)/messages`, `web/(app)/announcements` screens against design-system.md/platform-rules.md (spacing scale, no nested cards, icon-paired status badges, 48pt touch targets)

---

## Dependencies & Execution Order

- **Phase 1 (Setup)** → **Phase 2 (Foundational)**: strictly sequential; Phase 2 blocks every user story.
- **User Story 0 (Phase 3)** blocks every other user story — no parent account exists until it ships.
- **User Story 1 (Phase 4)** and **User Story 2 (Phase 5)** are both P1 and independent of each other once US0 is done — may be built in either order or in parallel by different developers.
- **User Story 3 (Phase 6)** depends on the `Notification` infrastructure from Phase 2 but not on US1/US2's implementation — independent once Foundational is done.
- **User Story 4 (Phase 7)** depends on US2 and US3 having something to notify about (messages, announcements) and on the `Notification` table from Phase 2 — build after US2/US3.
- **User Story 5 (Phase 8)** depends on US2 (message push) and US3 (announcement push already built in T078) — primarily wires push-token registration and the staff unread indicator; build last among the stories.
- **Phase 9 (Polish)** after all stories.

## Parallel Execution Examples

- Within Phase 1: T001–T006 and T009–T012 touch disjoint files — run in parallel; T007/T008 are sequential prerequisites for later route/mobile work.
- Within Phase 2: T013–T017 (entity files) are parallel; T018–T021 are sequential (same files, dependency order); T022–T024 are parallel; T025–T027 (mobile scaffold) parallel with backend Foundational work.
- Within each user story's Tests subsection: all `[P]`-marked test tasks touch independent test files/scenarios and can run in parallel; implementation tasks mostly share files within a story and are sequential unless marked `[P]`.

## Implementation Strategy

**MVP scope**: User Story 0 (Phase 3) + User Story 1 (Phase 4) + User Story 2 (Phase 5) — a parent can be invited, log in, see their child's day, and message the KDV. This is the minimum that delivers the feature's stated core value ("what happened with my child today?" + "I need to tell the KDV something"). User Stories 3–5 (announcements, notification centre, push) are valuable but independently deferrable increments per their own priority ordering (P2, P2, P3).

**Incremental delivery**: Phase 1 → Phase 2 → Phase 3 (US0) → Phase 4 (US1) → Phase 5 (US2) → ship/demo MVP → Phase 6 (US3) → Phase 7 (US4) → Phase 8 (US5) → Phase 9 (Polish).
