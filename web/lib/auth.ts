"use client";
import { login as apiLogin, register as apiRegister, setAccessToken, clearAccessToken } from "./api";
import type { AuthResponse, User } from "./types";

export interface Session {
  user:        User;
  accessToken: string;
}

let _session: Session | null = null;

export function getSession(): Session | null { return _session; }

function setSession(res: AuthResponse) {
  _session = { user: res.user, accessToken: res.accessToken };
  setAccessToken(res.accessToken);
}

export async function login(email: string, password: string): Promise<Session> {
  const res = await apiLogin(email, password);
  await storeRefreshToken(res.refreshToken);
  setSession(res);
  return _session!;
}

export async function register(email: string, password: string): Promise<Session> {
  const res = await apiRegister(email, password);
  await storeRefreshToken(res.refreshToken);
  setSession(res);
  return _session!;
}

export async function logout() {
  // BFF route reads the httpOnly cookie, revokes the token server-side, then clears the cookie
  try { await fetch("/api/logout", { method: "POST" }); } catch { /* best effort */ }
  clearAccessToken();
  _session = null;
}

/** Called on app mount to restore the session from the httpOnly cookie. */
export async function tryRestoreSession(): Promise<Session | null> {
  try {
    const res = await fetch("/api/refresh", { method: "POST" });
    if (!res.ok) return null;
    const data: AuthResponse = await res.json();
    setSession(data);
    return _session!;
  } catch {
    return null;
  }
}

// ── BFF helpers (call our own Next.js route handlers) ────────────────────────

async function storeRefreshToken(token: string) {
  await fetch("/api/set-refresh-token", {
    method:  "POST",
    headers: { "Content-Type": "application/json" },
    body:    JSON.stringify({ refreshToken: token }),
  });
}

