# Setup Checklist

Work through this top-to-bottom before running `dotnet run` or `expo start`. Every `YOUR_*` placeholder must be replaced.

**Prerequisites:** Node 20+, .NET 10 SDK, Docker Desktop, and the EF Core CLI:

```bash
dotnet tool install --global dotnet-ef
```

---

## 0. Rename the project

Run the setup script from the repo root — it handles all renaming automatically:

```bash
node scripts/setup.js
```

The script asks for your app name (PascalCase, e.g. `FitTrack`) and bundle ID (e.g. `com.acme.fittrack`). It replaces every token across the codebase, renames directories and project files, and resets git history to a clean initial commit.

After it runs, verify these user-facing strings were updated correctly:

| What to verify | Where |
|---|---|
| App name on login/register screen | `mobile/app/(auth)/login.tsx` and `register.tsx` — the `<Text>` below the logo emoji |
| Email subjects | `backend/<YourApp>.Api/Services/EmailService.cs` — `"Verify your <AppName> email"` and `"Reset your <AppName> password"` |
| JWT Issuer / Audience claims | `backend/<YourApp>.Api/appsettings.json` → `Jwt.Issuer` and `Jwt.Audience` |
| Deep-link scheme | `backend/<YourApp>.Api/appsettings.json` → `App.Scheme`, `App.ResetBaseUrl`, `App.VerifyBaseUrl` |
| Email from address | `backend/<YourApp>.Api/appsettings.json` → `Email.FromAddress` |
| Stripe redirect URLs | `backend/<YourApp>.Api/appsettings.json` → `Stripe.SuccessUrl`, `Stripe.CancelUrl` |

- [ ] `node scripts/setup.js` completed successfully
- [ ] Deep-link scheme matches `app.config.js` → `scheme`
- [ ] Stripe redirect URLs point to your real domain

---

## 1. Bundle ID / package name

Pick your reverse-domain app ID (e.g. `com.acme.myapp`) and set it in three places:

- [ ] `mobile/app.config.js` → `ios.bundleIdentifier`
- [ ] `mobile/app.config.js` → `android.package`
- [ ] `backend/ChildCare.Api/appsettings.json` → `Apple.BundleId`

Also update `App.Scheme` in `appsettings.json` to a short URL-safe name (e.g. `myapp`). This is used for deep-link callbacks.

---

## 2. Local backend secrets

```bash
cd backend/ChildCare.Api
cp appsettings.Development.example.json appsettings.Development.json
```

Fill in `appsettings.Development.json` — this file is gitignored and never committed.

### Local database (Docker)

The example file already contains the correct connection string for the local Docker Postgres. Start the database:

```bash
# From the repo root
docker compose up -d
```

Migrations run automatically when the API starts in Development mode — no manual `dotnet ef` step needed for local dev.

- [ ] Docker Desktop is running before starting the backend
- [ ] `docker compose up -d` runs without errors (`docker ps` shows `childcare-postgres` healthy)

---

## 3. JWT secret

Generate a secret:

```bash
openssl rand -base64 32
```

- [ ] `backend/ChildCare.Api/appsettings.Development.json` → `Jwt.Secret`

---

## 4. Mobile environment

```bash
cd mobile
cp .env.example .env
```

