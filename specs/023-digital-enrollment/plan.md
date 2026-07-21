# Implementation Plan: Digital Online Enrollment

**Branch**: `023-digital-enrollment` | **Date**: 2026-07-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/023-digital-enrollment/spec.md`

## Summary

Add a per-location, director-controlled, default-disabled public enrollment form (a new
unauthenticated Next.js route) that creates a `WaitingListEntry` (feature 012a) marked
self-registered, with anti-spam protection (honeypot + IP rate limiting), a reference code and
confirmation email to the parent, and an in-app notification to the tenant's directors. Director
conversion (012a's existing `offered`/`enrolled` transition) pre-fills the child/contact
creation flows from the entry. Directors can also send a tour invitation (proposed date/time +
signed accept/decline link) and record its outcome manually.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / Next.js (App Router) for `web/`
(director-web, and the new public enrollment page lives in this same app).

**Primary Dependencies**: ASP.NET Core Minimal APIs, MediatR, FluentValidation, EF Core 9,
PostgreSQL 16 (backend, all existing); Next.js/Tailwind (web, existing) — no new external
package required on either side; the public form and tour-response page reuse this codebase's
existing unauthenticated-route and signed-token patterns (see research.md).

**Storage**: PostgreSQL — extends existing `Location` and `WaitingListEntry` tables (no new
tables; the tour invitation is modeled as fields on the existing entry, per spec.md's
Assumptions/research.md R2).

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, Constitution Principle V);
existing Jest/RTL suite for `web/`.

**Target Platform**: a new public, unauthenticated web route (`web/app/enroll/...`) plus
director-web (settings toggle, waiting-list UI). No caregiver-tablet or parent-mobile change.

**Project Type**: Mixed — backend API extension (public + director-facing) and one new
public Next.js route group plus additions to the existing director-web waiting-list UI, per
spec.md's Product Context.

**Performance Goals**: no special target beyond the existing API baseline — this is low-volume,
prospective-family-scale traffic (spec.md's Technical Requirements).

**Constraints**: public enrollment defaults to disabled per location (SC-002); rate limit 3
submissions/IP/rolling hour (FR-006, SC-004); no child/contact data written to authoritative
tables until director conversion (FR-020, unchanged from 012a); all new strings in NL/FR/EN
(FR-019).

**Scale/Scope**: two extended entities (`Location`, `WaitingListEntry`, no new tables), ~6 new
backend MediatR requests, one new unauthenticated endpoint group, one new public Next.js route,
additions to the existing director-web waiting-list page and location settings page.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|---|---|
| I. Multi-Tenant Isolation | Pass. The public submission and tour-response endpoints carry no JWT `tenant_id` claim (they're anonymous), so they're marked `.RequireTenantExempt()` and resolve their tenant schema explicitly from an `org` slug parameter before touching any data — the exact existing pattern feature 020's unsubscribe endpoints and `ResetPasswordCommandHandler` (003) already use (research.md R1). Every director-facing addition runs through the existing `TenantMiddleware`/`ITenantDbContext` path unchanged. |
| II. Regulatory Compliance by Design | N/A. No BKR, contract-overlap, or closure-notification logic is touched by this feature. |
| III. CQRS via MediatR & Thin Endpoints | Pass. All new writes (submission, tour invitation send/response, outcome recording, location setting toggle) are new MediatR commands; new endpoint files stay thin (map HTTP ↔ command/query only). |
| IV. Internationalization First | Pass. FR-019 requires all new strings (form, confirmation email, tour-invitation email, director labels) in NL/FR/EN via existing i18n mechanisms — tracked explicitly in tasks.md. |
| V. Test with Real Infrastructure | Pass. Backend tests run against TestContainers PostgreSQL per existing convention; no InMemory provider introduced. |
| VI. Secure Configuration & Storage | Pass. New `Location`/`WaitingListEntry` columns ship as an EF Core migration with a manually-run SQL script (no auto-apply in production), per this repo's convention. No secrets involved; the tour-invitation token's signing key is server-side configuration only, mirroring `IUnsubscribeTokenService`. |
| VII. Monolith-First Simplicity | Pass. No new project/service — new code lives in `ChildCare.Application/WaitingList` and `ChildCare.Application/Locations` alongside the existing 012a/008b/021 code, and the public page lives in the existing `web/` Next.js app as a new route group, not a separate app. |

No violations. Complexity Tracking section is empty.

## Project Structure

### Documentation (this feature)

```text
specs/023-digital-enrollment/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── enrollment-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/Location.cs                                     # + PublicEnrollmentEnabled, PublicEnrollmentSlug, DefaultEnrollmentLocale
│   ├── Entities/WaitingListEntry.cs                              # + Source, ReferenceCode, SubmittedLocale, tour fields
│   └── Enums/
│       ├── WaitingListEntrySource.cs                             # new — DirectorEntered/SelfRegistered
│       ├── TourInvitationStatus.cs                                # new — NotSent/Sent/Accepted/Declined
│       └── NotificationType.cs                                    # + EnrollmentSubmitted
├── ChildCare.Application/
│   ├── WaitingList/
│   │   ├── SubmitPublicEnrollmentCommand.cs                      # new — public, tenant-exempt
│   │   ├── SubmitPublicEnrollmentCommandHandler.cs                # new
│   │   ├── GetPublicEnrollmentLocationInfoQuery.cs                 # new — public, tenant-exempt
│   │   ├── SendTourInvitationCommand.cs                            # new — director
│   │   ├── RecordTourOutcomeCommand.cs                             # new — director
│   │   ├── RespondTourInvitationCommand.cs                        # new — public, tenant-exempt
│   │   ├── ITourInvitationTokenService.cs / TourInvitationTokenService.cs  # new — mirrors IUnsubscribeTokenService
│   │   └── EnrollmentNotificationService.cs                       # new — director in-app notification (first Notification recipient that is a director, not a parent/contact)
│   └── Locations/
│       ├── UpdateLocationPublicEnrollmentSettingCommand.cs         # new — mirrors UpdateLocationQrCheckInSettingCommand
│       └── UpdateLocationPublicEnrollmentSettingCommandHandler.cs  # new
├── ChildCare.Contracts/
│   ├── Requests/WaitingListRequests.cs                            # + public submission/tour requests
│   ├── Requests/LocationRequests.cs                                # + public-enrollment setting request
│   └── Responses/WaitingListResponses.cs                           # + public submission/location-info responses; + Source/ReferenceCode/tour fields on existing entry response
├── ChildCare.Api/
│   ├── Endpoints/
│   │   ├── PublicEnrollmentEndpoints.cs                            # new — /api/public/enrollment/*, AllowAnonymous + RequireTenantExempt
│   │   ├── WaitingListEndpoints.cs                                 # + tour-invitation send/outcome routes
│   │   └── LocationEndpoints.cs                                    # + public-enrollment-setting route
│   ├── Services/EmailService.cs                                    # + SendEnrollmentConfirmationAsync, SendTourInvitationAsync
│   └── Program.cs                                                  # + "public-enrollment" rate-limit policy (3/IP/hour)
└── ChildCare.Infrastructure/
    ├── Persistence/Migrations/Tenant/<timestamp>_AddDigitalEnrollment.cs   # new
    └── Email/Templates/{enrollment-confirmation,tour-invitation}.scriban   # new — one template per type (locale-agnostic; a C# LabelsProvider.For(locale) supplies localized strings into the model), mirrors daily-report.scriban/DailyReportEmailLabels

web/
├── app/
│   ├── enroll/[orgSlug]/[locationSlug]/page.tsx                   # new — public, unauthenticated form (outside (app)/(auth) groups)
│   └── (app)/
│       ├── waiting-list/page.tsx                                  # + self-registered tag, duplicate flag, tour-invite action, outcome entry
│       └── locations/[id]/page.tsx                                 # + public-enrollment toggle section
├── lib/publicApiClient.ts                                          # new — unauthenticated fetch wrapper for the enroll route (existing apiClient assumes a session)
└── i18n/locales/{en,nl,fr}.json                                    # + new keys (form, confirmation, director labels)
```

**Structure Decision**: Follows the existing monolith-first layout (Constitution VII) — new
backend code extends `ChildCare.Application`'s `WaitingList`/`Locations` folders rather than a
new project; the public form is a new top-level route group in the existing `web/` Next.js app
(outside the `(app)`/`(auth)` groups, which are the only route groups an auth-gating layout
wraps — see research.md R1), not a new app or a separate marketing site.

## Complexity Tracking

*No Constitution Check violations — table intentionally empty.*
