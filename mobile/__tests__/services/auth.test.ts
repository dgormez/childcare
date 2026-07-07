import { useStore } from "../../store/useStore";

// In-memory fake for localDb's config store + tenant wipe — the shared generic expo-sqlite
// mock is a no-op stub (always returns null), so config persistence needs a real fake here.
// State lives inside the factory (module-scope inside the mock module) since jest.mock()
// factories cannot close over outer variables — only `mock`-prefixed ones are exempted.
jest.mock("../../services/localDb", () => {
  const mockConfigStore = new Map<string, string>();
  const mockDeletedTenantIds: string[] = [];
  return {
    getConfigValue: (key: string) => mockConfigStore.get(key) ?? null,
    setConfigValue: (key: string, value: string) => { mockConfigStore.set(key, value); },
    deleteConfigValue: (key: string) => { mockConfigStore.delete(key); },
    deleteLocalTenantData: (tenantId: string) => { mockDeletedTenantIds.push(tenantId); mockConfigStore.clear(); },
    __mockConfigStore: mockConfigStore,
    __mockDeletedTenantIds: mockDeletedTenantIds,
  };
});

jest.mock("../../services/apiClient", () => {
  const mockPost = jest.fn();
  const mockGet = jest.fn();
  let mockUnauthorizedHandler: (() => Promise<boolean>) | null = null;
  return {
    apiClient: { POST: (...args: unknown[]) => mockPost(...args), GET: (...args: unknown[]) => mockGet(...args) },
    configureApiBaseUrl: jest.fn(),
    setUnauthorizedHandler: (handler: () => Promise<boolean>) => { mockUnauthorizedHandler = handler; },
    __mockPost: mockPost,
    __mockGet: mockGet,
    __getMockUnauthorizedHandler: () => mockUnauthorizedHandler,
  };
});

// Imported after the mocks above so auth.ts picks them up.
import { login, refresh, logout, tryRestoreSession } from "../../services/auth";

const localDbMock = jest.requireMock("../../services/localDb") as {
  __mockConfigStore: Map<string, string>;
  __mockDeletedTenantIds: string[];
};
const apiClientMock = jest.requireMock("../../services/apiClient") as {
  __mockPost: jest.Mock;
  __mockGet: jest.Mock;
  __getMockUnauthorizedHandler: () => (() => Promise<boolean>) | null;
};
const postMock = apiClientMock.__mockPost;
const getMock = apiClientMock.__mockGet;
const configStore = localDbMock.__mockConfigStore;
const deletedTenantIds = localDbMock.__mockDeletedTenantIds;

function jsonResponse(status: number, body: unknown): { response: Response } {
  return {
    response: {
      ok: status >= 200 && status < 300,
      status,
      json: async () => body,
    } as unknown as Response,
  };
}

const authSession = {
  accessToken: "access-1",
  refreshToken: "refresh-1",
  user: { id: "u1", email: "care@test.com", emailVerified: true, role: "staff" },
};

beforeEach(() => {
  jest.clearAllMocks();
  configStore.clear();
  deletedTenantIds.length = 0;
  useStore.setState({ auth: null });
});

describe("login", () => {
  it("stores tokens and populates the auth slice, including role and organisationSlug", async () => {
    postMock.mockResolvedValueOnce(jsonResponse(200, authSession));
    getMock.mockResolvedValueOnce(jsonResponse(200, { staffProfileId: "sp1", firstName: "Care", lastName: "Giver", role: "staff", eligibleLocationIds: ["loc1"] }));

    await login("http://api.test", "org-a", "care@test.com", "password123");

    expect(useStore.getState().auth?.accessToken).toBe("access-1");
    expect(useStore.getState().auth?.role).toBe("staff");
    expect(useStore.getState().auth?.organisationSlug).toBe("org-a");
    expect(useStore.getState().auth?.staffProfileId).toBe("sp1");
    expect(useStore.getState().auth?.eligibleLocationIds).toEqual(["loc1"]);
  });

  it("throws the server errorKey on failure", async () => {
    postMock.mockResolvedValueOnce(jsonResponse(401, { errorKey: "errors.auth.invalid_credentials" }));

    await expect(login("http://api.test", "org-a", "care@test.com", "wrong")).rejects.toThrow("errors.auth.invalid_credentials");
  });

  it("throws NETWORK_ERROR when there is no connectivity (brand-new install, first launch offline)", async () => {
    postMock.mockRejectedValueOnce(new Error("Network request failed"));

    await expect(login("http://api.test", "org-a", "care@test.com", "password123")).rejects.toThrow("NETWORK_ERROR");
    expect(useStore.getState().auth).toBeNull();
  });
});

