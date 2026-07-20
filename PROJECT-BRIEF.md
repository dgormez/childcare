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

## Pre-Production Scaling Checklist (deferred — revisit before onboarding beyond ~1-2 clients)

Phase 1 targets a single client at $0-if-possible dev cost; cold starts and small DB tiers are
accepted deliberately for now. Revisit these — mostly config/tier choices, not code changes —
once real multi-tenant traffic (dozens of KDVs) is in sight:

- **DB tier**: `db-f1-micro` (or Neon free tier) will not hold up once concurrent load from many
  KDVs' drop-off/pickup rushes hits at once. Pick a properly sized Postgres tier deliberately
  before onboarding beyond a handful of clients.
- **Connection pooling**: no pgBouncer/pooler is used — Neon direct connections, one Npgsql pool
  per Cloud Run instance. Fine at low instance counts; re-examine once Cloud Run scales past a
  few instances, since pool-size × instances is currently unbounded. Note the original "no
  pgBouncer" rationale (search_path resets) may be stale — request-time code is schema-qualified
  via `HasDefaultSchema`, not `search_path`-dependent (only tenant provisioning sets
  `search_path`); the real constraint is more likely `PostgresAdvisoryLockService`'s session-level
  `pg_advisory_lock` calls, which transaction-mode pooling would break. Worth confirming properly
  before picking a pooler mode.
- **Cloud Run scaling**: `infra/gcp/main.tf` sets no `min_instance_count`, `max_instance_count`,
  `cpu`, or `memory` — all GCP defaults. Fine for 1 client; set these deliberately once instance
  count × DB connection limits actually matters. `min_instance_count` also controls cold starts,
  which are explicitly acceptable for now but not for production.
- **Push notification fan-out**: `SendAnnouncementCommandHandler` sends one sequential HTTP call
  to Expo per recipient inside the request. Fine for a single small KDV; will need batching (Expo
  supports up to 100 messages/call) or a background queue once a location/org has enough parents
  that this loop takes noticeably long.
