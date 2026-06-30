# Authentication Architecture

ChildCare uses a custom JWT-based auth system shared across the ASP.NET Core backend, Next.js web app, and Expo mobile app. There is no third-party auth service — you own the full implementation.

---

## Token model

| Token | Lifetime | Storage | Purpose |
|---|---|---|---|
| Access token | 60 min (configurable) | In-memory only | Authorize API requests |
| Refresh token | 30 days (configurable) | httpOnly cookie (web) / SecureStore (mobile) | Obtain a new access token silently |

Access tokens are short-lived and never persisted — they disappear on page reload or app restart. The refresh token (stored securely, never exposed to JavaScript) is used to get a fresh access token on startup and after expiry. This is the standard split-token pattern.

Each device gets its own refresh token row in `UserRefreshTokens`. Logging out on one device does not affect other sessions.

---

## Flows

### Email / password registration

```
Client                          Backend
  │── POST /api/auth/register ─►│  Create user (EmailVerified = false)
  │                              │  Issue refresh token row
  │                              │  Send verification email (24 h link)
  │◄─ { accessToken, refreshToken, user: { emailVerified: false } } ──│
  │
  │   (user clicks link in email)
  │── POST /api/auth/verify-email?token=... ─►│  Set EmailVerified = true
  │◄─ 200 OK ────────────────────────────────│
```

### Login

```
Client                          Backend
  │── POST /api/auth/login ────►│  Verify password
  │                              │  Insert new UserRefreshToken row
  │◄─ { accessToken, refreshToken, user } ───│
```

### Silent token refresh (on app start / 401)

```
Client                          Backend
  │── POST /api/auth/refresh ──►│  Find token row, check expiry
  │    { refreshToken }          │  Delete old row, insert new row (rotation)
  │◄─ { accessToken, refreshToken, user } ───│
```

### Google / Apple OAuth

```
Client                           Provider          Backend
  │── OAuth flow ───────────────►│
  │◄─ idToken / identityToken ───│
  │── POST /api/auth/google ─────────────────────►│  Validate token via provider
  │   { idToken }                                  │  Upsert user (EmailVerified = true)
  │◄─ { accessToken, refreshToken, user } ─────────│
```

OAuth users are marked `EmailVerified = true` immediately — the provider already verified it.

**Android redirect detail:** Google Sign-In on Android uses a Chrome Custom Tab. After the user authenticates, Google redirects to `<yourscheme>://oauthredirect`. Android catches this via the intent filter in `AndroidManifest.xml` and routes it to the `oauthredirect` screen in Expo Router, which calls `WebBrowser.maybeCompleteAuthSession()` to resolve the pending auth session.

For Google Sign-In to work on Android debug builds you need:
- The SHA-1 fingerprint of your debug keystore registered in Google Cloud Console under your Android OAuth client
- `adb reverse tcp:5001 tcp:5001` run once per emulator session (so `localhost` inside the emulator reaches your dev backend)

### Logout (single device)

```
Client                          Backend
  │── POST /api/auth/logout ───►│  Delete this device's token row only
  │    { refreshToken }          │  (other devices stay active)
  │◄─ 204 No Content ───────────│
```

### Password reset

```
Client                          Backend
  │── POST /api/auth/forgot-password ──►│  Generate reset token (1 h)
  │    { email }                         │  Send reset email
  │◄─ 200 (always, no enumeration) ─────│
  │
  │── POST /api/auth/reset-password ───►│  Verify token, hash new password
  │    { token, newPassword }            │  Delete ALL refresh token rows (all devices)
  │◄─ 200 OK ──────────────────────────│
```

---

## Web (Next.js BFF)

The browser never holds the refresh token — it lives in an httpOnly cookie managed by Next.js API routes:

| Route | What it does |
|---|---|
| `POST /api/set-refresh-token` | Stores the refresh token in an httpOnly cookie after login |
| `POST /api/refresh` | Reads the cookie, calls the backend `/api/auth/refresh`, updates the cookie |
| `POST /api/clear-refresh-token` | Deletes the cookie on logout |

On logout, the web app must also call `POST /api/auth/logout` (with the refresh token read from the cookie via the BFF) to revoke the server-side token row.

---

## Mobile (Expo)

- Refresh token → `expo-secure-store` (iOS Keychain / Android Keystore)
- Access token → module-level variable in `services/api.ts` (in-memory)
- On `401`: one automatic refresh attempt before routing to `/login`
- On app start: reads stored refresh token, calls `/api/auth/refresh` to restore the session

---

## Database tables

```
Users
  Id, Email, PasswordHash, GoogleId, AppleId
  EmailVerified, EmailVerificationToken, EmailVerificationExpiry
  PasswordResetToken, PasswordResetExpiry
  ExpoPushToken, CreatedAt, Stripe*

UserRefreshTokens
  Id, UserId (FK → Users), Token (unique), ExpiresAt, CreatedAt
```

Deleting a user cascades and removes all their refresh token rows.

---

## Configuration

```jsonc
// appsettings.json (template values — override per environment)
{
  "Jwt": {
    "Secret": "min 32 chars — generate with: openssl rand -base64 32",
    "Issuer": "ChildCare",
    "Audience": "ChildCareApp",
    "AccessTokenExpiryMinutes": "60",
    "RefreshTokenExpiryDays": "30"
  },
  "App": {
    "Scheme": "yourappscheme",                       // deep link scheme (matches Expo app.config.js scheme)
    "ResetBaseUrl": "yourappscheme://reset-password", // or https://yourapp.com/reset-password
    "VerifyBaseUrl": "yourappscheme://verify-email"   // or https://yourapp.com/verify-email
  },
  "Cors": {
    // Empty = AllowAnyOrigin (dev convenience). Set explicit origins in production.
    "AllowedOrigins": ["https://yourapp.com"]
  },
  "Google": {
    "AllowedClientIds": ["android-client-id", "ios-client-id", "web-client-id"]
  },
  "Apple": {
    "BundleId": "com.yourcompany.yourapp"
  },
  "Email": {
    "SmtpHost": "",        // leave empty in dev — links are logged to console instead
    "SmtpPort": "587",
    "FromAddress": "noreply@yourapp.com",
    "Username": "",
    "Password": ""         // API key for SendGrid/Postmark/Mailgun etc.
  }
}
```

---

## Security notes

- **Rate limiting**: login/register/password endpoints → 5 req / 15 min per IP. OAuth endpoints → 30 req / 15 min.
- **Token rotation**: every refresh call issues a new token and invalidates the old one.
- **Password reset invalidates all sessions**: when a user resets their password, every device is logged out.
- **No clock skew**: JWT validation uses `ClockSkew = TimeSpan.Zero` — tokens expire precisely at their stated time.
- **Email enumeration**: `forgot-password` always returns 200 regardless of whether the email exists.
