# ChildCare — Project Brief

> Paste this document when running `/speckit-constitution` in Claude Code.
> It provides the full project context that all specs, plans, and tasks should inherit.

---

## What We Are Building

**ChildCare** is a Belgian childcare management SaaS targeting **kinderdagverblijven (KDVs)** — licensed daycare centres in Flanders. It replaces paper and legacy systems with a modern platform for directors, caregivers, and parents.

**Phase 1 target:** Private KDVs only (no income-based subsidy / IKT complexity yet).

---

## Three Products, One Backend

| Product | Tech | Target user | Form factor |
|---|---|---|---|
| Web admin | Next.js 14 (App Router), TypeScript, Tailwind, shadcn/ui | Director / management | Desktop browser |
| Caregiver app | Expo (React Native) | Caregiver on the floor | Tablet, landscape |
| Parent app | Expo (React Native) | Parent | Phone, portrait |

All three share a single **ASP.NET Core .NET 10 API**.

---

## Backend Stack

| Layer | Choice |
|---|---|
| Runtime | .NET 10, C# |
| API style | Minimal APIs (no Controllers) |
| ORM | Entity Framework Core 9 |
| Database | PostgreSQL 16 |
| Auth | ASP.NET Core Identity + JWT; per-device refresh token rotation |
| PDF | QuestPDF (MIT licence) |
| Push notifications | Expo Push Notification Service |
| Storage | GCP Cloud Storage, signed URLs |
| API docs | Scalar (dev only, `/scalar/v1`) |
| Client generation | openapi-typescript + openapi-fetch (TypeScript); no NSwag |

---

## Solution Structure

Five projects — no more, no less:

```
ChildCare.sln
├── ChildCare.Api           # Minimal API endpoints, DI, middleware, Program.cs
├── ChildCare.Application   # MediatR commands/queries, FluentValidation, DTOs
├── ChildCare.Domain        # Entities, value objects, domain events, no EF deps
├── ChildCare.Infrastructure# EF Core, repositories, external service clients
└── ChildCare.Contracts     # Shared request/response types, enums
```

**Naming rule:** All C# namespaces, projects, and the solution use `ChildCare` — never `Kdv`, `KdvPlatform`, or Dutch acronyms in code.

---

## Architecture Decisions (non-negotiable)

### Patterns
- **MediatR + CQRS:** All writes (commands) go through MediatR. Complex reads go through MediatR. Simple lookups may be direct.
- **FluentValidation** as a MediatR pipeline behaviour — validation fires before every command handler.
- **Endpoint handlers stay thin** — no business logic in `*Endpoints.cs` files. Logic lives in Application layer handlers.
- **Monolith first** — single deployable. No YARP, no microservices until there is a proven need.

### Multi-Tenancy
- **Tenant = Organisation** (the legal entity that owns one or more KDV locations).
- **Schema-per-tenant** in PostgreSQL. Every tenant gets their own schema (e.g., `tenant_abc123`).
- **PublicDbContext** — single shared schema; contains only the `tenants` table (slug, schema name, subscription status).
- **TenantDbContext** — all domain data; `search_path` is set to the tenant's schema on every connection.
- **TenantMiddleware** — resolves tenant from the JWT claim `tenant_id`, sets `ICurrentTenantService`, switches schema.
- **No pgBouncer / transaction-mode pooling** — use Neon direct (non-pooled) connections so `search_path` is not reset between statements.
- **One owner, multiple locations:** A single tenant schema contains a `locations` table. All KDVs under one owner are in the same tenant.

### Split-Location Enrolment
A child may hold **two simultaneous active contracts** at different locations owned by the same organisation, provided the contracted days do not overlap. A day-overlap validator must run on contract activation.

### Auth Strategy
| App | Auth methods |
|---|---|
| Parent app | Google OAuth + Apple Sign-In (App Store requirement) + email/password |
| Caregiver app | Room kiosk: director sets up tablet once (email/password); caregivers identify per shift via 4-digit PIN. No daily email/password on the floor. |
| Web admin | Email/password + Google OAuth |

**Caregiver tablet model:** One shared tablet per group/section, locked in kiosk mode after one-time director setup. Each caregiver has a 4-digit PIN managed in the web admin. The underlying mechanism is JWT + SecureStore — the PIN layer sits on top of a long-lived **device token** (30-day TTL) scoped to the room, present on every API call. This is the security boundary; it never goes away regardless of shift state.

On top of that sits a **shift register**: caregivers check in/out via PIN at the start and end of their presence in the room. Two caregivers are simultaneously checked in for most of the day; either can log any event at any time. `recorded_by` is derived server-side from the shift log at `occurred_at` — caregivers do not need to identify themselves before every tap. For medical events (medication, temperature) a PIN prompt names `administered_by` specifically; skipping is allowed and the director fills in retroactively.

This is the standard pattern used by Brightwheel, Procare, and Famly. Implemented in feature `008a-caregiver-kiosk-mode`, not feature 008.

