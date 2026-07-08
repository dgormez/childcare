/**
 * auth.ts — login, silent refresh, logout, and session restoration against the real
 * feature-003 auth contract (organisationSlug + email + password; AuthSessionResponse
 * carries a `role` claim — research.md R3).
 *
 * All functions update SecureStore (refresh token only — FR-003), the plain config table
 * (organisationSlug/email/role, for offline session-restore display only, never secrets),
 * and the Zustand auth slice as a single operation so none of the three ever diverge.
 */
import * as SecureStore from "expo-secure-store";
import { apiClient, configureApiBaseUrl, setUnauthorizedHandler } from "./apiClient";
import { getConfigValue, setConfigValue, deleteLocalTenantData } from "./localDb";
import { useStore } from "../store/useStore";
import type { AuthResponse, StaffMeResponse } from "../types";

const REFRESH_TOKEN_KEY = "childcare_refresh_token";

const getStoredRefreshToken    = () => SecureStore.getItemAsync(REFRESH_TOKEN_KEY);
const storeRefreshToken        = (t: string) => SecureStore.setItemAsync(REFRESH_TOKEN_KEY, t);
const deleteStoredRefreshToken = () => SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY);

function applyAuthResponse(data: AuthResponse, organisationSlug: string) {
  setConfigValue("userId", data.user.id);
  setConfigValue("userEmail", data.user.email);
  setConfigValue("userRole", data.user.role);
  setConfigValue("organisationSlug", organisationSlug);

  useStore.getState().setAuth({
    userId:           data.user.id,
    email:            data.user.email,
    role:             data.user.role,
    organisationSlug,
    accessToken:      data.accessToken,
  });
}

/** FR-030: populate staffProfileId/eligibleLocationIds for display — never blocks login on
 * failure, since a caregiver whose StaffProfile lookup fails should still be able to use the
 * app (this is a display nicety, not required for correct scoping, which the server enforces
 * regardless — research.md R6). */
async function fetchAndApplyStaffMe(): Promise<void> {
  try {
    const result = await apiClient.GET("/api/staff/me");
    if (result.response.ok) {
      const me = result.data as unknown as StaffMeResponse;
      const { auth } = useStore.getState();
      if (auth) {
        useStore.getState().setAuth({
          ...auth,
          staffProfileId: me.staffProfileId,
          eligibleLocationIds: me.eligibleLocationIds,
        });
      }
    }
  } catch {
    /* display-only — ignore */
  }
}

export async function login(baseUrl: string, organisationSlug: string, email: string, password: string): Promise<void> {
  configureApiBaseUrl(baseUrl);

  let result;
  try {
    result = await apiClient.POST("/api/auth/login", {
      body: { organisationSlug, email, password },
    });
  } catch {
    // Brand-new install / first launch offline: there is no cached session to fall back to,
    // and authentication itself cannot happen without a network round-trip.
    throw new Error("NETWORK_ERROR");
  }

  if (!result.response.ok) {
    // openapi-fetch already consumed the response body to populate result.data/result.error —
    // re-reading result.response.json() here would throw ("body stream already read"), which
    // this catch previously masked as a plain "invalid credentials" for every failure reason,
    // including ones that had nothing to do with credentials (found on-device: a real 200
    // success was being misreported as invalid credentials for the same underlying reason).
    const errorKey = (result.error as { errorKey?: string } | undefined)?.errorKey ?? "errors.auth.invalid_credentials";
    throw new Error(errorKey);
  }

  const data = result.data as unknown as AuthResponse;
  await storeRefreshToken(data.refreshToken);
  applyAuthResponse(data, organisationSlug);
  await fetchAndApplyStaffMe();
}

/**
 * Attempts a silent refresh. Returns true on success. On an explicit rejection (401 — session
 * can no longer be renewed, FR-006, whether due to deactivation or natural expiry) this also
 * performs a full clean sign-out and returns false. On a network error (server unreachable)
 * it returns false WITHOUT clearing anything, since the session may still be valid once back
 * online — the caller decides what to do (tryRestoreSession falls back to offline-cached
 * identity; the apiClient 401-retry path simply gives up on that one request).
 */
export async function refresh(): Promise<boolean> {
  const refreshToken = await getStoredRefreshToken();
  const organisationSlug = getConfigValue("organisationSlug");
  if (!refreshToken || !organisationSlug) return false;

  try {
    const result = await apiClient.POST("/api/auth/refresh", {
      body: { organisationSlug, refreshToken },
    });

    if (!result.response.ok) {
      // FR-006: explicit rejection (deactivated account or refresh token expired/invalid) —
      // clean sign-out now rather than looping on retry.
      await signOutCleanly();
      return false;
    }

    const data = result.data as unknown as AuthResponse;
    await storeRefreshToken(data.refreshToken);
    applyAuthResponse(data, organisationSlug);
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

/** FR-005/FR-006/FR-019: clears SecureStore, tenant-scoped local cache/queue, and auth state —
 * shared by explicit logout and the "session can no longer be renewed" path. */
async function signOutCleanly(): Promise<void> {
  const tenantId = getConfigValue("organisationSlug");
  await deleteStoredRefreshToken();
  if (tenantId) deleteLocalTenantData(tenantId);
  useStore.getState().resetAuth();
}

/**
 * Called on app startup. Attempts to restore session in priority order:
 *
 * 1. Server refresh (online) — get a fresh access token and re-populate staff/me.
 * 2. Cached local identity (offline) — use the last known userId/email/role/org, no access
 *    token. API calls fail until back online; the caregiver still sees their own name.
 *
 * Returns true if a session was restored (either online or offline).
 */
export async function tryRestoreSession(baseUrl: string): Promise<boolean> {
  configureApiBaseUrl(baseUrl);

  const refreshToken = await getStoredRefreshToken();
  if (!refreshToken) return false;

  const refreshed = await refresh();
  if (refreshed) {
    await fetchAndApplyStaffMe();
    return true;
  }

  // Either explicitly rejected (signOutCleanly already ran, nothing to restore) or a network
  // error — fall back to offline-cached identity only in the network-error case.
  const userId = getConfigValue("userId");
  const userEmail = getConfigValue("userEmail");
  const userRole = getConfigValue("userRole");
  const organisationSlug = getConfigValue("organisationSlug");

  if (userId && userEmail && userRole && organisationSlug) {
    useStore.getState().setAuth({
      userId, email: userEmail, role: userRole, organisationSlug, accessToken: "",
    });
    return true;
  }

  return false;
}