Find your LAN IP (the simulator/device can't reach `localhost`):

```bash
# macOS
ipconfig getifaddr en0
```

- [ ] `mobile/.env` → `EXPO_PUBLIC_API_BASE_URL=http://<your-ip>:5001`

> **Leave the Google client ID fields empty for now** — you will fill them in after step 5.

---

## 5. Google Sign In

### Why three clients?

Each OAuth client produces an ID token whose `aud` (audience) claim equals the client ID that initiated the flow. The backend validates incoming tokens against `Google.AllowedClientIds`, so it needs to know all three:

| Client | Purpose |
|---|---|
| **iOS** | Identifies your app on iOS — `aud` in iOS tokens |
| **Android** | Identifies your app on Android — `aud` in Android tokens |
| **Web** | Used by Expo Go (can't use native clients) and as the fallback client |

**iOS-only for now?** You can skip the Android client and remove it from `AllowedClientIds`. Add it back when you're ready to ship Android.

> Note: the Android client requires your signing key's SHA-1 fingerprint, which differs between debug and release. You'll typically end up with two Android clients (one per build type).

### Step 1 — OAuth consent screen

Before creating any client, configure the consent screen once per GCP project:

1. Go to [Google Cloud Console](https://console.cloud.google.com) → **APIs & Services → OAuth consent screen**
2. Select **External** as the user type (works for development and production)
3. Fill in **App name**, **User support email**, and **Developer contact email**
4. Save — you do not need to add scopes or test users for this flow

### Step 2 — Create OAuth credentials

Go to **APIs & Services → Credentials → Create Credentials → OAuth client ID** and create:

| Type | Required field |
|---|---|
| **iOS** | Bundle ID matching `ios.bundleIdentifier` |
| **Android** | Package name + SHA-1 from `keytool -keystore ~/.android/debug.keystore -list -v -storepass android` (debug) |
| **Web** | Add an **Authorized redirect URI**: `https://auth.expo.io/@{your-expo-username}/{your-app-slug}` |

> The redirect URI on the Web client is required for the Expo Go / web OAuth flow. Replace `{your-expo-username}` and `{your-app-slug}` with your actual values (visible in `app.config.js`).

### Step 3 — Wire them up

The same client IDs go in both the mobile app and the backend:

> **Common mistake:** copy each client ID carefully — a single extra character (e.g. a stray letter at the start) will cause Google Sign In to silently return Unauthorized with no useful error message. Double-check the values if OAuth fails.

- [ ] `mobile/.env` → `EXPO_PUBLIC_GOOGLE_IOS_CLIENT_ID`, `EXPO_PUBLIC_GOOGLE_ANDROID_CLIENT_ID`, `EXPO_PUBLIC_GOOGLE_WEB_CLIENT_ID`
- [ ] `backend/ChildCare.Api/appsettings.json` → `Google.AllowedClientIds` (list every client ID you created)
- [ ] `web/.env.local` → `NEXT_PUBLIC_GOOGLE_CLIENT_ID` — set to your **Web** client ID. This enables the "Continue with Google" button on the web app. If omitted, the button is hidden with no error — easy to miss.

### Rebuild native

```bash
cd mobile
npm install
npx expo prebuild --clean --platform ios
npx expo run:ios
```

> Google Sign In uses `expo-auth-session` (web-based OAuth flow) — no native Google SDK needed.

---

## 6. Apple Sign In

> Requires an Apple Developer account ($99/yr). Does **not** work in Expo Go.

Unlike Google, there is **no OAuth client ID, no client secret, and no redirect URI** to configure. The `expo-apple-authentication` library handles the native iOS Sign In with Apple flow entirely — Apple just needs your App ID registered with the capability enabled.

### Register the App ID

1. Go to [developer.apple.com/account](https://developer.apple.com/account) and sign in
2. Click **Certificates, IDs & Profiles** in the left sidebar
3. Click **Identifiers** in the left sidebar
4. Click + (top left) to add a new identifier
5. Select **App IDs** → Continue → select **App** → Continue
6. Fill in:
   - **Description:** any name (e.g. `ChildCare`)
   - **Bundle ID:** select **Explicit** and enter exactly the value from `mobile/app.config.js` → `ios.bundleIdentifier`
7. Scroll the capabilities list and check **Sign In with Apple**
8. Click **Continue** → **Register**

That's it — no redirect URIs, no keys to download.

### Backend

- [ ] `backend/ChildCare.Api/appsettings.json` → `Apple.BundleId` — must match the Bundle ID exactly (the backend validates the `aud` claim in Apple's identity token against this value)

### Rebuild native

```bash
cd mobile
npm install
npx expo prebuild --clean --platform ios
npx expo run:ios
```

> `npm install` is required before prebuild — the command fails if the `expo` package isn't installed locally.

**Testing on simulator:** sign into an Apple ID first via **Simulator → Settings → Sign in to iPhone**.

---

## 7. Password reset (SMTP)

In development, the reset link is printed to the console — no SMTP needed. For production, add credentials to your Cloud Run environment variables (or `appsettings.json` locally):

```
Email__SmtpHost=smtp.sendgrid.net
Email__SmtpPort=587
Email__FromAddress=noreply@yourapp.com
Email__Username=apikey
Email__Password=YOUR_SENDGRID_API_KEY
```

Works with SendGrid, Mailgun, AWS SES, or any SMTP provider.

---

## 8. Sentry (optional)

1. Create a React Native project at [sentry.io](https://sentry.io)
2. Copy the DSN from **Project → Settings → Client Keys**

- [ ] `mobile/.env` → `EXPO_PUBLIC_SENTRY_DSN=https://...`
- [ ] `mobile/app.config.js` → `plugins[@sentry/react-native/expo].organization` and `.project`

---

## 9. Stripe

1. Create an account at [dashboard.stripe.com](https://dashboard.stripe.com) if you don't have one
2. Create a **Product** → add a recurring **Price** (e.g. $9.99/month)
3. Copy the `price_...` ID — this is your `PriceId`

### Local development

> **Every dev session:** run the webhook forwarder in a dedicated terminal alongside the backend. Without it, payments complete but subscription status never updates — the webhook never reaches your local API.

```bash
# Install the Stripe CLI (once)
brew install stripe/stripe-cli/stripe
stripe login

# Run every time you want to test payments locally (keep this terminal open)
stripe listen --forward-to localhost:5001/api/payments/webhook
# The CLI prints a whsec_... secret — paste it into appsettings.Development.json
```

- [ ] `backend/ChildCare.Api/appsettings.Development.json` → `Stripe:SecretKey` (`sk_test_...`)
- [ ] `backend/ChildCare.Api/appsettings.Development.json` → `Stripe:WebhookSecret` (`whsec_...` from CLI)
- [ ] `backend/ChildCare.Api/appsettings.Development.json` → `Stripe:PriceId` (`price_...`)

### Production (GitHub secrets)

Go to your GitHub repo → **Settings → Secrets and variables → Actions** and add:

| Secret | Value |
|---|---|
| `STRIPE_SECRET_KEY` | `sk_live_...` (live secret key from Stripe Dashboard) |
| `STRIPE_WEBHOOK_SECRET` | `whsec_...` from Stripe Dashboard → Webhooks → your endpoint |
| `STRIPE_PRICE_ID` | `price_...` recurring price ID |

For the production webhook endpoint, go to **Stripe Dashboard → Webhooks → Add endpoint**:
- URL: `https://your-api-domain.com/api/payments/webhook`
- Events: `customer.subscription.created`, `customer.subscription.updated`, `customer.subscription.deleted`

> The checkout flow includes a **14-day free trial** out of the box. Users start trialing immediately without entering a card, then convert to paid after the trial ends.

### Test the flow locally

1. Start the backend: `dotnet run --project backend/ChildCare.Api`
2. In a second terminal: `stripe listen --forward-to localhost:5001/api/payments/webhook`
3. Open Scalar at `http://localhost:5001/scalar/v1`, log in, hit `POST /api/payments/checkout`
4. Complete checkout with test card `4242 4242 4242 4242`
5. Hit `GET /api/payments/status` — should show `Trialing`

---

## 10. EAS Build

```bash
cd mobile
eas login
eas init    # links your Expo account and writes projectId to app.config.js
```

- [ ] Verify `extra.eas.projectId` and `updates.url` are updated in `app.config.js`
- [ ] Add `EXPO_TOKEN` to GitHub → Settings → Secrets → Actions (for the EAS CI workflow)

---

## 11. GCP Deployment

See **[infra/gcp/SETUP.md](infra/gcp/SETUP.md)** for the full step-by-step guide. Summary:

1. `gcloud auth login` + `gcloud config set project YOUR_PROJECT_ID`
2. `cd infra/gcp && terraform init && terraform apply` — creates everything (service account, Workload Identity, Artifact Registry, Cloud Run)
3. Add GitHub secrets from Terraform outputs
4. Push to `master` — `deploy-gcp.yml` deploys automatically

### Production migrations

EF Core never auto-migrates in production. After every schema change:

```bash
dotnet ef migrations script <FromMigration> <ToMigration> \
  --project backend/ChildCare.Api \
  --output migration.sql
```

Run `migration.sql` against your production database (`psql`, Supabase SQL editor, or your cloud provider's query tool).

> **Why manual?** Keeps schema changes and code deploys decoupled — migrate first, verify, then deploy.

---

## 12. Azure Deployment (alternative to GCP)

See **[infra/azure/SETUP.md](infra/azure/SETUP.md)** for the full step-by-step guide. Summary:

1. `az login` + `az provider register --namespace Microsoft.App --wait`
2. `cd infra/azure && terraform init && terraform apply` — creates everything (ACR, Container Apps, Managed Identity, federated credentials)
3. Add GitHub secrets from Terraform outputs
4. Push to `master` — `deploy-azure.yml` deploys automatically

### Production migrations

Same as GCP — run the generated `migration.sql` against your production database before deploying code.

---

## 13. README

- [ ] Replace `YOUR_USERNAME` in the clone URL with your GitHub username