describe("session persistence (FR-002)", () => {
  it("tryRestoreSession restores the session from a valid stored refresh token, no fresh login needed", async () => {
    postMock.mockResolvedValueOnce(jsonResponse(200, authSession)); // login
    getMock.mockResolvedValueOnce(jsonResponse(200, { staffProfileId: "sp1", firstName: "C", lastName: "G", role: "staff", eligibleLocationIds: [] }));
    await login("http://api.test", "org-a", "care@test.com", "password123");

    postMock.mockResolvedValueOnce(jsonResponse(200, { ...authSession, accessToken: "access-2" })); // refresh
    getMock.mockResolvedValueOnce(jsonResponse(200, { staffProfileId: "sp1", firstName: "C", lastName: "G", role: "staff", eligibleLocationIds: [] }));

    const restored = await tryRestoreSession("http://api.test");
    expect(restored).toBe(true);
    expect(useStore.getState().auth?.accessToken).toBe("access-2");
  });
});

describe("silent refresh on 401 (FR-004)", () => {
  it("the apiClient auto-retry path is wired via setUnauthorizedHandler", async () => {
    postMock.mockResolvedValueOnce(jsonResponse(200, authSession));
    getMock.mockResolvedValueOnce(jsonResponse(200, { staffProfileId: "sp1", firstName: "C", lastName: "G", role: "staff", eligibleLocationIds: [] }));
    await login("http://api.test", "org-a", "care@test.com", "password123");

    expect(apiClientMock.__getMockUnauthorizedHandler()).toBe(refresh);
  });
});

describe("logout (FR-005/FR-019)", () => {
  it("clears SecureStore tokens, the auth slice, and tenant-scoped local data", async () => {
    postMock.mockResolvedValueOnce(jsonResponse(200, authSession));
    getMock.mockResolvedValueOnce(jsonResponse(200, { staffProfileId: "sp1", firstName: "C", lastName: "G", role: "staff", eligibleLocationIds: [] }));
    await login("http://api.test", "org-a", "care@test.com", "password123");

    postMock.mockResolvedValueOnce(jsonResponse(204, {})); // logout revoke call
    await logout();

    expect(useStore.getState().auth).toBeNull();
    expect(deletedTenantIds).toContain("org-a");
  });
});

describe("device handoff between two caregivers (FR-019)", () => {
  it("caregiver A's cached identity is gone once caregiver B logs in on the same device", async () => {
    postMock.mockResolvedValueOnce(jsonResponse(200, authSession));
    getMock.mockResolvedValueOnce(jsonResponse(200, { staffProfileId: "sp-a", firstName: "A", lastName: "One", role: "staff", eligibleLocationIds: ["loc-a"] }));
    await login("http://api.test", "org-a", "carer-a@test.com", "password123");
    expect(configStore.get("organisationSlug")).toBe("org-a");

    postMock.mockResolvedValueOnce(jsonResponse(204, {}));
    await logout();

    expect(deletedTenantIds).toContain("org-a");
    expect(useStore.getState().auth).toBeNull();
    expect(configStore.size).toBe(0);

    const authSessionB = {
      accessToken: "access-b",
      refreshToken: "refresh-b",
      user: { id: "u2", email: "carer-b@test.com", emailVerified: true, role: "director" },
    };
    postMock.mockResolvedValueOnce(jsonResponse(200, authSessionB));
    getMock.mockResolvedValueOnce(jsonResponse(404, {}));
    await login("http://api.test", "org-b", "carer-b@test.com", "password456");

    expect(useStore.getState().auth?.userId).toBe("u2");
    expect(useStore.getState().auth?.organisationSlug).toBe("org-b");
    expect(useStore.getState().auth?.staffProfileId).toBeUndefined();
    expect(deletedTenantIds).not.toContain("org-b");
  });
});

describe("refresh rejection triggers clean sign-out (FR-006)", () => {
  it("an explicit 401 on refresh signs the caregiver out with no retry loop", async () => {
    postMock.mockResolvedValueOnce(jsonResponse(200, authSession));
    getMock.mockResolvedValueOnce(jsonResponse(200, { staffProfileId: "sp1", firstName: "C", lastName: "G", role: "staff", eligibleLocationIds: [] }));
    await login("http://api.test", "org-a", "care@test.com", "password123");

    postMock.mockResolvedValueOnce(jsonResponse(401, { errorKey: "errors.auth.invalid_credentials" }));
    const result = await refresh();

    expect(result).toBe(false);
    expect(useStore.getState().auth).toBeNull();
    expect(deletedTenantIds).toContain("org-a");
  });

  it("a network error during refresh does NOT clear the session (may still be valid once back online)", async () => {
    postMock.mockResolvedValueOnce(jsonResponse(200, authSession));
    getMock.mockResolvedValueOnce(jsonResponse(200, { staffProfileId: "sp1", firstName: "C", lastName: "G", role: "staff", eligibleLocationIds: [] }));
    await login("http://api.test", "org-a", "care@test.com", "password123");

    postMock.mockRejectedValueOnce(new Error("network down"));
    const result = await refresh();

    expect(result).toBe(false);
    expect(deletedTenantIds).not.toContain("org-a");
  });
});
