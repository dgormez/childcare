/**
 * apiClient.ts — typed HTTP client (openapi-fetch), mirrors mobile/services/apiClient.ts.
 *
 * Note on response typing: the backend's OpenAPI document doesn't yet declare response body
 * schemas for any endpoint (no `.Produces<T>()` on any Minimal API route across this
 * codebase — confirmed while building feature 008, not specific to this feature). Request
 * bodies and query parameters ARE fully typed from the generated `paths`. Callers cast the
 * parsed JSON to the corresponding type from `types/index.ts` after a successful call — a
 * normal pattern when an OpenAPI spec under-declares responses, not a defect here.
 *
 * Unlike mobile/, there is no device-token concept here — parent-mobile only ever carries a
 * user access token (personal login, no kiosk pairing).
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

// /api/auth/* calls (login/refresh/logout/google/apple) never go through the auto-retry path
// below — a 401 from /api/auth/refresh itself would otherwise call unauthorizedHandler (==
// refresh) again, recursing forever.
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
      // `new Request(url, request)` — passing a Request object as `init` — does not reliably
      // carry the body over in React Native's fetch implementation (confirmed on-device during
      // feature 008a: every POST landed at the server with a completely empty body, while the
      // identical code path in Jest's Node-based fetch worked fine, which is why the equivalent
      // test in mobile/ only ever covered a bodyless GET and never caught it).
      //
      // First attempt at a fix gated the body-read on `request.body` (the ReadableStream
      // getter) — that getter isn't reliably implemented in RN's fetch polyfill either, so it
      // read as falsy even when a body genuinely existed, silently reproducing the same bug
      // through a different path. Always reading via `.text()` instead — which every fetch
      // implementation (browser, Node, RN) implements correctly regardless of the raw `.body`
      // stream property — sidesteps that unreliable getter entirely. `.text()` on a bodyless
      // request just resolves to `""`, which is safe to omit.
      const newUrl = request.url.replace(PLACEHOLDER_ORIGIN, baseUrl);
      const text = await request.text();
      return new Request(newUrl, {
        method: request.method,
        headers: request.headers,
        body: text || undefined,
      });
    }
    return request;
  },

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
