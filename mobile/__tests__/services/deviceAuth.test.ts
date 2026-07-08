import { useStore } from "../../store/useStore";

jest.mock("../../services/deviceTokenStorage", () => {
  let storedToken: string | null = null;
  return {
    getDeviceToken: () => Promise.resolve(storedToken),
    storeDeviceToken: (token: string) => { storedToken = token; return Promise.resolve(); },
    deleteStoredDeviceToken: () => { storedToken = null; return Promise.resolve(); },
    __setStoredToken: (token: string | null) => { storedToken = token; },
    __getStoredToken: () => storedToken,
  };
});

jest.mock("../../services/localDb", () => {
  const mockConfigStore = new Map<string, string>();
  const mockDeletedTenantIds: string[] = [];
  return {
    getConfigValue: (key: string) => mockConfigStore.get(key) ?? null,
    setConfigValue: (key: string, value: string) => { mockConfigStore.set(key, value); },
    deleteConfigValue: (key: string) => { mockConfigStore.delete(key); },
    deleteLocalTenantData: (tenantId: string) => { mockDeletedTenantIds.push(tenantId); },
    __mockConfigStore: mockConfigStore,
    __mockDeletedTenantIds: mockDeletedTenantIds,
  };
});

jest.mock("../../services/apiClient", () => {
  const mockPost = jest.fn();
  return {
    apiClient: { POST: (...args: unknown[]) => mockPost(...args) },
    __mockPost: mockPost,
  };
});

import {
  pairDevice, exitRoomMode, tryRestoreDeviceState, handleDeviceRejection, applyDevicePairing,
} from "../../services/deviceAuth";

const deviceTokenStorageMock = jest.requireMock("../../services/deviceTokenStorage") as {
  __setStoredToken: (token: string | null) => void;
  __getStoredToken: () => string | null;
};
const localDbMock = jest.requireMock("../../services/localDb") as {
  __mockConfigStore: Map<string, string>;
  __mockDeletedTenantIds: string[];
};
const apiClientMock = jest.requireMock("../../services/apiClient") as { __mockPost: jest.Mock };
const postMock = apiClientMock.__mockPost;
const configStore = localDbMock.__mockConfigStore;
const deletedTenantIds = localDbMock.__mockDeletedTenantIds;

function jsonResponse(status: number, body: unknown): { response: Response; data?: unknown; error?: unknown } {
  const ok = status >= 200 && status < 300;
  return {
    response: { ok, status, json: async () => body } as unknown as Response,
    data: ok ? body : undefined,
    error: ok ? undefined : body,
  };
}

beforeEach(() => {
  jest.clearAllMocks();
  deviceTokenStorageMock.__setStoredToken(null);
  configStore.clear();
  deletedTenantIds.length = 0;
  useStore.setState({ device: null });
});

// ── T019 (US1): room-setup pairs, stores the device token, and treats its presence as
//    "enter room mode" on next launch ──

describe("pairDevice", () => {
  it("stores the returned device token and pairing state on success", async () => {
    postMock.mockResolvedValueOnce(jsonResponse(200, { deviceId: "dev-1", deviceToken: "device-tok-1", tokenVersion: 1 }));

    await pairDevice("loc-1", "grp-1", "Location A", "Group A", "135790");

    expect(deviceTokenStorageMock.__getStoredToken()).toBe("device-tok-1");
    expect(useStore.getState().device).toEqual({
      deviceId: "dev-1", locationId: "loc-1", groupId: "grp-1", locationName: "Location A", groupName: "Group A",
    });
  });

  it("throws the server errorKey on failure and stores nothing", async () => {
    postMock.mockResolvedValueOnce(jsonResponse(404, { errorKey: "errors.devices.group_not_found" }));

    await expect(pairDevice("loc-1", "grp-1", "Location A", "Group A", "135790")).rejects.toThrow("errors.devices.group_not_found");
    expect(deviceTokenStorageMock.__getStoredToken()).toBeNull();
    expect(useStore.getState().device).toBeNull();
  });
});

describe("tryRestoreDeviceState", () => {
  it("returns true and restores the device slice when a token and full pairing state are cached", async () => {
    deviceTokenStorageMock.__setStoredToken("device-tok-1");
    applyDevicePairing({ deviceId: "dev-1", locationId: "loc-1", groupId: "grp-1", locationName: "Location A", groupName: "Group A" });
    useStore.setState({ device: null }); // simulate a fresh app launch — only cached config + SecureStore survive

    const restored = await tryRestoreDeviceState();

    expect(restored).toBe(true);
    expect(useStore.getState().device?.deviceId).toBe("dev-1");
  });

  it("returns false when no device token is stored (never paired, or since cleared)", async () => {
    const restored = await tryRestoreDeviceState();
    expect(restored).toBe(false);
  });
});

describe("exitRoomMode", () => {
  it("clears all device credentials on a correct override PIN", async () => {
    deviceTokenStorageMock.__setStoredToken("device-tok-1");
    applyDevicePairing({ deviceId: "dev-1", locationId: "loc-1", groupId: "grp-1", locationName: "Location A", groupName: "Group A" });
    postMock.mockResolvedValueOnce(jsonResponse(200, { ok: true }));

    await exitRoomMode("135790");

    expect(deviceTokenStorageMock.__getStoredToken()).toBeNull();
    expect(useStore.getState().device).toBeNull();
  });

  it("throws the server errorKey on an incorrect override PIN, without clearing credentials", async () => {
    deviceTokenStorageMock.__setStoredToken("device-tok-1");
    applyDevicePairing({ deviceId: "dev-1", locationId: "loc-1", groupId: "grp-1", locationName: "Location A", groupName: "Group A" });
    postMock.mockResolvedValueOnce(jsonResponse(401, { errorKey: "errors.devices.invalid_override_pin" }));

    await expect(exitRoomMode("000000")).rejects.toThrow("errors.devices.invalid_override_pin");
    expect(deviceTokenStorageMock.__getStoredToken()).toBe("device-tok-1");
  });
});

// ── T077 (US7): a device.revoked/token_expired rejection clears all local credentials and
//    cached tenant data, returning the tablet to (room-setup) ──

describe("handleDeviceRejection (FR-021/FR-022)", () => {
  it("clears the device token, pairing config, device slice, and cached tenant data", async () => {
    deviceTokenStorageMock.__setStoredToken("device-tok-1");
    applyDevicePairing({ deviceId: "dev-1", locationId: "loc-1", groupId: "grp-1", locationName: "Location A", groupName: "Group A" });
    configStore.set("organisationSlug", "org-a");

    await handleDeviceRejection();

    expect(deviceTokenStorageMock.__getStoredToken()).toBeNull();
    expect(useStore.getState().device).toBeNull();
    expect(configStore.has("deviceId")).toBe(false);
    expect(deletedTenantIds).toContain("org-a");
  });

  it("is a no-op on cached tenant data when no organisation slug was ever cached", async () => {
    deviceTokenStorageMock.__setStoredToken("device-tok-1");

    await handleDeviceRejection();

    expect(deletedTenantIds).toEqual([]);
  });
});
