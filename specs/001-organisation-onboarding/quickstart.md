# Quickstart: Organisation Onboarding

Validates the feature end-to-end against a local Postgres instance. Mirrors the acceptance scenarios in [spec.md](spec.md).

## Prerequisites

- Docker running (for local Postgres — see PROJECT-BRIEF.md's "Local dev: Docker PostgreSQL").
- `backend/ChildCare.Api/appsettings.Development.json` configured with a working `ConnectionStrings:DefaultConnection` (see `.example.json` for the shape) and a `SuperAdmin:ApiKey` value for local testing.
- `dotnet` SDK matching `net10.0` (per the existing `ChildCare.Api.csproj`).

## Setup

```bash
cd backend
dotnet restore ChildCare.sln
dotnet ef database update --project ChildCare.Infrastructure --startup-project ChildCare.Api --context PublicDbContext
dotnet run --project ChildCare.Api
```

The public-schema migration creates the `tenants` and `invitations` tables. No tenant schema exists yet — those are created dynamically per registration (research.md R6).

## Scenario 1 — Happy path (spec.md User Story 1)

1. Create an invitation as the operator:

   ```bash
   curl -s -X POST http://localhost:5xxx/api/admin/invitations \
     -H "X-Superadmin-Key: $SUPERADMIN_API_KEY" \
     -H "Content-Type: application/json" \
     -d '{"email":"director@example.com"}'
   ```

   Expected: `201`, response includes a plaintext `token`. Copy it.

2. Register using that token:

   ```bash
   curl -s -X POST http://localhost:5xxx/api/organisations/register \
     -H "Content-Type: application/json" \
     -d '{
       "invitationToken": "<token from step 1>",
       "organisationName": "Kinderdagverblijf De Zonnebloem",
       "directorName": "Marie Peeters",
       "email": "director@example.com",
       "password": "correct-horse-battery"
     }'
   ```

   Expected: `201`, response includes `accessToken` and the created `organisation`/`director`.

3. Confirm the workspace is real: connect to Postgres and verify a schema named `tenant_<slug>` exists with a `users` table containing exactly one row (the director), and that `tenants.provisioning_status = 'ready'` for this organisation.

## Scenario 2 — Invite-only enforcement (spec.md User Story 2)

- Repeat step 2 above with the **same token again** → expect `422` (`errors.registration...`), no second schema created.
- Repeat step 2 with a token that was never issued → expect `404`/`422`, no schema created.
- Manually expire an invitation (`UPDATE invitations SET expires_at = now() - interval '1 day' WHERE ...`) and attempt registration → expect rejection, no schema created.
- Attempt registration with the right token but a different `email` than the invitation targeted → expect `422` with `errors.registration.email_mismatch`.

## Scenario 3 — Resilience (spec.md User Story 3)

Hard to trigger a genuine mid-provisioning failure via `curl` alone; validate via the integration test suite (`ChildCare.Api.Tests`) instead, which should include a test that:

- Injects a failure between schema creation and migration (e.g., a test-only hook or a deliberately-invalid migration in a test fixture) and asserts the `Tenant` row is left in `provisioning`/`failed` status, then asserts a second attempt with the **same invitation token** completes successfully without creating a duplicate `Tenant` row.
- Fires two concurrent registration requests with the same invitation token (e.g., `Task.WhenAll`) and asserts exactly one `Tenant` row exists afterward with `ProvisioningStatus = 'ready'`.

Per constitution Principle V, these MUST run against TestContainers-provisioned PostgreSQL, not EF Core's InMemory provider — schema-per-tenant behavior (`CREATE SCHEMA`, `search_path`) has no InMemory equivalent.

## Expected outcomes checklist (maps to spec.md Success Criteria)

- [ ] SC-002: invitation → `201` registration response with a working `accessToken`, no extra steps.
- [ ] SC-003: expired/used/invalid invitation → rejected, zero side effects.
- [ ] SC-004: `tenants.provisioning_status = 'ready'` and the tenant schema's `users` table has the director row, both true at the moment `201` is returned — not sometime later.
- [ ] SC-005: concurrent same-token requests → exactly one `Tenant` row.
- [ ] SC-006: registration succeeds with `dossiernummer`/KBO fields entirely absent from the request (they aren't even part of this request shape — see contracts/register-organisation.md).
- [ ] SC-007: mismatched email → rejected, zero side effects.
