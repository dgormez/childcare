import React from "react";
import { render, fireEvent, act } from "@testing-library/react-native";
import GroupViewScreen from "../../app/(app)/index";
import type { ChildResponse, GroupResponse } from "../../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("../../services/apiClient", () => {
  const mockGet = jest.fn();
  return {
    apiClient: { GET: (...args: unknown[]) => mockGet(...args), POST: jest.fn() },
    configureApiBaseUrl: jest.fn(),
    getApiBaseUrl: () => "http://api.test",
    setUnauthorizedHandler: jest.fn(),
    __mockGet: mockGet,
  };
});

jest.mock("../../services/readCache", () => {
  const mockCacheStore = new Map<string, unknown>();
  return {
    getCached: (key: string) => mockCacheStore.get(key) ?? null,
    setCached: (key: string, data: unknown) => { mockCacheStore.set(key, data); },
    __mockCacheStore: mockCacheStore,
  };
});

jest.mock("../../services/syncEngine", () => {
  const mockSyncPendingQueue = jest.fn().mockResolvedValue({ succeeded: 0, failed: 0, conflicted: 0 });
  return { syncPendingQueue: (...args: unknown[]) => mockSyncPendingQueue(...args), __mockSyncPendingQueue: mockSyncPendingQueue };
});

const { useRouter } = require("expo-router");
const readCacheMock = jest.requireMock("../../services/readCache") as { __mockCacheStore: Map<string, unknown> };
const getMock = (jest.requireMock("../../services/apiClient") as { __mockGet: jest.Mock }).__mockGet;
const syncEngineMock = jest.requireMock("../../services/syncEngine") as { __mockSyncPendingQueue: jest.Mock };

// Mirrors openapi-fetch's real result shape (data on success, error on failure) — see
// auth.test.ts's jsonResponse for why this matters (result.response.json() throws against a
// real openapi-fetch response since the body is already consumed internally, but this mock's
// fresh Response object silently allowed it, masking the bug this suite would otherwise catch).
function jsonResponse(status: number, body: unknown) {
  const ok = status >= 200 && status < 300;
  return {
    response: { ok, status, json: async () => body },
    data: ok ? body : undefined,
    error: ok ? undefined : body,
  };
}

const group: GroupResponse = { id: "g1", name: "Group A", locationId: "loc1" };
const child: ChildResponse = {
  id: "c1", firstName: "Timmy", lastName: "Tester",
  dateOfBirth: "2022-01-01", photoDownloadUrl: null,
  allergiesDescription: "Peanuts", allergySeverity: "severe",
  medicalConditions: null, dietaryRestrictions: null, deactivatedAt: null,
};

beforeEach(() => {
  jest.clearAllMocks();
  readCacheMock.__mockCacheStore.clear();
  useRouter.mockReturnValue({ push: jest.fn(), replace: jest.fn(), back: jest.fn() });
});

it("loads and renders children from the group view, with an allergy icon and an inactive fever slot (FR-007, FR-008)", async () => {
  getMock.mockImplementation((path: string) => {
    if (path === "/api/groups") return Promise.resolve(jsonResponse(200, [group]));
    if (path === "/api/children") return Promise.resolve(jsonResponse(200, [child]));
    return Promise.resolve(jsonResponse(404, {}));
  });

  const { findByText, getByLabelText } = await render(<GroupViewScreen />);

  expect(await findByText("Timmy Tester")).toBeTruthy();
  expect(getByLabelText("child.allergyAlert")).toBeTruthy();
  expect(getByLabelText("child.feverAlert")).toBeTruthy();
});

it("navigates to the child detail route when a card is tapped (FR-008)", async () => {
  getMock.mockImplementation((path: string) => {
    if (path === "/api/groups") return Promise.resolve(jsonResponse(200, [group]));
    if (path === "/api/children") return Promise.resolve(jsonResponse(200, [child]));
    return Promise.resolve(jsonResponse(404, {}));
  });
  const push = jest.fn();
  useRouter.mockReturnValue({ push, replace: jest.fn(), back: jest.fn() });

  const { findByText } = await render(<GroupViewScreen />);
  const card = await findByText("Timmy Tester");
  fireEvent.press(card);

  expect(push).toHaveBeenCalledWith("/(app)/child/c1");
});

it("renders from read_cache when offline, with no network call succeeding (FR-009, SC-002)", async () => {
  readCacheMock.__mockCacheStore.set("children:today", [child]);
  getMock.mockRejectedValue(new Error("network down"));

  const { findByText } = await render(<GroupViewScreen />);

  expect(await findByText("Timmy Tester")).toBeTruthy();
});

it("pull-to-refresh re-fetches from the server and updates the cache (FR-011)", async () => {
  let callCount = 0;
  getMock.mockImplementation((path: string) => {
    if (path === "/api/groups") return Promise.resolve(jsonResponse(200, [group]));
    if (path === "/api/children") {
      callCount += 1;
      const firstName = callCount === 1 ? "Timmy" : "Updated";
      return Promise.resolve(jsonResponse(200, [{ ...child, firstName }]));
    }
    return Promise.resolve(jsonResponse(404, {}));
  });

  const { findByText, getByTestId } = await render(<GroupViewScreen />);
  expect(await findByText("Timmy Tester")).toBeTruthy();

  const list = getByTestId("group-view-list");
  await act(async () => {
    await list.props.refreshControl.props.onRefresh();
  });

  expect(await findByText("Updated Tester")).toBeTruthy();
  expect(readCacheMock.__mockCacheStore.get("children:today")).toEqual([{ ...child, firstName: "Updated" }]);
  expect(syncEngineMock.__mockSyncPendingQueue).toHaveBeenCalledTimes(1); // FR-012a: pull-to-refresh is a sync trigger
});

it("shows a clear empty state when the caregiver has zero assigned children (FR-007, CHK002)", async () => {
  getMock.mockImplementation((path: string) => {
    if (path === "/api/groups") return Promise.resolve(jsonResponse(200, []));
    return Promise.resolve(jsonResponse(404, {}));
  });

  const { findByText } = await render(<GroupViewScreen />);
  expect(await findByText("groupView.empty")).toBeTruthy();
});
