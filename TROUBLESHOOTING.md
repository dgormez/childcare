# Troubleshooting

Common failure modes and how to resolve them. Check here before opening an issue.

---

## Google Sign-In silently returns Unauthorized

**Symptoms:** Tapping "Continue with Google" shows no error but the user is not signed in. The backend logs a 401 with no useful message.

**Root cause:** Almost always a client ID mismatch or a missing redirect URI.

### Check 1 — Client ID copied incorrectly

Open `appsettings.json` → `Google.AllowedClientIds`. The values must exactly match the client IDs in Google Cloud Console → **APIs & Services → Credentials**. A single extra character causes a silent failure.

```json
"Google": {
  "AllowedClientIds": [
    "123456789-abc.apps.googleusercontent.com",   // iOS
    "123456789-xyz.apps.googleusercontent.com",   // Android
    "123456789-web.apps.googleusercontent.com"    // Web
  ]
}
```

Compare each value character-by-character with the Google Cloud Console. Copy-paste directly — do not type manually.

### Check 2 — Web client is missing an Authorized redirect URI (mobile)

The Expo OAuth flow routes through a web client. The **Web** OAuth client in Google Cloud Console must have an Authorized redirect URI set to:

```
https://auth.expo.io/@{your-expo-username}/{your-app-slug}
```

Replace `{your-expo-username}` with your Expo account username and `{your-app-slug}` with the `slug` field in `mobile/app.config.js` (default: `childcare`).

If this URI is missing, the OAuth flow completes but the token is rejected.

### Check 3 — Web client ID missing from web/.env.local

The "Continue with Google" button on the web app requires:

```
NEXT_PUBLIC_GOOGLE_CLIENT_ID=your-web-client-id.apps.googleusercontent.com
```

If this variable is absent, the button is silently hidden — no error is shown. Copy the value from your **Web** OAuth client in Google Cloud Console.

### Check 4 — OAuth consent screen not published (production)

If your app is in **Testing** mode in the OAuth consent screen, only users explicitly added as test users can sign in. Either add users as testers, or publish the consent screen.

---

## Payment completes but subscription status stays None / not Pro

> **This is almost always a missing webhook forwarder.** The Stripe CLI must be running in a separate terminal every time you test payments locally. It is not a one-time setup step — it must be running for the session.

```bash
stripe listen --forward-to localhost:5001/api/payments/webhook
```

After paying you should see this in the CLI terminal:
```
--> customer.subscription.created [evt_xxx]
<-- [200] POST http://localhost:5001/api/payments/webhook
```

If you do not see it, nothing will update the database regardless of what the app does.

---

## Stripe webhooks not received locally

**Symptoms:** Checkout completes successfully but `GET /api/payments/status` still shows no subscription. The backend never logs a webhook event.

**Root cause:** The Stripe CLI is not running or is forwarding to the wrong URL.

### Step 1 — Start the CLI forwarder

Open a second terminal while the backend is running and execute:

```bash
stripe listen --forward-to localhost:5001/api/payments/webhook
```

The CLI prints a webhook signing secret:

```
> Ready! Your webhook signing secret is whsec_xxxxxxxxxxxx
```

### Step 2 — Paste the secret into your dev config

The secret printed by the CLI is only valid for that session. Paste it into `appsettings.Development.json`:

```json
"Stripe": {
  "WebhookSecret": "whsec_xxxxxxxxxxxx"
}
```

Restart the backend after editing this value.

### Step 3 — Verify the event is reaching the endpoint

After completing a checkout, the CLI terminal should print something like:

```
2024-01-01 12:00:00   --> customer.subscription.created [evt_xxx]
2024-01-01 12:00:00  <-- [200] POST http://localhost:5001/api/payments/webhook
```

If you see `[400]` or `[500]`, check the backend logs for signature validation errors — this usually means the `WebhookSecret` value is stale (the CLI generates a new one each time it starts).

### Step 4 — Check the Stripe dashboard for event delivery (production)

In production, go to **Stripe Dashboard → Webhooks → your endpoint → Recent deliveries**. A failed delivery shows the HTTP status code and response body your API returned, which is usually enough to diagnose the issue.

---

## EAS Build fails — EXPO_TOKEN missing or invalid

**Symptoms:** The `eas-build.yml` GitHub Actions workflow fails at the `eas build` step with an authentication error.

**Root cause:** The `EXPO_TOKEN` secret is missing, expired, or scoped to the wrong account.

### Step 1 — Generate a new token

1. Go to [expo.dev/accounts/\[username\]/settings/access-tokens](https://expo.dev/accounts/)
2. Click **Create Token**
3. Give it a name (e.g. `github-actions`) and copy the value immediately — it is not shown again

### Step 2 — Add it to GitHub

Go to your GitHub repository → **Settings → Secrets and variables → Actions → New repository secret**:

| Name | Value |
|---|---|
| `EXPO_TOKEN` | The token you just generated |

### Step 3 — Verify the EAS project is linked

`mobile/app.config.js` must have a valid `extra.eas.projectId`. If `YOUR_EAS_PROJECT_ID` is still the placeholder value, run:

```bash
cd mobile
eas login
eas init
```

`eas init` links your local project to your Expo account and writes the correct `projectId` into `app.config.js`. Commit this change.

### Step 4 — Check the workflow trigger

`eas-build.yml` only runs on manual dispatch (`workflow_dispatch`) or pushes to `master` that touch `mobile/**`. If you pushed to a different branch or changed only backend files, the workflow will not trigger.

---

## Backend won't start — Jwt:Secret not configured

**Symptoms:** `dotnet run` throws on startup:

```
Unhandled exception. System.InvalidOperationException: Jwt:Secret is not configured.
```

**Fix:** This value is required and has no default. Create `appsettings.Development.json` from the example file and fill it in:

```bash
cd backend/ChildCare.Api
cp appsettings.Development.example.json appsettings.Development.json
# Then edit appsettings.Development.json and set Jwt:Secret
```

Generate a secret:

```bash
openssl rand -base64 32
```

---

## Backend won't start — Postgres connection refused

**Symptoms:** The API starts but immediately throws a connection error, or the auto-migration fails at startup.

**Fix:** The local Postgres container is not running. Start it:

```bash
docker compose up -d
```

Verify it is healthy:

```bash
docker ps
# Should show childcare-postgres with status: healthy
```

If the container exists but is unhealthy, inspect the logs:

```bash
docker logs childcare-postgres
```

A common cause is a port conflict — another Postgres instance is already on port 5432. Stop the conflicting process or change the port in `docker-compose.yml` and `appsettings.Development.json`.