### Database (by environment)
| Env | Database |
|---|---|
| Local dev | Docker PostgreSQL |
| CI | TestContainers (PostgreSQL) |
| Deployed (pre-revenue) | Neon free tier, Frankfurt (eu-central-1) |
| Deployed (post-revenue) | Cloud SQL db-f1-micro |

---

## Infrastructure

- **Cloud:** GCP, project `childcare-501020`, region `europe-west1`
- **Compute:** Cloud Run (containerised, scale-to-zero)
- **Registry:** Artifact Registry
- **IaC:** Terraform (`infra/gcp/`)
- **CI/CD:** GitHub Actions — push to `master` → build → push image → deploy to Cloud Run
- **Walking skeleton is live** — CI/CD pipeline deployed and auth tested end-to-end.

---

## i18n Rules

- **Languages:** Dutch (NL), French (FR), English (EN) — all three from day one.
- **No hardcoded user-facing strings anywhere** in the codebase.
- Web admin: `next-intl`
- Mobile apps: `expo-localization` + `react-i18next`
- API error messages and validation responses must use locale-aware keys, not raw strings.

---

## Domain Model — Key Concepts

### Belgian KDV Regulatory Context
- **KDV** (kinderdagverblijf) — licensed daycare centre, Flanders.
- **Erkenning** — operating licence issued by Opgroeien (Flemish agency).
- **BKR** (begeleider-kind-ratio) — legal caregiver-to-child ratios: solo caregiver max 8 children; 2+ caregivers max 9 per caregiver; nap time max 14; leefgroep (living group) max 18.
- **MeMoQ** — mandatory pedagogical quality self-evaluation (6 dimensions). Applies to all KDVs with erkenning. Phase 2 feature.
- **IKT** — income-based subsidy from Opgroeien. Phase 3 only. Adds significant complexity; excluded from Phase 1–2.
- **Closure calendar** — each KDV sets its own holiday/closure schedule (independent of school holidays). Types: `holiday`, `training`, `extraordinary`. Parents must be notified.

### Core Entities (Phase 1)
- **Organisation** — the tenant. Has many Locations.
- **Location** — a physical KDV building. Has a BKR config, closure calendar, and leefgroepen.
- **Child** — belongs to a family; has a profile, medical notes, authorised pickups, and a contract.
- **Contract** — links a child to a location with contracted days. Enforces split-location day-overlap rule.
- **Caregiver** — staff member, can be assigned to multiple locations.
- **Director** — user role with full admin rights for their organisation.
- **child_events** — single JSONB table tracking: `sleep`, `temperature`, `medication`, `feeding_bottle`, `feeding_solid`, `diaper`, `mood`, `activity`, `note`, `weight`, `measurement`. No separate tables per event type.

---

## What Already Exists in the Codebase

The repo at `ChildCare/` contains a **walking skeleton** to avoid building on nothing:

- `ChildCare.Api/Program.cs` — JWT auth, rate limiting (5 policies), CORS, Scalar UI, global exception handler, security headers, auto-migrate in dev.
- `ChildCare.Api/Endpoints/AuthEndpoints.cs` — email/password + Google + Apple sign-in, rate-limited per endpoint type.
- `ChildCare.Api/Services/AuthService.cs` — per-device refresh token rotation, Google tokeninfo validation, Apple JWKS validation.
- `ChildCare.Api/Data/AppDbContext.cs` — **to be replaced**: currently a single-schema DbContext with Users/Habits. Must become PublicDbContext + TenantDbContext.
- `ChildCare.Api/Endpoints/HabitEndpoints.cs` — **to be deleted**: the Habits domain is a leftover from the template. Remove entirely.
- `ChildCare.Api.Tests/AuthEndpointTests.cs` — WebApplicationFactory integration tests; currently uses InMemory EF. **Replace InMemory with TestContainers**.
- `infra/gcp/` — Terraform for Cloud Run, Artifact Registry, WIF, IAM. Working.
- `.github/workflows/deploy-gcp.yml` — CI/CD pipeline. Working.

---

## Phase Roadmap (summary)

| Phase | Scope |
|---|---|
| 1 | Core operations: child profiles, contracts, daily tracking (child_events), attendance, caregiver scheduling, parent communication, closure calendar, basic invoicing |
| 2 | MeMoQ quality tracking, fiscal attestations (tax certificates), developmental milestones, management reporting, email communications (bulk parent emails by location/section, emailed daily reports) |
| 3 | IKT subsidy integration (Opgroeien API), advanced financial admin |

---

## Non-Negotiables for Every Feature

1. Multi-tenancy respected — no cross-tenant data leakage possible.
2. BKR ratio enforced at business logic level, not just UI.
3. All user-facing text goes through i18n keys.
4. No business logic in endpoint handlers.
5. Every command goes through MediatR with FluentValidation.
6. Tests use TestContainers (real PostgreSQL), not InMemory.
7. No Dutch or French words in C# identifiers — English only.
8. QuestPDF for all PDF output — no other PDF library.
9. Signed GCP URLs for all file access — no public blob URLs.
10. Sensitive config (secrets, connection strings) never hardcoded — always via env vars or Secret Manager.