- **`migrate-tenants` CLI**: migrates tenant schemas sequentially, one at a time. Fine at a handful
  of tenants; parallelize (with a concurrency cap) before tenant count grows into the hundreds.

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
- **BKR** (begeleider-kind-ratio) — legal caregiver-to-child ratios: solo caregiver max 8 children; 2+ caregivers max 9 per caregiver; nap time max 14; leefgroep (living group) max 18. **IMPORTANT: a lower kindratio becomes mandatory 1 January 2027** (verified from Opgroeien's kindratio special): baby-only leefgroep (≤12m) 1:5, only >12m 1:8, mixed leefgroep 1:7, rest moment max 14 (max 2h, 2+ caregivers actively supervising), max leefgroep size stays 18; ratio assessed at location level. Transition until 31/12/2026; early adoption allowed and subsidy-linked. BKR rules must therefore be **date-versioned config, not constants** — backlog feature 041.
- **MeMoQ** — mandatory pedagogical quality self-evaluation (6 dimensions). Applies to all KDVs with erkenning. Phase 2 feature.
- **IKT** — income-based subsidy from Opgroeien. Phase 3 only. Adds significant complexity; excluded from Phase 1–2.
- **Kinderopvangtoeslag (Groeipakket)** — parents using **free-price** childcare (i.e. the Phase 1 target segment) receive a fixed allowance per (half) day of presence via the Groeipakket. The organisator must report presences to Opgroeien **every month** — via the free AARON web app or via webservice (REST/JSON, bearer token). Backlog feature 033, Phase 2.
- **Aanwezigheidsregister** — legal attendance register: arrival/departure **times** per child, recorded **at the moment** (never in advance or afterwards), **confirmed by parents** in writing or electronically (frequency chosen by the organisator), kept **≥ 12 months** for Zorginspectie. Backlog feature 037.
- **Inlichtingenfiche** — mandatory, up-to-date information sheet per child (identity, parent + GP contacts, medical data, authorised pickups). Access legally restricted to: organisator, verantwoordelijke, the child's caregiver, Zorginspectie, Opgroeien. Covered by features 006/006a; the access restriction is an RBAC constraint on every child-data surface.
- **Huishoudelijk reglement & schriftelijke overeenkomst** — mandatory documents; parents sign the reglement for *kennisname* (acknowledgment, incl. on changes) and the overeenkomst for *akkoord*. Opgroeien publishes model documents.
- **Fiscaal attest 281.86** — annual tax certificate for childcare costs. The **mandatory federal model** must be used, and the attest data must be **filed digitally with FOD Financiën via Belcotax-on-web** (deadline: end of February following the income year). Parents also receive their copy. Features 015 (PDF + manual BOW entry) and 019 item 5 (automated submission).
- **Attest slaaphouding** — babies < 1 year sleep on their back; a signed parental attest (plus medical attest if medical) is required for any other position. Feature 035.
- **Risicoanalyse** — legally required risk analysis on 4 domains: injuries/accidents, crises/life-threatening situations, child disappearance, illness/contamination. Continuous process; unacceptable risks must be addressed immediately. Feature 035.
- **Verplichte melding** — legal duty to report to Opgroeien asap: every crisis and grensoverschrijdend gedrag, morality-related criminal investigations/convictions of anyone in regular contact with children, complaints about a crisis, continuity threats, important governance changes. Official meldingsformulier → klantenbeheerder; urgent cases via Opgroeipunt. Feature 036.
- **Bewaartermijnen** — legal retention: 10y complaints/crisis; 5y child/family data and staff data (staff clock starts at end of employment); 3y strafregister extract (destroy previous when a new one arrives); ≥12m attendance register. Feature 038.
- **Closure calendar** — each KDV sets its own holiday/closure schedule (independent of school holidays). Types: `holiday`, `training`, `extraordinary`. Parents must be notified.

### Opgroeien / Government Integration Surface (verified 2026-07)

Opgroeien states (email + website) that it offers **no general public API or developer support**. However, the Organisator → Software page publishes concrete integration contracts for specific flows:

| Flow | Tech | Auth | Backlog |
|---|---|---|---|
| IKT: attest matching + opvangprestaties | SOAP webservice (WSDL: test `tstarws.kindengezin.be`, prod `arws.kindengezin.be`), operations MatchKind / MatchKinderen / MatchKinderenResultaat / OpvangPrestatiesResultaat | WS-Security X.509 (Kind & Gezin certificate; one **vendor-level certificate** on the supplier's KBO may cover all client organisations) | 019 (Phase 3) |
| Kinderopvangtoeslag (Groeipakket) presences | REST/JSON webservice ("AARON" backend); test Swagger: `tstgpappr.kindengezin.be/swagger-ui.html` | Bearer token (test token on request) | 033 (Phase 2) |
| Aanwezigheidsregistratie per location/month (IKT/T2 locations) | FO-SU-05.xsd v2.3 — monthly aggregated counts per kindcode in buckets min3u/min5u/min11u/min11uFlex × (present / justified absent / unjustified absent); submitted as XML by email to ko.formulieren@opgroeien.be (webservice exchange announced) | n/a (email) / X.509 for webservice | 019 item 4 |
| Jaarregistraties | XML per form: FO-RE-14 (inclusieve opvang), FO-RE-15 (flex openingstijden), FO-RE-19 (voorrangsgroepen/IKT), FO-RE-28 (dringende plaatsen), FO-RE-29 (ruimere openingsmomenten), FO-RE-30 (kwetsbare gezinnen) — all subsidy-tied; medewerkers form = PDF only; Opgroeien supplies per-organisation pre-filled values as Excel | n/a (file submission) | 034 (Phase 2, may shift to Phase 3 — subsidy-tied) |
| Fiscaal attest 281.86 | Belcotax-on-web (FOD Financiën, federal — not Opgroeien) | see FOD technical docs | 015 / 019 item 5 |

Vendor onboarding contact for all Opgroeien flows: **software-ontwikkeling@opgroeien.be** (a.k.a. @kindengezin.be).

**Vendor certificate contract** (verified from the official contract .docx, V5.0): ChildCare signs a contract with Opgroeien to obtain one KIND&GEZIN certificate on its own KBO, acting for all client organisators. Binding conditions: data centre inside the EU, vendor + subcontractors under GDPR, a **verwerkersovereenkomst with every organisator** (KDV = verantwoordelijke, ChildCare = verwerker — Opgroeien publishes a template), full vendor liability for misuse, **Opgroeien audit right**, certificate revocable on breach. Signed form goes to **dpo@opgroeien.be**. Consequence: the verwerkersovereenkomst belongs in organisation onboarding, and the GCP `europe-west1` hosting + subprocessor chain must be documented audit-ready.

The official schema/contract files (FO-RE-14/15/19/28/29/30.xsd, FO-SU-05.xsd v2.3, the aanwezigheden beschrijving PDF, the certificate contract) were obtained on 2026-07-15 — commit them to the repo (e.g. `docs/integrations/opgroeien/`) as the reference contracts for features 019 and 034.

**Kinderopvangzoeker** (kinderopvangzoeker.be): official parent-facing directory of licensed locations incl. inspection reports. **No public API**; not an integration target.

### Core Entities (Phase 1)
- **Organisation** — the tenant. Has many Locations.
- **Location** — a physical KDV building. Has a BKR config, closure calendar, and leefgroepen.
- **Child** — belongs to a family; has a profile, medical notes, authorised pickups, and a contract.
- **Contract** — links a child to a location with contracted days. Enforces split-location day-overlap rule.
- **Caregiver** — staff member, can be assigned to multiple locations.
- **Director** — user role with full admin rights for their organisation.
- **child_events** — single JSONB table tracking: `sleep`, `temperature`, `medication`, `feeding_bottle`, `feeding_solid`, `diaper`, `mood`, `activity`, `note`, `weight`, `measurement`. No separate tables per event type.

---

## Competitive Landscape (Flanders, snapshot 2026-07)

- **D-Care (daycare-solutions.be)** — Flemish, built by a KDV operator. Model: touchscreen terminals on the floor (hardware can be included in the subscription), web-based parent app (day reports, development tracking, absences, reservation status, closure days, invoices with in-app payment via **POM**), automatic generation of contracts, invoices, fiscal attests and IKT documents. Pricing scales with capacity. Public site is marketing-thin — a demo is needed for a real feature-by-feature comparison.
- **Bitcare (bitcare.com)** — Dutch company (Delft) with a real Belgian module (crawl verified 2026-07-15): **attendance submission to Kind & Gezin, fiscal attests + BOW/Belcotax file for the FOD, subsidy admin, Flemish kindratio calculations** — it already covers a chunk of the 015/019 government-reporting scope. Broader feature set: digital enrollment → auto-contract → e-signature with SEPA mandate; waiting list + forward occupancy + BKR placement checks + doorstroom planning; parent request queue; integrated staff planning with clock-in/out, plus/min-hours, leave self-service, salary export (Humanwave); group app with digital diary, chat, offline access; invoicing with automatic reminders, betalingsbewijzen, Mollie payments, accounting couplings (Exact, AFAS, Twinfield, MS Dynamics, SnelStart); dashboards incl. a "kwaliteitsmonitor" data-completeness check; "BOP" onboarding = 3 human-guided sessions + shadow-running. Weak spot (Play Store 2026-07): parent app 2.7★, staff app 2.2★ — crashes, late/missing push notifications, sync bugs. **Mobile reliability is a differentiator.**
- **Differentiation bets for ChildCare** (nothing on either competitor's public materials): Flemish risk-analysis support (035), crisis/verplichte-melding workflows (036), verontrusting registration (036), retention automation (038), productised self-service migration + full tenant export (039), attendance-register parent confirmation built for the Flemish rules (037), caregiver PIN/kiosk model (008a), NL/FR/EN i18n from day one.
- Competitor parity items in the backlog: parent-app payments + automatic reminders + receipts (014a, PSP to be chosen: Stripe/Mollie/POM — investigate before implementation), accounting export (029), occupancy reporting + data-completeness monitor (018), forward occupancy/placement/doorstroom planning (040), staff app (027), HR dossier + time registration (028), digital enrollment + e-signature (023/024).

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
| 2 | MeMoQ quality tracking, fiscal attestations (tax certificates), developmental milestones, management reporting, email communications, invoice payments & reminders (014a); government reporting: kinderopvangtoeslag/AARON submission (033), jaarregistraties XML (034); compliance: safety & risk-analysis register (035), crisis & mandatory reporting (036), attendance-register compliance (037), data retention & GDPR lifecycle (038); growth: tenant onboarding & supplier-switch migration (039), occupancy & placement planning (040); regulatory: date-versioned BKR 2027 ruleset (041); family & floor operations (added 2026-07-19): settling-in planning (042), medication authorisations (043), day-specific pickups (044), activity planning (045), parent survey (046), sleep checks (047), supplies requests (048), message auto-translation (049) |
| 3 | IKT subsidy integration (Opgroeien SOAP webservices + X.509 certificate), Belcotax 281.86 automated submission, advanced financial admin |

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
11. Legal retention classes (10y/5y/3y/12m — see Bewaartermijnen) respected in every data model; no destructive delete of retention-bound records outside the retention engine (038).
12. Government submissions (AARON, jaarregistraties, IKT, Belcotax) always keep an immutable audit copy of exactly what was sent.
