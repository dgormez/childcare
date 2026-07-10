import { useStore } from "../../store/useStore";

jest.mock("../../services/apiClient", () => {
  const mockPost = jest.fn();
  const mockGet = jest.fn();
  let mockUnauthorizedHandler: (() => Promise<boolean>) | null = null;
  return {
    apiClient: {
      POST: (...args: unknown[]) => mockPost(...args),
      GET: (...args: unknown[]) => mockGet(...args),
      PUT: jest.fn().mockResolvedValue({ response: { ok: true } }),
    },
    configureApiBaseUrl: jest.fn(),
    setUnauthorizedHandler: (handler: () => Promise<boolean>) => { mockUnauthorizedHandler = handler; },
    __mockPost: mockPost,
    __mockGet: mockGet,
    __getMockUnauthorizedHandler: () => mockUnauthorizedHandler,
  };
});

jest.mock("../../services/pushToken", () => ({
  registerPushToken: jest.fn().mockResolvedValue(undefined),
}));

// Imported after the mocks above so auth.ts picks them up.
import { login, loginWithApple, loginWithGoogle, refresh, logout, tryRestoreSession } from "../../services/auth";

const apiClientMock = jest.requireMock("../../services/apiClient") as {
  __mockPost: jest.Mock;
  __mockGet: jest.Mock;
  __getMockUnauthorizedHandler: () => (() => Promise<boolean>) | null;
};
const postMock = apiClientMock.__mockPost;

// Mirrors openapi-fetch's real result shape (data on success, error on failure) — mobile/'s
// auth.test.ts found that re-reading result.response.json() throws against a real
// openapi-fetch response (body already consumed internally); this fixture matches the real
// shape so a regression to that pattern would be caught here too.
function jsonResponse(status: number, body: unknown): { response: Response; data?: unknown; error?: unknown } {
  const ok = status >= 200 && status < 300;
  return {
    response: { ok, status, json: async () => body } as unknown as Response,
    data: ok ? body : undefined,
    error: ok ? undefined : body,
  };
}

const authSession = {
  accessToken: "access-1",
  refreshToken: "refresh-1",
  user: { id: "u1", email: "parent@test.com", emailVerified: true, role: "parent", name: "Parent One" },
};

beforeEach(async () => {
  jest.clearAllMocks();
  useStore.setState({ auth: null });
  const SecureStore = require("expo-secure-store");
  await SecureStore.deleteItemAsync("childcareparent_refresh_token");
  await SecureStore.deleteItemAsync("childcareparent_session_display");
});

describe("login", () => {
  it("stores tokens and populates the auth slice, including role and organisationSlug", async () => {
    postMock.mockResolvedValueOnce(jsonResponse(200, authSession));

    await login("http://api.test", "org-a", "parent@test.com", "password123");

    expect(useStore.getState().auth?.accessToken).toBe("access-1");
    expect(useStore.getState().auth?.role).toBe("parent");
    expect(useStore.getState().auth?.organisationSlug).toBe("org-a");
    expect(useStore.getState().auth?.email).toBe("parent@test.com");
  });

  it("throws the server errorKey on failure", async () => {
    postMock.mockResolvedValueOnce(jsonResponse(401, { errorKey: "errors.auth.invalid_credentials" }));

    await expect(login("http://api.test", "org-a", "parent@test.com", "wrong")).rejects.toThrow("errors.auth.invalid_credentials");
  });

  it("throws NETWORK_ERROR when there is no connectivity", async () => {
    postMock.mockRejectedValueOnce(new Error("Network request failed"));

    await expect(login("http://api.test", "org-a", "parent@test.com", "password123")).rejects.toThrow("NETWORK_ERROR");
    expect(useStore.getState().auth).toBeNull();
  });
});

describe("loginWithGoogle / loginWithApple", () => {
  it("loginWithGoogle posts the idToken and applies the session", async () => {
    postMock.mockResolvedValueOnce(jsonResponse(200, authSession));

    await loginWithGoogle("http://api.test", "org-a", "google-id-token");

    expect(postMock).toHaveBeenCalledWith("/api/auth/google", { body: { organisationSlug: "org-a", idToken: "google-id-token" } });
    expect(useStore.getState().auth?.accessToken).toBe("access-1");
  });

  it("loginWithApple posts the identityToken and optional email", async () => {
    postMock.mockResolvedValueOnce(jsonResponse(200, authSession));

    await loginWithApple("http://api.test", "org-a", "apple-identity-token", "parent@test.com");

    expect(postMock).toHaveBeenCalledWith("/api/auth/apple", {
      body: { organisationSlug: "org-a", identityToken: "apple-identity-token", email: "parent@test.com" },
    });
    expect(useStore.getState().auth?.accessToken).toBe("access-1");
  });
});

describe("session persistence", () => {
  it("tryRestoreSession restores the session from a valid stored refresh token, no fresh login needed", async () => {
    postMock.mockResolvedValueOnce(jsonResponse(200, authSession)); // login
    await login("http://api.test", "org-a", "parent@test.com", "password123");

    postMock.mockResolvedValueOnce(jsonResponse(200, { ...authSession, accessToken: "access-2" })); // refresh

    const restored = await tryRestoreSession("http://api.test");
    expect(restored).toBe(true);
    expect(useStore.getState().auth?.accessToken).toBe("access-2");
  });

  it("tryRestoreSession returns false with no stored refresh token", async () => {
    const restored = await tryRestoreSession("http://api.test");
    expect(restored).toBe(false);
  });
});

describe("silent refresh on 401", () => {
  it("the apiClient auto-retry path is wired via setUnauthorizedHandler", async () => {
    postMock.mockResolvedValueOnce(jsonResponse(200, authSession));
    await login("http://api.test", "org-a", "parent@test.com", "password123");

    expect(apiClientMock.__getMockUnauthorizedHandler()).toBe(refresh);
  });

  it("an explicit 401 on refresh signs the parent out with no retry loop", async () => {
    postMock.mockResolvedValueOnce(jsonResponse(200, authSession));
    await login("http://api.test", "org-a", "parent@test.com", "password123");

    postMock.mockResolvedValueOnce(jsonResponse(401, { errorKey: "errors.auth.invalid_credentials" }));
    const result = await refresh();

    expect(result).toBe(false);
    expect(useStore.getState().auth).toBeNull();
  });

  it("a network error during refresh does NOT clear the session", async () => {
    postMock.mockResolvedValueOnce(jsonResponse(200, authSession));
    await login("http://api.test", "org-a", "parent@test.com", "password123");

    postMock.mockRejectedValueOnce(new Error("network down"));
    const result = await refresh();

    expect(result).toBe(false);
    expect(useStore.getState().auth?.accessToken).toBe("access-1"); // untouched
  });
});

describe("logout", () => {
  it("clears the auth slice and stored refresh token", async () => {
    postMock.mockResolvedValueOnce(jsonResponse(200, authSession));
    await login("http://api.test", "org-a", "parent@test.com", "password123");

    postMock.mockResolvedValueOnce(jsonResponse(204, {}));
    await logout();

    expect(useStore.getState().auth).toBeNull();
    const restored = await tryRestoreSession("http://api.test");
    expect(restored).toBe(false); // nothing left to restore
  });
});
