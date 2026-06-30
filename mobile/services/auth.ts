/**
 * auth.ts — Register, login, logout, and session restoration.
 *
 * All functions update both SecureStore (refresh token) and the Zustand store
 * (auth state) as a single operation so the two never diverge.
 */
import {
  configureApi,
  setAccessToken,
  clearAccessToken,
  getStoredRefreshToken,
  storeRefreshToken,
  deleteStoredRefreshToken,
} from "./api";
import { useStore } from "../store/useStore";
import { getConfigValue, setConfigValue, deleteConfigValue } from "./localDb";
import type { AuthResponse } from "../types";

// ── Helpers ───────────────────────────────────────────────────────────────────

export function applyAuthResponse(baseUrl: string, data: AuthResponse) {
  configureApi(baseUrl);
  setAccessToken(data.accessToken);

  // Persist userId + email for offline session restoration
  setConfigValue("userId",    data.user.id);
  setConfigValue("userEmail", data.user.email);

  useStore.getState().setAuth({
    userId:      data.user.id,
    email:       data.user.email,
    accessToken: data.accessToken,
  });
}

// ── Public API ────────────────────────────────────────────────────────────────

export async function register(baseUrl: string, email: string, password: string): Promise<void> {
  const res = await fetch(`${baseUrl}/api/auth/register`, {
    method:  "POST",
    headers: { "Content-Type": "application/json" },
    body:    JSON.stringify({ email, password }),
  });

  if (!res.ok) {
    const data = await res.json().catch(() => ({}));
    throw new Error((data as any).error ?? `Registration failed (${res.status})`);
  }

  const data: AuthResponse = await res.json();
  await storeRefreshToken(data.refreshToken);
  applyAuthResponse(baseUrl, data);
}

export async function login(baseUrl: string, email: string, password: string): Promise<void> {
  const res = await fetch(`${baseUrl}/api/auth/login`, {
    method:  "POST",
    headers: { "Content-Type": "application/json" },
    body:    JSON.stringify({ email, password }),
  });

  if (!res.ok) throw new Error("Invalid email or password.");

  const data: AuthResponse = await res.json();
  await storeRefreshToken(data.refreshToken);
  applyAuthResponse(baseUrl, data);
}

export async function logout(baseUrl: string): Promise<void> {
  // Best-effort server-side revocation — sends the refresh token to invalidate this device's session
  try {
    const accessToken  = useStore.getState().auth?.accessToken;
    const refreshToken = await getStoredRefreshToken();
    if (refreshToken) {
      await fetch(`${baseUrl}/api/auth/logout`, {
        method:  "POST",
        headers: { "Content-Type": "application/json", Authorization: `Bearer ${accessToken}` },
        body:    JSON.stringify({ refreshToken }),
      });
    }
  } catch { /* ignore */ }

  await deleteStoredRefreshToken();
  clearAccessToken();
  deleteConfigValue("userId");
  deleteConfigValue("userEmail");
  useStore.getState().resetAuth();
}

/**
 * Called on app startup. Attempts to restore session in priority order:
 *
 * 1. Server refresh (online) — get a fresh access token.
 * 2. Cached local auth (offline) — use the last known userId/email, no access
 *    token. Sync will silently fail until the device comes back online.
 *
 * Returns true if a session was restored (either online or offline).
 */
export async function tryRestoreSession(baseUrl: string): Promise<boolean> {
  const refreshToken = await getStoredRefreshToken();
  if (!refreshToken) return false;

  configureApi(baseUrl);

  try {
    const res = await fetch(`${baseUrl}/api/auth/refresh`, {
      method:  "POST",
      headers: { "Content-Type": "application/json" },
      body:    JSON.stringify({ refreshToken }),
    });

    if (!res.ok) {
      // Token is invalid/expired — user must log in again
      await deleteStoredRefreshToken();
      deleteConfigValue("userId");
      deleteConfigValue("userEmail");
      return false;
    }

    const data: AuthResponse = await res.json();
    await storeRefreshToken(data.refreshToken);
    applyAuthResponse(baseUrl, data);
    return true;
  } catch {
    // Network error — try to restore from local cache so the app works offline
    const userId    = getConfigValue("userId");
    const userEmail = getConfigValue("userEmail");

    if (userId && userEmail) {
      setAccessToken(""); // no token — API calls will fail until back online
      useStore.getState().setAuth({ userId, email: userEmail, accessToken: "" });
      return true;
    }

    return false;
  }
}
