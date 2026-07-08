import { apiClient, configureApiBaseUrl, setUnauthorizedHandler } from "../../services/apiClient";
import { useStore } from "../../store/useStore";
import { getDeviceToken, deleteStoredDeviceToken } from "../../services/deviceTokenStorage";

// openapi-fetch resolves its default fetch implementation once, at client-creation time
// (`fetch: baseFetch = globalThis.fetch`) — reassigning `global.fetch` afterwards has no
// effect on it. Passing `fetch: mockFetch` as a per-call option overrides it for that call;
// assigning the SAME mock to `global.fetch` also covers the middleware's own retry path
// (`onResponse` calls the bare, dynamically-resolved `fetch()` directly, not through openapi-fetch).
let mockFetch: jest.Mock;

function jsonResponse(status: number, body: unknown = {}): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

beforeEach(async () => {
  mockFetch = jest.fn();
  global.fetch = mockFetch;
  useStore.setState({ auth: null });
  configureApiBaseUrl("http://api.test");
  setUnauthorizedHandler(jest.fn().mockResolvedValue(false));
  await deleteStoredDeviceToken(); // isolates the device-token-rotation tests from each other
});

it("rewrites every request to the currently configured base URL (placeholder-origin fix)", async () => {
  mockFetch.mockResolvedValue(jsonResponse(200, []));

  await apiClient.GET("/api/groups", { fetch: mockFetch });

  const sentRequest = mockFetch.mock.calls[0][0] as Request;
  expect(sentRequest.url).toBe("http://api.test/api/groups");
});

// Found on-device (not by this test originally — it didn't exist): `new Request(url, request)`
// dropped the body silently in React Native's fetch, while the identical code worked fine
// against Jest's Node-based fetch. The GET-only test above never exercised a request with a
// body at all, so this regressed unnoticed until a real login POST landed at the server empty.
it("preserves the request body through the base-URL rewrite for a POST", async () => {
  mockFetch.mockResolvedValue(jsonResponse(200, {}));

  await apiClient.POST("/api/auth/login", {
    body: { organisationSlug: "org-a", email: "director@test.com", password: "password123" },
    fetch: mockFetch,
  });

  const sentRequest = mockFetch.mock.calls[0][0] as Request;
  expect(sentRequest.url).toBe("http://api.test/api/auth/login");
  const sentBody = await sentRequest.clone().json();
  expect(sentBody).toEqual({ organisationSlug: "org-a", email: "director@test.com", password: "password123" });
});

it("attaches the Bearer token from the auth slice to every request", async () => {
  useStore.setState({
    auth: { userId: "u1", email: "c@test.com", role: "staff", organisationSlug: "org-a", accessToken: "tok-1" },
  });
  mockFetch.mockResolvedValue(jsonResponse(200, []));

  await apiClient.GET("/api/groups", { fetch: mockFetch });

  const sentRequest = mockFetch.mock.calls[0][0] as Request;
  expect(sentRequest.headers.get("Authorization")).toBe("Bearer tok-1");
});

// T072 (US6, research.md R3): a response carrying X-Device-Token-Refresh is swapped into
// SecureStore before the *next* call, without interrupting the response already in flight.
it("stores a rotated device token from the X-Device-Token-Refresh response header", async () => {
  mockFetch.mockResolvedValue(
    new Response(JSON.stringify([]), {
      status: 200,
      headers: { "Content-Type": "application/json", "X-Device-Token-Refresh": "rotated-device-token" },
    }),
  );

  const result = await apiClient.GET("/api/room-shifts/roster", { fetch: mockFetch });

  expect(result.response.status).toBe(200); // the in-flight response itself is untouched
  await expect(getDeviceToken()).resolves.toBe("rotated-device-token");
});

it("does not touch the stored device token when the response carries no rotation header", async () => {
  mockFetch.mockResolvedValue(jsonResponse(200, []));

  await apiClient.GET("/api/room-shifts/roster", { fetch: mockFetch });

  await expect(getDeviceToken()).resolves.toBeNull();
});

describe("401 handling (FR-004/FR-006)", () => {
  it("refreshes exactly once and transparently retries the original request once on 401", async () => {
    useStore.setState({
      auth: { userId: "u1", email: "c@test.com", role: "staff", organisationSlug: "org-a", accessToken: "expired-tok" },
    });
    const refresh = jest.fn().mockImplementation(async () => {
      useStore.setState({
        auth: { userId: "u1", email: "c@test.com", role: "staff", organisationSlug: "org-a", accessToken: "fresh-tok" },
      });
      return true;
    });
    setUnauthorizedHandler(refresh);

    mockFetch
      .mockResolvedValueOnce(jsonResponse(401))
      .mockResolvedValueOnce(jsonResponse(200, [{ id: "g1" }]));

    const result = await apiClient.GET("/api/groups", { fetch: mockFetch });

    expect(refresh).toHaveBeenCalledTimes(1);
    expect(mockFetch).toHaveBeenCalledTimes(2);
    const retryRequest = mockFetch.mock.calls[1][0] as Request;
    expect(retryRequest.headers.get("Authorization")).toBe("Bearer fresh-tok");
    expect(result.response.status).toBe(200);
  });

  it("returns the original 401 without retrying when refresh fails", async () => {
    const refresh = jest.fn().mockResolvedValue(false);
    setUnauthorizedHandler(refresh);
    mockFetch.mockResolvedValue(jsonResponse(401));

    const result = await apiClient.GET("/api/groups", { fetch: mockFetch });

    expect(refresh).toHaveBeenCalledTimes(1);
    expect(mockFetch).toHaveBeenCalledTimes(1); // no retry attempted
    expect(result.response.status).toBe(401);
  });

  it("never triggers the unauthorized handler for /api/auth/* requests (anti-recursion guard)", async () => {
    const refresh = jest.fn().mockResolvedValue(true);
    setUnauthorizedHandler(refresh);
    mockFetch.mockResolvedValue(jsonResponse(401));

    const result = await apiClient.POST("/api/auth/refresh", {
      body: { organisationSlug: "org-a", refreshToken: "r1" },
      fetch: mockFetch,
    });

    expect(refresh).not.toHaveBeenCalled();
    expect(mockFetch).toHaveBeenCalledTimes(1);
    expect(result.response.status).toBe(401);
  });
});
