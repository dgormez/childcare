# ChildCare — Expo + ASP.NET Core Boilerplate

A production-ready monorepo starter for shipping React Native apps backed by a .NET API. Skip the boilerplate, own the stack.

**Stack:** Expo SDK 54 · NativeWind v4 · Zustand · expo-sqlite · Next.js 15 · ASP.NET Core 10 · EF Core · PostgreSQL · Stripe · GCP Cloud Run · EAS Build

[![Buy Me A Coffee](https://img.shields.io/badge/Buy%20Me%20a%20Coffee-dgormez-FFDD00?style=flat&logo=buy-me-a-coffee&logoColor=black)](https://buymeacoffee.com/dgormez)

---

## Why ChildCare?

Most React Native starters use a JavaScript backend. ChildCare is built for .NET developers who want a mobile app without switching ecosystems.

| What's included | Notes |
|---|---|
| Email / Google / Apple sign-in | All three on mobile; email + Google on web (Apple Sign In is iOS-only) |
| JWT auth with silent refresh | Access token in memory, refresh token in SecureStore / httpOnly cookie |
| Password reset via email | SMTP — works with SendGrid, Mailgun, etc. |
| Local SQLite + API sync | Offline-first with incremental background sync (mobile) |
| Push notifications | expo-notifications + Expo push service |
| Stripe subscriptions | Checkout, Customer Portal, webhooks, 14-day free trial |
| Habit tracker demo | Free tier (3 habits), Pro (unlimited) — shows the full paywall flow |
| Next.js 15 web app | Auth, habits, subscription — mirrors the mobile experience |
| Scalar API UI | Interactive docs at `/scalar/v1` with Bearer auth (development only) |
| GCP Cloud Run deploy | Docker-based, GitHub Actions workflow included |
| Azure Container Apps | Alternative deploy target with Terraform + GitHub Actions |
| EAS Build CI | App Store + Play Store builds on push |
| Sentry crash reporting | Pre-wired, just add your DSN |

---

## Prerequisites

- Node 20+ and npm
- .NET 10 SDK
- EF Core CLI — `dotnet tool install --global dotnet-ef`
- Docker Desktop (for local Postgres)
- Expo CLI — `npm install -g expo-cli`
- EAS CLI — `npm install -g eas-cli`
- [Stripe](https://dashboard.stripe.com) account (for payments)
- [GCP](https://console.cloud.google.com) or [Azure](https://portal.azure.com) project (for production deploy)
- [Sentry](https://sentry.io) project — React Native (optional)

---

## Quick start

> **Before you run the app:** work through [SETUP_CHECKLIST.md](./SETUP_CHECKLIST.md) steps 1–9 to fill in credentials and API keys. Start with step 1 below (rename) first, then return to the checklist. Hit a problem? See [TROUBLESHOOTING.md](./TROUBLESHOOTING.md).

### 1. Rename the project to your app

```bash
node scripts/setup.js
```

The script will ask for your app name (PascalCase, e.g. `FitTrack`) and bundle ID (e.g. `com.acme.fittrack`). It replaces every `ChildCare` token across the codebase, renames directories and project files, and resets the git history to a clean initial commit.

```bash
cd mobile && npm install
```

### 2. Start the backend

```bash
# Start local Postgres
docker compose up -d

# Run the API (auto-migrates on first run)
dotnet run --project backend/<YourApp>.Api
# API + Scalar UI → http://localhost:5001/scalar/v1
```

### 3. Point the mobile app at your machine

```bash
# macOS — find your LAN IP
ipconfig getifaddr en0

# Edit mobile/.env
EXPO_PUBLIC_API_BASE_URL=http://<your-ip>:5001
```

> **iOS simulator** — `localhost` works directly. No IP needed.
>
> **Android emulator** — keep `EXPO_PUBLIC_API_BASE_URL=http://localhost:5001`, then run once per emulator session after it boots:
> ```bash
> adb reverse tcp:5001 tcp:5001
> ```
> This maps `localhost:5001` inside the emulator to your machine.
>
> **Physical device** — use your machine's LAN IP as shown above.

### 4. Start the mobile app

```bash
cd mobile
npx expo start
```

Press `i` for the iOS simulator or `a` for the Android emulator. Register an account — the Habits demo should sync end-to-end.

> **Android build note:** if the build fails with a Java version error, your system Java may be too new. Force Java 17:
> ```bash
> JAVA_HOME=$(/usr/libexec/java_home -v 17) npx expo run:android
> ```

### 5. Start the web app

```bash
cd web
npm run dev
```

Open `http://localhost:3000`. The web app mirrors the mobile experience — auth, habits, and Stripe subscription all work the same way.

---

## Deploy to production

### Backend — GCP Cloud Run

See [SETUP_CHECKLIST.md → GCP Deployment](./SETUP_CHECKLIST.md#gcp-deployment) for the one-time GCP setup (service account, Workload Identity, Artifact Registry).

Once the GitHub secrets are set, push to `master` and the `deploy-gcp.yml` workflow handles the rest.

**Important:** EF Core never auto-migrates in production. After every schema change, generate the SQL and run it against your production database:

```bash
dotnet ef migrations script <FromMigration> <ToMigration> \
  --project backend/ChildCare.Api \
  --output migration.sql
# Run migration.sql against your production database (Supabase SQL editor, psql, etc.)
```

### Mobile — EAS Build

```bash
cd mobile
eas login
eas init          # links project, writes projectId to app.json
eas build --platform all --profile preview   # first test build
```

Add `EXPO_TOKEN` to your GitHub repository secrets to enable the EAS CI workflow.

---

## Project structure

```
childcare/
├── mobile/                      # Expo React Native app
│   ├── app/
│   │   ├── _layout.tsx          # root layout — bootstrap + auth guard
│   │   ├── oauthredirect.tsx    # OAuth callback handler (Android deep link)
│   │   ├── (auth)/              # login, register, forgot/reset password
│   │   ├── (tabs)/              # today, habits manage, subscription, settings
│   │   └── habit/               # add/edit habit modals
│   ├── hooks/
│   │   ├── useSync.ts           # incremental background sync
│   │   └── useSubscription.ts   # Stripe checkout / portal / status
│   ├── services/
│   │   ├── api.ts               # HTTP client with JWT auto-refresh
│   │   ├── auth.ts              # register / login / logout / session restore
│   │   ├── googleAuth.ts        # Google Sign-In hook (iOS / Android / web)
│   │   └── localDb.ts           # SQLite persistence (web: no-op shim)
│   ├── store/useStore.ts        # Zustand global state
│   └── types/index.ts
├── web/                         # Next.js 15 web app
│   ├── app/
│   │   ├── (auth)/              # login, register, forgot/reset password
│   │   ├── (app)/               # habits, settings, subscription (protected)
│   │   └── api/                 # route handlers for httpOnly cookie auth
│   ├── components/AuthProvider.tsx
│   └── lib/
│       ├── api.ts               # HTTP client (mirrors mobile)
│       └── auth.ts              # login / logout / session restore
├── backend/
│   └── ChildCare.Api/
│       ├── Endpoints/           # AuthEndpoints, HabitEndpoints, PaymentEndpoints
│       ├── Models/              # User, Habit, HabitCompletion, SubscriptionStatus
│       ├── Services/            # AuthService, StripeService, JwtService, EmailService
│       ├── Data/AppDbContext.cs
│       └── Program.cs
├── infra/
│   ├── gcp/                     # Terraform for GCP Cloud Run
│   └── azure/                   # Terraform for Azure Container Apps
└── .github/workflows/
    ├── deploy-gcp.yml           # Backend → GCP Cloud Run on push to master
    ├── deploy-azure.yml         # Backend → Azure Container Apps (alternative)
    └── eas-build.yml            # Mobile → EAS Build
```

---

## Replacing Habits with your own domain

**Backend:**
1. Add your model in `Models/`
2. Add a `DbSet<>` in `AppDbContext` and configure it in `OnModelCreating`
3. Add `Endpoints/YourEntityEndpoints.cs` — use `HabitEndpoints.cs` as the template (it shows subscription gating)
4. Register it in `Program.cs`: `app.MapYourEntityEndpoints();`
5. `dotnet ef migrations add AddYourEntity` — then run the generated SQL against your production database

**Mobile:**
1. Add the TypeScript type in `types/index.ts`
2. Add the SQLite table in `services/localDb.ts`
3. Add API functions in `services/api.ts`
4. Add state to `store/useStore.ts`
5. Add screens under `app/`

**Web:**
1. Add API functions in `web/lib/api.ts`
2. Add pages under `web/app/(app)/`

---

## Auth flow

```
App opens
  └─ tryRestoreSession()
       ├─ Online  → POST /api/auth/refresh → fresh access token → tabs
       ├─ Offline → cached userId/email → tabs (sync fails silently)
       └─ No token / expired → /(auth)/login

Login / Register
  └─ POST /api/auth/login|register
       └─ { accessToken, refreshToken, user }
            ├─ accessToken   → Zustand (in-memory only)
            ├─ refreshToken  → expo-secure-store
            └─ userId, email → SQLite config table

API request
  └─ Authorization: Bearer <accessToken>
       └─ 401 → POST /api/auth/refresh → retry once → or SESSION_EXPIRED
```

---

## Environment variable reference

### Backend (`appsettings.json` / Cloud Run env vars)

| Key | Description |
|-----|-------------|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string (local Docker for dev, Supabase/managed PostgreSQL for prod) |
| `Jwt:Secret` | Random 32+ char secret (`openssl rand -base64 32`) |
| `Jwt:Issuer` | Token issuer claim (default: `ChildCare`) |
| `Jwt:Audience` | Token audience claim (default: `ChildCareApp`) |
| `Jwt:AccessTokenExpiryMinutes` | Default: `60` |
| `Jwt:RefreshTokenExpiryDays` | Default: `30` |
| `Apple:BundleId` | Your iOS bundle identifier |
| `Google:AllowedClientIds` | Array of all three Google OAuth client IDs |
| `Email:SmtpHost` / `Email:Password` | SMTP credentials for password reset |
| `Stripe:SecretKey` | Stripe secret key (`sk_test_...` / `sk_live_...`) |
| `Stripe:WebhookSecret` | Stripe webhook signing secret (`whsec_...`) |
| `Stripe:PriceId` | Stripe recurring Price ID (`price_...`) |

### Mobile (`mobile/.env`)

| Key | Description |
|-----|-------------|
| `EXPO_PUBLIC_API_BASE_URL` | Backend URL (LAN IP for dev, Cloud Run URL for prod) |
| `EXPO_PUBLIC_GOOGLE_IOS_CLIENT_ID` | Google OAuth iOS client ID |
| `EXPO_PUBLIC_GOOGLE_ANDROID_CLIENT_ID` | Google OAuth Android client ID |
| `EXPO_PUBLIC_GOOGLE_WEB_CLIENT_ID` | Google OAuth Web client ID |
| `EXPO_PUBLIC_SENTRY_DSN` | Sentry DSN (optional) |

### Web (`web/.env.local`)

| Key | Description |
|-----|-------------|
| `NEXT_PUBLIC_API_BASE_URL` | Backend URL (e.g. `http://localhost:5001` for dev) |
| `NEXT_PUBLIC_GOOGLE_CLIENT_ID` | Google OAuth **web** client ID — enables the "Continue with Google" button (optional; button is hidden if unset) |

---

## License

See [LICENSE](./LICENSE). Each purchase grants one developer or team a license to use MiniStack across unlimited personal and client projects.
