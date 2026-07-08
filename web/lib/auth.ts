"use client";
/**
 * auth.ts — director login (email/password or Google), silent refresh, logout, and session
 * restoration against feature 003's contract (organisationSlug + email/password;
 * AuthSessionResponse carries a role claim). Rebuilt on apiClient.ts (research.md R1) —
 * replaces the Habits-era raw-fetch version. Also fetches the organisation's display name
 * (feature 007a, spec FR-005a) once a session is established, since the sidebar shell needs it
 * and no auth response includes it.
 */
import { apiClient, setAccessToken, clearAccessToken, setUnauthorizedHandler } from "./apiClient";
import type { AuthResponse, OrganisationResponse } from "./types";

export interface Session {
  user: AuthResponse["user"];
  accessToken: string;
  organisationSlug: string;
  organisationName: string;
}

const ORG_SLUG_KEY = "childcare_organisation_slug";

function storeOrganisationSlug(slug: string) {
  if (typeof localStorage !== "undefined") localStorage.setItem(ORG_SLUG_KEY, slug);
}

function getStoredOrganisationSlug(): string | null {
  if (typeof localStorage === "undefined") return null;
  return localStorage.getItem(ORG_SLUG_KEY);
}

async function fetchOrganisationName(): Promise<string> {
  const result = await apiClient.GET("/api/organisations/me");
  if (!result.response.ok) return "";
  return (result.data as unknown as OrganisationResponse).name;
}

async function storeRefreshToken(refreshToken: string, organisationSlug: string) {
  await fetch("/api/set-refresh-token", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken, organisationSlug }),
  });
}

async function buildSession(data: AuthResponse, organisationSlug: string): Promise<Session> {
  setAccessToken(data.accessToken);
  // Fall back to the slug (always known, never empty) if the name fetch fails — spec.md Edge
  // Cases: the shell must show sensible fallback text, never blank space.
  const organisationName = (await fetchOrganisationName()) || organisationSlug;
  return { user: data.user, accessToken: data.accessToken, organisationSlug, organisationName };
}

export async function login(organisationSlug: string, email: string, password: string): Promise<Session> {
  const result = await apiClient.POST("/api/auth/login", {
    body: { organisationSlug, email, password },
  });

  if (!result.response.ok) {
    const errorKey = (result.error as { errorKey?: string } | undefined)?.errorKey ?? "errors.auth.invalid_credentials";
    const err = new Error(errorKey) as Error & { status?: number };
    err.status = result.response.status;
    throw err;
  }

  const data = result.data as unknown as AuthResponse;
  await storeRefreshToken(data.refreshToken, organisationSlug);
  storeOrganisationSlug(organisationSlug);
  return buildSession(data, organisationSlug);
}

/** Called by GoogleSignInButton.tsx after it has already exchanged the Google credential for an
 * AuthSessionResponse — session bookkeeping (refresh-token storage, org name fetch) is
 * centralized here rather than duplicated in the button component. */
export async function completeGoogleSignIn(data: AuthResponse, organisationSlug: string): Promise<Session> {
  await storeRefreshToken(data.refreshToken, organisationSlug);
  storeOrganisationSlug(organisationSlug);
  return buildSession(data, organisationSlug);
}

/** Registered by AuthProvider.tsx — fires when a refresh is explicitly rejected (401, not a
 * network error) while a session was active, so the UI can show a clear message before
 * redirecting to /login (spec Edge Cases, SC-006), rather than silently bouncing the director. */
let sessionExpiredHandler: (() => void) | null = null;

export function setSessionExpiredHandler(handler: () => void) {
  sessionExpiredHandler = handler;
}

/** Silent refresh via the /api/refresh BFF route (httpOnly cookie, never touched directly by
 * client code). Returns true on success — registered as apiClient's 401 handler below. */
export async function refresh(): Promise<boolean> {
  try {
    const res = await fetch("/api/refresh", { method: "POST" });
    if (!res.ok) {
      if (res.status === 401) sessionExpiredHandler?.();
      return false;
    }
    const data: AuthResponse = await res.json();
    setAccessToken(data.accessToken);
    return true;
  } catch {
    return false; // network error — leave the session as-is, don't force a sign-out
  }
}

setUnauthorizedHandler(refresh);

export async function logout(): Promise<void> {
  try {
    await fetch("/api/logout", { method: "POST" });
  } catch {
    /* best-effort revocation */
  }
  clearAccessToken();
  if (typeof localStorage !== "undefined") localStorage.removeItem(ORG_SLUG_KEY);
}

/** Called on app mount to restore the session from the httpOnly refresh-token cookie. */
export async function tryRestoreSession(): Promise<Session | null> {
  const organisationSlug = getStoredOrganisationSlug();
  if (!organisationSlug) return null;

  try {
    const res = await fetch("/api/refresh", { method: "POST" });
    if (!res.ok) return null;
    const data: AuthResponse = await res.json();
    return buildSession(data, organisationSlug);
  } catch {
    return null;
  }
}
