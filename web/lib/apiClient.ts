/**
 * apiClient.ts — typed HTTP client (openapi-fetch), replacing the Habits-era hand-written
 * fetch wrapper in the old lib/api.ts (research.md R1). Mirrors
 * mobile/services/apiClient.ts's 401-refresh-and-retry shape, minus the placeholder-origin
 * rewrite mobile needs — Next.js's NEXT_PUBLIC_API_BASE_URL is a build-time env var, so the
 * real base URL is known at client-creation time here.
 *
 * Auth: access token lives in-memory (module-level, like the old lib/api.ts); refresh token
 * lives in an httpOnly cookie managed by the existing /api/refresh BFF route handler.
 */
import createClient, { type Middleware } from "openapi-fetch";
import type { paths } from "./generated/api-types";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

let _accessToken = "";

export const setAccessToken = (t: string) => {
  _accessToken = t;
};
export const clearAccessToken = () => {
  _accessToken = "";
};
export const getAccessToken = () => _accessToken;

/** Registered by auth.ts to avoid a circular import — apiClient needs to trigger a refresh on
 * 401, but the refresh logic itself needs apiClient to make the refresh call. */
let unauthorizedHandler: (() => Promise<boolean>) | null = null;

export function setUnauthorizedHandler(handler: () => Promise<boolean>) {
  unauthorizedHandler = handler;
}

// /api/auth/* calls (login/refresh/Google) never go through the auto-retry path below — a 401
// from /api/auth/refresh itself would otherwise call unauthorizedHandler (== refresh) again,
// recursing forever (mirrors mobile/services/apiClient.ts's identical guard).
function isAuthEndpoint(url: string): boolean {
  return url.includes("/api/auth/");
}

const authMiddleware: Middleware = {
  async onRequest({ request }) {
    if (_accessToken) request.headers.set("Authorization", `Bearer ${_accessToken}`);
    return request;
  },

  async onResponse({ request, response }) {
    if (response.status !== 401 || isAuthEndpoint(request.url) || !unauthorizedHandler) {
      return response;
    }

    const refreshed = await unauthorizedHandler();
    if (!refreshed) return response;

    const retryRequest = request.clone();
    if (_accessToken) retryRequest.headers.set("Authorization", `Bearer ${_accessToken}`);
    return fetch(retryRequest);
  },
};

export const apiClient = createClient<paths>({ baseUrl: API_BASE });
apiClient.use(authMiddleware);
