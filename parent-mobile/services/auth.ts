/**
 * auth.ts — login (password + Google + Apple), silent refresh, logout, and session
 * restoration, mirroring mobile/services/auth.ts's shape against the same feature-003 auth
 * contract (organisationSlug + email/idToken/identityToken; AuthSessionResponse carries a
 * `role` claim).
 *
 * Unlike mobile/, there is no `fetchAndApplyStaffMe` equivalent (no parent StaffProfile) and no
 * SQLite-backed local cache — this app has no offline requirement (research.md R9). Session
 * display data (userId/email/role/organisationSlug — never the refresh token itself, which
 * stays in its own SecureStore key) is persisted as a single JSON blob in SecureStore purely so
 * tryRestoreSession can show a name before the network round-trip completes; nothing here is a
 * secret beyond the refresh token.
 */
import * as SecureStore from "expo-secure-store";
import { apiClient, configureApiBaseUrl, setUnauthorizedHandler } from "./apiClient";
import { useStore } from "../store/useStore";
import { registerPushToken } from "./pushToken";
import type { AuthResponse } from "../types";

const REFRESH_TOKEN_KEY = "childcareparent_refresh_token";
const SESSION_DISPLAY_KEY = "childcareparent_session_display";

interface SessionDisplay {
  userId:           string;
  email:            string;
  role:             string;
  organisationSlug: string;
}

const getStoredRefreshToken    = () => SecureStore.getItemAsync(REFRESH_TOKEN_KEY);
const storeRefreshToken        = (t: string) => SecureStore.setItemAsync(REFRESH_TOKEN_KEY, t);
const deleteStoredRefreshToken = () => SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY);

async function storeSessionDisplay(display: SessionDisplay): Promise<void> {
  await SecureStore.setItemAsync(SESSION_DISPLAY_KEY, JSON.stringify(display));
}

async function getStoredSessionDisplay(): Promise<SessionDisplay | null> {
  const raw = await SecureStore.getItemAsync(SESSION_DISPLAY_KEY);
  return raw ? (JSON.parse(raw) as SessionDisplay) : null;
}

async function deleteStoredSessionDisplay(): Promise<void> {
  await SecureStore.deleteItemAsync(SESSION_DISPLAY_KEY);
}

async function applyAuthResponse(data: AuthResponse, organisationSlug: string): Promise<void> {
  await storeSessionDisplay({
    userId: data.user.id,
    email: data.user.email,
    role: data.user.role,
    organisationSlug,
  });

  useStore.getState().setAuth({
    userId:           data.user.id,
    email:            data.user.email,
    role:             data.user.role,
    organisationSlug,
    accessToken:      data.accessToken,
  });

  // Best-effort — a parent without push capability still has the in-app notification centre
  // (User Story 4's fallback path). Never blocks or fails the sign-in itself.
  registerPushToken().catch(() => {});
}

function errorKeyFrom(result: { error?: unknown }): string {
  return (result.error as { errorKey?: string } | undefined)?.errorKey ?? "errors.auth.invalid_credentials";
}

export async function login(baseUrl: string, organisationSlug: string, email: string, password: string): Promise<void> {
  configureApiBaseUrl(baseUrl);

  let result;
  try {
    result = await apiClient.POST("/api/auth/login", {
      body: { organisationSlug, email, password },
    });
  } catch {
    throw new Error("NETWORK_ERROR");
  }

  if (!result.response.ok) throw new Error(errorKeyFrom(result));

  const data = result.data as unknown as AuthResponse;
  await storeRefreshToken(data.refreshToken);
  await applyAuthResponse(data, organisationSlug);
}

export async function loginWithGoogle(baseUrl: string, organisationSlug: string, idToken: string): Promise<void> {
  configureApiBaseUrl(baseUrl);

  let result;
  try {
    result = await apiClient.POST("/api/auth/google", {
      body: { organisationSlug, idToken },
    });
  } catch {
    throw new Error("NETWORK_ERROR");
  }

  if (!result.response.ok) throw new Error(errorKeyFrom(result));

  const data = result.data as unknown as AuthResponse;
  await storeRefreshToken(data.refreshToken);
  await applyAuthResponse(data, organisationSlug);
}

export async function loginWithApple(
  baseUrl: string, organisationSlug: string, identityToken: string, email?: string,
): Promise<void> {
  configureApiBaseUrl(baseUrl);

  let result;
  try {
    result = await apiClient.POST("/api/auth/apple", {
      body: { organisationSlug, identityToken, email: email ?? null },
    });
  } catch {
    throw new Error("NETWORK_ERROR");
  }

  if (!result.response.ok) throw new Error(errorKeyFrom(result));

  const data = result.data as unknown as AuthResponse;
  await storeRefreshToken(data.refreshToken);
  await applyAuthResponse(data, organisationSlug);
}

/**
 * Attempts a silent refresh. Returns true on success. On an explicit rejection (401 — session
 * can no longer be renewed) this also performs a full clean sign-out and returns false. On a
 * network error (server unreachable) it returns false WITHOUT clearing anything.
 */
export async function refresh(): Promise<boolean> {
  const refreshToken = await getStoredRefreshToken();
  const display = await getStoredSessionDisplay();
  if (!refreshToken || !display) return false;

  try {
    const result = await apiClient.POST("/api/auth/refresh", {
      body: { organisationSlug: display.organisationSlug, refreshToken },
    });

    if (!result.response.ok) {
      await signOutCleanly();
      return false;
    }

    const data = result.data as unknown as AuthResponse;
    await storeRefreshToken(data.refreshToken);
    await applyAuthResponse(data, display.organisationSlug);
    return true;
  } catch {
    return false; // network error — leave the stored session intact
  }
}

setUnauthorizedHandler(refresh);

export async function logout(): Promise<void> {
  try {
    const accessToken = useStore.getState().auth?.accessToken;
    const refreshToken = await getStoredRefreshToken();
    if (refreshToken) {
      await apiClient.POST("/api/auth/logout", {
        body: { refreshToken },
        headers: { Authorization: `Bearer ${accessToken}` },
      });
    }
  } catch {
    /* best-effort revocation */
  }

  await signOutCleanly();
}

async function signOutCleanly(): Promise<void> {
  await deleteStoredRefreshToken();
  await deleteStoredSessionDisplay();
  useStore.getState().resetAuth();
}

/**
 * Called on app startup. Restores the session via a server refresh (this app has no offline
 * requirement, unlike mobile/ — research.md R9 — so there is no offline-cached-identity
 * fallback here; either the refresh succeeds or the parent sees the login screen).
 */
export async function tryRestoreSession(baseUrl: string): Promise<boolean> {
  configureApiBaseUrl(baseUrl);

  const refreshToken = await getStoredRefreshToken();
  if (!refreshToken) return false;

  // refresh() -> applyAuthResponse() already re-registers the push token on success.
  return refresh();
}
