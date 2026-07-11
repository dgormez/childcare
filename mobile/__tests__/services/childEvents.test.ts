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
import { recordChildEvent, recordChildEventBatch, endSleepEvent } from "../../services/childEvents";

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

// Feature 009c (T028, research.md R6): a multi-child batch is queued as exactly one offline_queue
// entry, never one per child, and each child still gets its own client-generated id for
// idempotent retry (research.md R5).
describe("recordChildEventBatch", () => {
  it("online: posts one request with every childId and returns the server response", async () => {
    const serverResponse = { created: [{ childId: "c1", eventId: "e1" }, { childId: "c2", eventId: "e2" }], errors: [] };
    (apiClient.POST as jest.Mock).mockResolvedValue({ response: { ok: true }, data: serverResponse });

    const result = await recordChildEventBatch(
      { childIds: ["c1", "c2"], eventType: "diaper", occurredAt: "2026-01-01T10:00:00.000Z", payload: { type: "wet" } },
      true
    );

    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/child-events/batch",
      expect.objectContaining({
        body: expect.objectContaining({
          items: [
            { childId: "c1", id: expect.any(String) },
            { childId: "c2", id: expect.any(String) },
          ],
          eventType: "diaper",
        }),
      })
    );
    expect(result.response).toEqual(serverResponse);
    expect(offlineQueueMock.__mockEnqueue).not.toHaveBeenCalled();
  });

  it("offline: queues exactly one offline_queue entry for the whole batch, not one per child", async () => {
    const result = await recordChildEventBatch(
      { childIds: ["c1", "c2", "c3"], eventType: "sleep", occurredAt: "2026-01-01T13:00:00.000Z", payload: { quality: null } },
      false
    );

    expect(offlineQueueMock.__mockEnqueue).toHaveBeenCalledTimes(1);
    expect(offlineQueueMock.__mockEnqueue).toHaveBeenCalledWith(
      expect.objectContaining({
        entityType: "child_event_batch",
        operation: "create",
        endpoint: "/api/child-events/batch",
        httpMethod: "POST",
      })
    );
    // Optimistic reconstruction assumes success for every child until the real replay result is known.
    expect(result.response.created).toHaveLength(3);
    expect(result.response.errors).toEqual([]);
  });

  it("gives each child a distinct client-generated id, even offline", async () => {
    const result = await recordChildEventBatch(
      { childIds: ["c1", "c2"], eventType: "note", occurredAt: "2026-01-01T10:00:00.000Z", payload: { text: "hi" } },
      false
    );

    const ids = result.itemIds.map((i) => i.id);
    expect(new Set(ids).size).toBe(2);
  });
});
