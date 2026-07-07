/**
 * apiClient.ts — typed HTTP client (openapi-fetch, research.md R2).
 *
 * Note on response typing: the backend's OpenAPI document doesn't yet declare response body
 * schemas for any endpoint (no `.Produces<T>()` on any Minimal API route across this
 * codebase — discovered during this feature's implementation, not specific to feature 008).
 * Request bodies and query parameters ARE fully typed from the generated `paths`. Callers
 * cast the parsed JSON to the corresponding type from `types/index.ts` after a successful
 * call — a normal pattern when an OpenAPI spec under-declares responses, not a defect here.
 */
import createClient, { type Middleware } from "openapi-fetch";
import type { paths } from "./generated/api-types";
import { useStore } from "../store/useStore";

let baseUrl = "";

export function configureApiBaseUrl(url: string) {
  baseUrl = url;
}

export function getApiBaseUrl(): string {
  return baseUrl;
}

/**
 * Registered by auth.ts to avoid a circular import — apiClient needs to trigger a refresh on
 * 401, but the refresh logic itself needs apiClient to make the refresh call.
 */
let unauthorizedHandler: (() => Promise<boolean>) | null = null;

export function setUnauthorizedHandler(handler: () => Promise<boolean>) {
  unauthorizedHandler = handler;
}

// /api/auth/* calls (login/refresh/logout) never go through the auto-retry path below — a
// 401 from /api/auth/refresh itself would otherwise call unauthorizedHandler (== refresh)
// again, recursing forever. Found while wiring this middleware, not a downstream defect.
function isAuthEndpoint(url: string): boolean {
  return url.includes("/api/auth/");
}

// openapi-fetch's own `baseUrl` is fixed at client-creation time (a plain string, not a
// getter — createClient({ baseUrl: () => baseUrl }) type-checks as an error and was silently
// wrong at runtime too, since it's read once, before configureApiBaseUrl() has ever run).
// The client is created against this placeholder origin instead, and onRequest below rewrites
// every request to the real, dynamically-configurable base URL.
const PLACEHOLDER_ORIGIN = "http://api.invalid";

const authMiddleware: Middleware = {
  async onRequest({ request }) {
    const token = useStore.getState().auth?.accessToken;
    if (token) request.headers.set("Authorization", `Bearer ${token}`);

    if (baseUrl && request.url.startsWith(PLACEHOLDER_ORIGIN)) {
      return new Request(request.url.replace(PLACEHOLDER_ORIGIN, baseUrl), request);
    }
    return request;
  },

  // FR-004/FR-006: on 401, refresh once and transparently retry the original request once —
  // the caller never sees the interruption. If refresh fails, the original 401 is returned
  // as-is (auth.ts's refresh() itself already performed the clean sign-out in that case).
  async onResponse({ request, response }) {
    if (response.status !== 401 || isAuthEndpoint(request.url) || !unauthorizedHandler) {
      return response;
    }

    const refreshed = await unauthorizedHandler();
    if (!refreshed) return response;

    const retryRequest = request.clone();
    const token = useStore.getState().auth?.accessToken;
    if (token) retryRequest.headers.set("Authorization", `Bearer ${token}`);
    return fetch(retryRequest);
  },
};

export const apiClient = createClient<paths>({
  baseUrl: PLACEHOLDER_ORIGIN,
});

apiClient.use(authMiddleware);
