jest.mock("../../services/apiClient", () => ({
  apiClient: { POST: jest.fn(), GET: jest.fn() },
}));

jest.mock("../../services/offlineQueue", () => {
  const mockEnqueue = jest.fn().mockResolvedValue("queued-id");
  return {
    enqueue: (...args: unknown[]) => mockEnqueue(...args),
    __mockEnqueue: mockEnqueue,
  };
});

jest.mock("../../services/syncEngine", () => ({ registerSyncHandler: jest.fn() }));

import { apiClient } from "../../services/apiClient";
import { createGroupActivity, getGroupTimeline } from "../../services/groupActivities";

const offlineQueueMock = jest.requireMock("../../services/offlineQueue") as { __mockEnqueue: jest.Mock };
const syncEngineMock = jest.requireMock("../../services/syncEngine") as { registerSyncHandler: jest.Mock };

// Captured before any beforeEach's clearAllMocks() runs — the module registers its handler
// exactly once, at import time above, not on each test.
const registerSyncHandlerCallsAtImport = syncEngineMock.registerSyncHandler.mock.calls.slice();

beforeEach(() => {
  jest.clearAllMocks();
});

describe("createGroupActivity", () => {
  it("online: posts directly and returns the server response", async () => {
    (apiClient.POST as jest.Mock).mockResolvedValue({ response: { ok: true }, data: { id: "a1", title: "In de tuin" } });

    const result = await createGroupActivity(
      { activityType: "outdoor", title: "In de tuin", occurredAt: "2026-01-01T10:00:00.000Z" },
      true
    );

    expect(result).toEqual({ id: "a1", title: "In de tuin" });
    expect(offlineQueueMock.__mockEnqueue).not.toHaveBeenCalled();
  });

  it("offline: enqueues a create and returns an optimistic reconstruction (FR-012)", async () => {
    const result = await createGroupActivity(
      { activityType: "creative", title: "Tekenen", description: "Met verf", occurredAt: "2026-01-01T10:00:00.000Z" },
      false
    );

    expect(offlineQueueMock.__mockEnqueue).toHaveBeenCalledWith(
      expect.objectContaining({ entityType: "group_activity", operation: "create", httpMethod: "POST", endpoint: "/api/group-activities" })
    );
    expect(result.title).toBe("Tekenen");
    expect(result.description).toBe("Met verf");
    expect(result.recordedBy).toEqual([]);
    expect(result.photos).toEqual([]);
  });

  it("registers a sync handler for entity_type 'group_activity'", () => {
    expect(registerSyncHandlerCallsAtImport).toContainEqual(["group_activity", expect.objectContaining({ onConflict: expect.any(Function) })]);
  });
});

describe("getGroupTimeline", () => {
  it("fetches the merged timeline for a group/date", async () => {
    (apiClient.GET as jest.Mock).mockResolvedValue({ response: { ok: true }, data: { entries: [] } });

    const result = await getGroupTimeline("group-1", "2026-01-01");

    expect(apiClient.GET).toHaveBeenCalledWith("/api/group-activities/timeline", {
      params: { query: { groupId: "group-1", date: "2026-01-01" } },
    });
    expect(result).toEqual({ entries: [] });
  });

  it("throws when the request fails", async () => {
    (apiClient.GET as jest.Mock).mockResolvedValue({ response: { ok: false } });

    await expect(getGroupTimeline("group-1")).rejects.toThrow();
  });
});
