/**
 * api.ts — HTTP client with automatic JWT refresh.
 *
 * Usage:
 *   1. Call configureApi(baseUrl) on app startup.
 *   2. After login/register, call setAccessToken(token).
 *   3. All requests automatically include Authorization: Bearer <token>.
 *   4. On 401, the client attempts one token refresh before throwing.
 */
import * as SecureStore from "expo-secure-store";
import { Habit, HabitCompletion, AuthResponse } from "../types";

// ── Config ────────────────────────────────────────────────────────────────────

const REFRESH_TOKEN_KEY = "childcare_refresh_token";

let _baseUrl     = "";
let _accessToken = "";

export const configureApi    = (baseUrl: string) => { _baseUrl = baseUrl.replace(/\/$/, ""); };
export const setAccessToken  = (token: string)   => { _accessToken = token; };
export const clearAccessToken = ()               => { _accessToken = ""; };

// ── SecureStore helpers ───────────────────────────────────────────────────────

export const getStoredRefreshToken    = () => SecureStore.getItemAsync(REFRESH_TOKEN_KEY);
export const storeRefreshToken        = (t: string) => SecureStore.setItemAsync(REFRESH_TOKEN_KEY, t);
export const deleteStoredRefreshToken = () => SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY);

// ── Token refresh ─────────────────────────────────────────────────────────────

/** Attempts a silent token refresh. Returns the new access token on success. */
async function tryRefresh(): Promise<string | null> {
  const refreshToken = await getStoredRefreshToken();
  if (!refreshToken) return null;

  try {
    const res = await fetch(`${_baseUrl}/api/auth/refresh`, {
      method:  "POST",
      headers: { "Content-Type": "application/json" },
      body:    JSON.stringify({ refreshToken }),
    });

    if (!res.ok) {
      // Refresh token is invalid or expired — force re-login
      await deleteStoredRefreshToken();
      return null;
    }

    const data: AuthResponse = await res.json();
    _accessToken = data.accessToken;
    await storeRefreshToken(data.refreshToken);

    // Notify the store so the in-memory auth state stays current
    const { useStore } = await import("../store/useStore");
    useStore.getState().updateAccessToken(data.accessToken);

    return data.accessToken;
  } catch {
    return null; // network error — don't clear the refresh token
  }
}

// ── Core request ──────────────────────────────────────────────────────────────

async function request<T>(
  method:  string,
  path:    string,
  body?:   unknown,
  isRetry = false,
): Promise<T> {
  const url = `${_baseUrl}${path}`;
  const res = await fetch(url, {
    method,
    headers: {
      "Content-Type": "application/json",
      ...(_accessToken ? { Authorization: `Bearer ${_accessToken}` } : {}),
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });

  if (res.status === 401 && !isRetry) {
    const newToken = await tryRefresh();
    if (newToken) return request<T>(method, path, body, true);
    // Refresh failed — bubble up a sentinel so the app can redirect to login
    throw new Error("SESSION_EXPIRED");
  }

  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new Error(`[${res.status}] ${text || res.statusText}`);
  }

  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

// ── Habits API ────────────────────────────────────────────────────────────────

export const fetchHabits = (): Promise<Habit[]> =>
  request("GET", "/api/habits");

export const fetchCompletions = (from: string, to: string): Promise<HabitCompletion[]> =>
  request("GET", `/api/habits/completions?from=${from}&to=${to}`);

export const createHabit = (name: string, color: string, icon: string): Promise<Habit> =>
  request("POST", "/api/habits", { name, color, icon });

export const updateHabit = (id: string, name: string, color: string, icon: string): Promise<Habit> =>
  request("PUT", `/api/habits/${id}`, { name, color, icon });

export const deleteHabit = (id: string): Promise<void> =>
  request("DELETE", `/api/habits/${id}`);

export const completeHabit = (id: string, date: string): Promise<HabitCompletion> =>
  request("POST", `/api/habits/${id}/complete?date=${date}`);

export const uncompleteHabit = (id: string, date: string): Promise<void> =>
  request("DELETE", `/api/habits/${id}/complete?date=${date}`);

// ── Auth actions ─────────────────────────────────────────────────────────────

export const verifyEmail = (token: string): Promise<void> =>
  request("POST", "/api/auth/verify-email", { token });

export const resendVerification = (): Promise<void> =>
  request("POST", "/api/auth/resend-verification");

export const deleteAccount = (): Promise<void> =>
  request("DELETE", "/api/auth/account");

// ── Notifications ─────────────────────────────────────────────────────────────

export const registerPushToken = (token: string): Promise<void> =>
  request("POST", "/api/notifications/push-token", { token });

// ── Payments ──────────────────────────────────────────────────────────────────

export type SubscriptionStatus = {
  status: string;
  isActive: boolean;
  currentPeriodEnd: string | null;
};

export const getSubscriptionStatus = (): Promise<SubscriptionStatus> =>
  request("GET", "/api/payments/status");

export const createCheckoutSession = (): Promise<{ url: string }> =>
  request("POST", "/api/payments/checkout");

export const createPortalSession = (): Promise<{ url: string }> =>
  request("POST", "/api/payments/portal");

// ── Health ────────────────────────────────────────────────────────────────────

export const checkHealth = async (): Promise<boolean> => {
  try { return (await fetch(`${_baseUrl}/health`)).ok; } catch { return false; }
};
