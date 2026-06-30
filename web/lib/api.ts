/**
 * api.ts — HTTP client for the web app.
 *
 * Auth:
 *   - Access token: in-memory (module-level variable)
 *   - Refresh token: httpOnly cookie managed via /api/refresh route handler (BFF)
 *
 * On 401: calls /api/refresh to get a new access token, then retries once.
 */
import type { Habit, HabitCompletion, AuthResponse, SubscriptionStatus } from "./types";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

let _accessToken = "";

export const setAccessToken  = (t: string) => { _accessToken = t; };
export const clearAccessToken = ()         => { _accessToken = ""; };
export const getAccessToken  = ()          => _accessToken;

// ── Token refresh via BFF route handler ───────────────────────────────────────

async function tryRefresh(): Promise<string | null> {
  try {
    const res = await fetch("/api/refresh", { method: "POST" });
    if (!res.ok) return null;
    const { accessToken } = await res.json();
    _accessToken = accessToken;
    return accessToken;
  } catch {
    return null;
  }
}

// ── Core request ──────────────────────────────────────────────────────────────

async function request<T>(
  method:  string,
  path:    string,
  body?:   unknown,
  isRetry = false,
): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
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
    throw new Error("SESSION_EXPIRED");
  }

  if (!res.ok) {
    const text = await res.text().catch(() => "");
    const err = new Error(`[${res.status}] ${text || res.statusText}`) as Error & { status: number };
    err.status = res.status;
    throw err;
  }

  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

// ── Auth ──────────────────────────────────────────────────────────────────────

export const login = (email: string, password: string): Promise<AuthResponse> =>
  request("POST", "/api/auth/login", { email, password });

export const register = (email: string, password: string): Promise<AuthResponse> =>
  request("POST", "/api/auth/register", { email, password });

export const forgotPassword = (email: string): Promise<void> =>
  request("POST", "/api/auth/forgot-password", { email });

export const resetPassword = (token: string, newPassword: string): Promise<void> =>
  request("POST", "/api/auth/reset-password", { token, newPassword });

export const verifyEmail = (token: string): Promise<void> =>
  request("POST", "/api/auth/verify-email", { token });

export const resendVerification = (): Promise<void> =>
  request("POST", "/api/auth/resend-verification");

export const deleteAccount = (): Promise<void> =>
  request("DELETE", "/api/auth/account");

// ── Habits ────────────────────────────────────────────────────────────────────

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
  request("POST", `/api/habits/${id}/complete`, { date });

export const uncompleteHabit = (id: string, date: string): Promise<void> =>
  request("DELETE", `/api/habits/${id}/complete`, { date });

// ── Payments ──────────────────────────────────────────────────────────────────

export const getSubscriptionStatus = (): Promise<SubscriptionStatus> =>
  request("GET", "/api/payments/status");

export const createCheckoutSession = (successUrl: string, cancelUrl: string): Promise<{ url: string }> =>
  request("POST", "/api/payments/checkout", { successUrl, cancelUrl });

export const createPortalSession = (returnUrl: string): Promise<{ url: string }> =>
  request("POST", "/api/payments/portal", { returnUrl });

// ── Health ────────────────────────────────────────────────────────────────────

export const checkHealth = async (): Promise<boolean> => {
  try { return (await fetch(`${API_BASE}/health`)).ok; } catch { return false; }
};
