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
  return {
    syncPendingQueue: (...args: unknown[]) => mockSyncPendingQueue(...args),
    __mockSyncPendingQueue: mockSyncPendingQueue,
    // feature 010: attendance.ts registers a sync handler at module load time.
    registerSyncHandler: jest.fn(),
  };
});

// Feature 009c: recordChildEventBatch is registered by childEvents.ts at module load time
// (registerSyncHandler("child_event_batch", ...)) — the mock only needs to satisfy that.
jest.mock("../../services/childEvents", () => ({
  recordChildEventBatch: jest.fn(),
}));

const mockToastShow = jest.fn();
jest.mock("react-native-toast-message", () => ({ show: (...args: unknown[]) => mockToastShow(...args) }));

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

function presentAttendance(childId: string) {
  return { id: `a-${childId}`, childId, locationId: "loc1", date: "2026-07-11", status: "present", checkInAt: "2026-07-11T08:00:00Z", checkOutAt: null, plannedDurationMinutes: null, absenceJustified: null, absenceReason: null, recordedBy: [], createdAt: "2026-07-11T08:00:00Z", updatedAt: "2026-07-11T08:00:00Z" };
}
function absentAttendance(childId: string) {
  return { ...presentAttendance(childId), status: "absent", checkInAt: null };
}

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

// FR-001/FR-017 (feature 010): a card's own tap is now check-in/out, the screen's
// highest-frequency action — navigation to the detail/timeline moved to a distinct secondary
// affordance so it doesn't compete with that one-tap gesture.
it("navigates to the child detail route via the secondary 'view details' affordance", async () => {
  getMock.mockImplementation((path: string) => {
    if (path === "/api/groups") return Promise.resolve(jsonResponse(200, [group]));
    if (path === "/api/children") return Promise.resolve(jsonResponse(200, [child]));
    return Promise.resolve(jsonResponse(404, {}));
  });
  const push = jest.fn();
  useRouter.mockReturnValue({ push, replace: jest.fn(), back: jest.fn() });

  const { findByText, getByLabelText } = await render(<GroupViewScreen />);
  await findByText("Timmy Tester");
  fireEvent.press(getByLabelText("groupView.viewDetail"));

  expect(push).toHaveBeenCalledWith("/(app)/child/c1");
});

it("tapping the card itself checks the child in, not navigate (FR-001/FR-017)", async () => {
  getMock.mockImplementation((path: string) => {
    if (path === "/api/groups") return Promise.resolve(jsonResponse(200, [group]));
    if (path === "/api/children") return Promise.resolve(jsonResponse(200, [child]));
    return Promise.resolve(jsonResponse(404, {}));
  });
  const postMock = (jest.requireMock("../../services/apiClient") as { apiClient: { POST: jest.Mock } }).apiClient.POST;
  postMock.mockResolvedValue(jsonResponse(201, { id: "a1", childId: "c1", status: "present", checkInAt: "2026-01-05T08:00:00Z" }));
  const push = jest.fn();
  useRouter.mockReturnValue({ push, replace: jest.fn(), back: jest.fn() });

  const { findByText } = await render(<GroupViewScreen />);
  const card = await findByText("Timmy Tester");
  await act(async () => {
    fireEvent.press(card);
  });

  expect(postMock).toHaveBeenCalledWith("/api/attendance/check-in", expect.objectContaining({ body: { childId: "c1", date: expect.any(String) } }));
  expect(push).not.toHaveBeenCalled();
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

// Feature 009c (T014/T020): multi-select mode on the room roster — present children become
// selectable, absent children stay non-interactive for selection purposes.

const childB: ChildResponse = { ...child, id: "c2", firstName: "Amy", lastName: "Ainsworth" };
const childC: ChildResponse = { ...child, id: "c3", firstName: "Ben", lastName: "Baker", allergiesDescription: null };

it("entering multi-select mode makes present children selectable and leaves absent children non-selectable (T014)", async () => {
  getMock.mockImplementation((path: string) => {
    if (path === "/api/groups") return Promise.resolve(jsonResponse(200, [group]));
    if (path === "/api/children") return Promise.resolve(jsonResponse(200, [child, childB]));
    if (path === "/api/attendance/today") return Promise.resolve(jsonResponse(200, [presentAttendance("c1"), absentAttendance("c2")]));
    return Promise.resolve(jsonResponse(404, {}));
  });

  const { findByText, getByLabelText, getByText, queryByText } = await render(<GroupViewScreen />);
  await findByText("Timmy Tester");

  await act(async () => fireEvent.press(getByLabelText("groupView.multiSelect.enter")));
  expect(queryByText("groupView.multiSelect.selectedCount")).toBeTruthy();

  const presentCard = await findByText("Timmy Tester");
  await act(async () => fireEvent.press(presentCard));
  expect(getByText("groupView.multiSelect.logEvent (1)")).toBeTruthy();

  const absentCard = await findByText("Amy Ainsworth");
  await act(async () => fireEvent.press(absentCard));
  // Still only 1 selected — the absent child's card is disabled, the tap has no effect.
  expect(getByText("groupView.multiSelect.logEvent (1)")).toBeTruthy();
});

it("'Alles selecteren' selects every present child up to the 30-child cap, and further taps beyond the cap are blocked with an explanation (T014/T020, SC-005)", async () => {
  const manyChildren = Array.from({ length: 31 }, (_, i) => ({ ...child, id: `c${i}`, firstName: `Child${i}` }));
  const manyAttendance = manyChildren.map((c) => presentAttendance(c.id));
  getMock.mockImplementation((path: string) => {
    if (path === "/api/groups") return Promise.resolve(jsonResponse(200, [group]));
    if (path === "/api/children") return Promise.resolve(jsonResponse(200, manyChildren));
    if (path === "/api/attendance/today") return Promise.resolve(jsonResponse(200, manyAttendance));
    return Promise.resolve(jsonResponse(404, {}));
  });

  const { findByText, getByLabelText, getByText } = await render(<GroupViewScreen />);
  await findByText("Child0 Tester");

  await act(async () => fireEvent.press(getByLabelText("groupView.multiSelect.enter")));
  await act(async () => fireEvent.press(await findByText("groupView.multiSelect.selectAll")));

  expect(getByText("groupView.multiSelect.logEvent (30)")).toBeTruthy();
  expect(mockToastShow).toHaveBeenCalledWith(expect.objectContaining({ text1: "groupView.multiSelect.maxReached" }));
});
