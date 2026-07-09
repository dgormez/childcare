jest.mock("../../services/apiClient", () => ({
  apiClient: { POST: jest.fn(), PATCH: jest.fn(), DELETE: jest.fn(), GET: jest.fn() },
}));

jest.mock("../../services/offlineQueue", () => {
  const mockEnqueue = jest.fn().mockResolvedValue("queued-id");
  const mockGetPending = jest.fn().mockResolvedValue([]);
  return {
    enqueue: (...args: unknown[]) => mockEnqueue(...args),
    getPending: (...args: unknown[]) => mockGetPending(...args),
    __mockEnqueue: mockEnqueue,
    __mockGetPending: mockGetPending,
  };
});

jest.mock("../../services/localDb", () => {
  const mockUpdateQueueRowPayload = jest.fn().mockReturnValue(false);
  return {
    updateQueueRowPayload: (...args: unknown[]) => mockUpdateQueueRowPayload(...args),
    __mockUpdateQueueRowPayload: mockUpdateQueueRowPayload,
  };
});

jest.mock("../../services/syncEngine", () => ({ registerSyncHandler: jest.fn() }));

import { apiClient } from "../../services/apiClient";
import { recordChildEvent, endSleepEvent } from "../../services/childEvents";

const offlineQueueMock = jest.requireMock("../../services/offlineQueue") as {
  __mockEnqueue: jest.Mock;
  __mockGetPending: jest.Mock;
};
const localDbMock = jest.requireMock("../../services/localDb") as { __mockUpdateQueueRowPayload: jest.Mock };

function pendingCreateRow(eventId: string, endedAt: string | null = null) {
  return {
    id: "queue-row-1",
    tenant_id: "org-a",
    entity_type: "child_event",
    operation: "create",
    payload: JSON.stringify({ id: eventId, childId: "child-1", eventType: "sleep", occurredAt: "2026-01-01T13:00:00.000Z", endedAt, payload: {} }),
    endpoint: "/api/child-events",
    http_method: "POST",
    created_at: "2026-01-01T13:00:00.000Z",
    synced_at: null,
    sync_error: null,
  };
}

beforeEach(() => {
  jest.clearAllMocks();
  offlineQueueMock.__mockGetPending.mockResolvedValue([]);
  localDbMock.__mockUpdateQueueRowPayload.mockReturnValue(false);
});

describe("endSleepEvent — offline merge behavior (research.md R3)", () => {
  it("merges endedAt/quality into the still-queued create row instead of adding a second row", async () => {
    const eventId = "sleep-event-1";
    offlineQueueMock.__mockGetPending.mockResolvedValue([pendingCreateRow(eventId)]);
    localDbMock.__mockUpdateQueueRowPayload.mockReturnValue(true);

    const result = await endSleepEvent(eventId, "2026-01-01T14:30:00.000Z", "good", false);

    expect(result).toBeNull();
    expect(localDbMock.__mockUpdateQueueRowPayload).toHaveBeenCalledWith(
      "queue-row-1",
      expect.stringContaining("2026-01-01T14:30:00.000Z")
    );
    expect(offlineQueueMock.__mockEnqueue).not.toHaveBeenCalled();
  });

  it("queues a normal PATCH when the create has already synced (row not found pending)", async () => {
    offlineQueueMock.__mockGetPending.mockResolvedValue([]); // create already synced — not in the pending list

    await endSleepEvent("sleep-event-2", "2026-01-01T14:30:00.000Z", "restless", false);

    expect(localDbMock.__mockUpdateQueueRowPayload).not.toHaveBeenCalled();
    expect(offlineQueueMock.__mockEnqueue).toHaveBeenCalledWith(
      expect.objectContaining({
        entityType: "child_event",
        operation: "update",
        endpoint: "/api/child-events/sleep-event-2",
        httpMethod: "PATCH",
      })
    );
  });

  it("falls back to a queued PATCH if the merge loses the race (row synced between read and update)", async () => {
    const eventId = "sleep-event-3";
    offlineQueueMock.__mockGetPending.mockResolvedValue([pendingCreateRow(eventId)]);
    localDbMock.__mockUpdateQueueRowPayload.mockReturnValue(false); // synced in the interim

    await endSleepEvent(eventId, "2026-01-01T14:30:00.000Z", "okay", false);

    expect(offlineQueueMock.__mockEnqueue).toHaveBeenCalledWith(
      expect.objectContaining({ operation: "update", endpoint: `/api/child-events/${eventId}` })
    );
  });

  it("when online, PATCHes directly rather than touching the offline queue at all", async () => {
    (apiClient.PATCH as jest.Mock).mockResolvedValue({ response: { ok: true }, data: { id: "sleep-event-4" } });

    await endSleepEvent("sleep-event-4", "2026-01-01T14:30:00.000Z", "good", true);

    expect(apiClient.PATCH).toHaveBeenCalled();
    expect(offlineQueueMock.__mockEnqueue).not.toHaveBeenCalled();
    expect(offlineQueueMock.__mockGetPending).not.toHaveBeenCalled();
  });
});

describe("recordChildEvent", () => {
  it("online: posts directly and returns the server response", async () => {
    (apiClient.POST as jest.Mock).mockResolvedValue({ response: { ok: true }, data: { id: "e1", childId: "c1" } });

    const result = await recordChildEvent(
      { childId: "c1", eventType: "diaper", occurredAt: "2026-01-01T10:00:00.000Z", payload: { type: "wet" } },
      true
    );

    expect(result).toEqual({ id: "e1", childId: "c1" });
    expect(offlineQueueMock.__mockEnqueue).not.toHaveBeenCalled();
  });

  it("offline: enqueues a create and returns an optimistic reconstruction (FR-012)", async () => {
    const result = await recordChildEvent(
      { childId: "c1", eventType: "diaper", occurredAt: "2026-01-01T10:00:00.000Z", payload: { type: "wet" } },
      false
    );

    expect(offlineQueueMock.__mockEnqueue).toHaveBeenCalledWith(
      expect.objectContaining({ entityType: "child_event", operation: "create", httpMethod: "POST" })
    );
    expect(result.childId).toBe("c1");
    expect(result.payload).toEqual({ type: "wet" });
    expect(result.recordedBy).toEqual([]);
  });

  // feature 009a FR-005: a `custom` event queues/syncs identically to every other event type —
  // the offline queue has no type-specific branching, so this is the same generic path US1's
  // other event types already exercise above, just asserted explicitly for `custom`.
  it("offline: a custom event enqueues exactly like any other event type (no type-specific branching)", async () => {
    const result = await recordChildEvent(
      { childId: "c1", eventType: "custom", occurredAt: "2026-01-01T10:00:00.000Z", payload: { label: "Sunscreen applied" } },
      false
    );

    expect(offlineQueueMock.__mockEnqueue).toHaveBeenCalledWith(
      expect.objectContaining({ entityType: "child_event", operation: "create", httpMethod: "POST" })
    );
    expect(result.payload).toEqual({ label: "Sunscreen applied" });
  });
});
