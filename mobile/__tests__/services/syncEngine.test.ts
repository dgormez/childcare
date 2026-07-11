import { useStore } from "../../store/useStore";

jest.mock("../../services/offlineQueue", () => {
  const mockGetPending = jest.fn();
  const mockMarkSynced = jest.fn();
  const mockMarkSyncError = jest.fn();
  return {
    getPending: (...args: unknown[]) => mockGetPending(...args),
    markSynced: (...args: unknown[]) => mockMarkSynced(...args),
    markSyncError: (...args: unknown[]) => mockMarkSyncError(...args),
    __mockGetPending: mockGetPending,
    __mockMarkSynced: mockMarkSynced,
    __mockMarkSyncError: mockMarkSyncError,
  };
});

jest.mock("../../services/auth", () => ({ refresh: jest.fn() }));
jest.mock("../../services/apiClient", () => ({ getApiBaseUrl: () => "http://api.test" }));

jest.mock("../../services/localDb", () => {
  const mockGetQueueRowById = jest.fn().mockReturnValue(null);
  return {
    setLastSyncTime: jest.fn(),
    getQueueRowById: (...args: unknown[]) => mockGetQueueRowById(...args),
    __mockGetQueueRowById: mockGetQueueRowById,
  };
});

import { syncPendingQueue, registerSyncHandler } from "../../services/syncEngine";

const offlineQueueMock = jest.requireMock("../../services/offlineQueue") as {
  __mockGetPending: jest.Mock;
  __mockMarkSynced: jest.Mock;
  __mockMarkSyncError: jest.Mock;
};
const authMock = jest.requireMock("../../services/auth") as { refresh: jest.Mock };
const localDbMock = jest.requireMock("../../services/localDb") as { __mockGetQueueRowById: jest.Mock };

interface Row {
  id: string;
  tenant_id: string;
  entity_type: string;
  operation: string;
  payload: string;
  endpoint: string;
  http_method: string;
  created_at: string;
  synced_at: string | null;
  sync_error: string | null;
}

function row(overrides: Partial<Row> = {}): Row {
  return {
    id: overrides.id ?? "row-1",
    tenant_id: "org-a",
    entity_type: overrides.entity_type ?? "_test_entity",
    operation: "create",
    payload: JSON.stringify({ foo: "bar" }),
    endpoint: "/api/test-entity",
    http_method: "POST",
    created_at: overrides.created_at ?? "2026-01-01T00:00:00.000Z",
    synced_at: null,
    sync_error: null,
  };
}

function fetchResponse(status: number, body: unknown = {}) {
  return { ok: status >= 200 && status < 300, status, json: async () => body } as Response;
}

beforeEach(() => {
  jest.clearAllMocks();
  useStore.setState({
    auth: { userId: "u1", email: "carer@test.com", role: "staff", organisationSlug: "org-a", accessToken: "tok" },
  });
  global.fetch = jest.fn();
  localDbMock.__mockGetQueueRowById.mockReturnValue(null);
});

it("sends queued rows sequentially, in order, and marks each synced_at on success (FR-012, FR-013)", async () => {
  const rows = [row({ id: "r1", created_at: "2026-01-01T00:00:00.000Z" }), row({ id: "r2", created_at: "2026-01-01T00:00:01.000Z" })];
  offlineQueueMock.__mockGetPending.mockResolvedValue(rows);
  (global.fetch as jest.Mock).mockResolvedValue(fetchResponse(200));

  const result = await syncPendingQueue();

  expect(result).toEqual({ succeeded: 2, failed: 0, conflicted: 0 });
  expect((global.fetch as jest.Mock).mock.calls[0][0]).toBe("http://api.test/api/test-entity");
  expect(offlineQueueMock.__mockMarkSynced).toHaveBeenNthCalledWith(1, "r1");
  expect(offlineQueueMock.__mockMarkSynced).toHaveBeenNthCalledWith(2, "r2");
});

it("leaves a row pending on a transient (500) error, never discarding it (FR-014)", async () => {
  offlineQueueMock.__mockGetPending.mockResolvedValue([row({ id: "r1" })]);
  (global.fetch as jest.Mock).mockResolvedValue(fetchResponse(500));

  const result = await syncPendingQueue();

  expect(result).toEqual({ succeeded: 0, failed: 1, conflicted: 0 });
  expect(offlineQueueMock.__mockMarkSynced).not.toHaveBeenCalled();
  expect(offlineQueueMock.__mockMarkSyncError).toHaveBeenCalledWith("r1", expect.stringContaining("500"));
});

it("leaves a row pending on a network error, never discarding it (FR-014)", async () => {
  offlineQueueMock.__mockGetPending.mockResolvedValue([row({ id: "r1" })]);
  (global.fetch as jest.Mock).mockRejectedValue(new Error("network timeout"));

  const result = await syncPendingQueue();

  expect(result).toEqual({ succeeded: 0, failed: 1, conflicted: 0 });
  expect(offlineQueueMock.__mockMarkSynced).not.toHaveBeenCalled();
});

it("discards a 409 with no registered onConflict handler, marking it synced with a conflict note (FR-014a default)", async () => {
  offlineQueueMock.__mockGetPending.mockResolvedValue([row({ id: "r1", entity_type: "unregistered_entity" })]);
  (global.fetch as jest.Mock).mockResolvedValue(fetchResponse(409, { message: "conflict" }));

  const result = await syncPendingQueue();

  expect(result).toEqual({ succeeded: 0, failed: 0, conflicted: 1 });
  expect(offlineQueueMock.__mockMarkSynced).toHaveBeenCalledWith("r1", expect.stringContaining("conflict"));
});

it("a registered onConflict handler can request a retry instead of the default discard", async () => {
  registerSyncHandler("_test_entity", { onConflict: () => "retry" });
  offlineQueueMock.__mockGetPending.mockResolvedValue([row({ id: "r1" })]);
  (global.fetch as jest.Mock).mockResolvedValue(fetchResponse(409, {}));

  const result = await syncPendingQueue();

  expect(result).toEqual({ succeeded: 0, failed: 1, conflicted: 0 });
  expect(offlineQueueMock.__mockMarkSynced).not.toHaveBeenCalled();
});

it("on 401: refreshes exactly once, retries that row once, and stops the whole run if the retry also fails (FR-015)", async () => {
  authMock.refresh.mockResolvedValue(true);
  offlineQueueMock.__mockGetPending.mockResolvedValue([
    row({ id: "r1" }),
    row({ id: "r2", created_at: "2026-01-01T00:00:02.000Z" }),
  ]);
  (global.fetch as jest.Mock)
    .mockResolvedValueOnce(fetchResponse(401))
    .mockResolvedValueOnce(fetchResponse(401)); // retry also fails

  const result = await syncPendingQueue();

  expect(authMock.refresh).toHaveBeenCalledTimes(1);
  expect(global.fetch).toHaveBeenCalledTimes(2); // original + one retry — row 2 never attempted
  expect(result).toEqual({ succeeded: 0, failed: 1, conflicted: 0 });
  expect(offlineQueueMock.__mockMarkSyncError).toHaveBeenCalledWith("r1", expect.stringContaining("refresh retry"));
});

it("on 401: a successful refresh-and-retry succeeds and the run continues to the next row", async () => {
  authMock.refresh.mockResolvedValue(true);
  offlineQueueMock.__mockGetPending.mockResolvedValue([
    row({ id: "r1" }),
    row({ id: "r2", created_at: "2026-01-01T00:00:02.000Z" }),
  ]);
  (global.fetch as jest.Mock)
    .mockResolvedValueOnce(fetchResponse(401))
    .mockResolvedValueOnce(fetchResponse(200)) // retry of r1 succeeds
    .mockResolvedValueOnce(fetchResponse(200)); // r2 succeeds normally

  const result = await syncPendingQueue();

  expect(result).toEqual({ succeeded: 2, failed: 0, conflicted: 0 });
});

it("50+ synthetic queued rows all eventually sync, strictly in order, none silently dropped (FR-013, SC-003)", async () => {
  const rows = Array.from({ length: 55 }, (_, i) =>
    row({ id: `r${i}`, created_at: `2026-01-01T00:${String(Math.floor(i / 60)).padStart(2, "0")}:${String(i % 60).padStart(2, "0")}.000Z` })
  );
  offlineQueueMock.__mockGetPending.mockResolvedValue(rows);
  (global.fetch as jest.Mock).mockResolvedValue(fetchResponse(200));

  const result = await syncPendingQueue();

  expect(result).toEqual({ succeeded: 55, failed: 0, conflicted: 0 });
  const syncedOrder = offlineQueueMock.__mockMarkSynced.mock.calls.map((c) => c[0]);
  expect(syncedOrder).toEqual(rows.map((r) => r.id));
});

// ── Feature 009 (research.md R3/CHK008, T022a): replay() re-reads the current payload ──

it("re-reads a row's current payload from local storage before transmitting, not the batch snapshot (CHK008)", async () => {
  const staleRow = row({ id: "r1", entity_type: "child_event", payload: JSON.stringify({ endedAt: null }) });
  offlineQueueMock.__mockGetPending.mockResolvedValue([staleRow]);
  // Simulates a sleep-end merge landing after the batch read but before this row's send —
  // getQueueRowById returns the already-merged payload, not the stale one syncPendingQueue read.
  localDbMock.__mockGetQueueRowById.mockReturnValue({ ...staleRow, payload: JSON.stringify({ endedAt: "2026-01-01T01:00:00.000Z" }) });
  (global.fetch as jest.Mock).mockResolvedValue(fetchResponse(200));

  await syncPendingQueue();

  const [, requestInit] = (global.fetch as jest.Mock).mock.calls[0];
  expect(JSON.parse(requestInit.body)).toEqual({ endedAt: "2026-01-01T01:00:00.000Z" });
});

// ── Feature 009 (FR-014a/research.md R10, T022b): a 422 is a permanent rejection ──

it("marks a 422 with a distinguishable 'rejected:' prefix rather than retrying it as transient (FR-014a)", async () => {
  offlineQueueMock.__mockGetPending.mockResolvedValue([row({ id: "r1", entity_type: "child_event" })]);
  (global.fetch as jest.Mock).mockResolvedValue(fetchResponse(422, { errorKey: "errors.validation" }));

  const result = await syncPendingQueue();

  expect(result).toEqual({ succeeded: 0, failed: 1, conflicted: 0 });
  expect(offlineQueueMock.__mockMarkSynced).not.toHaveBeenCalled();
  expect(offlineQueueMock.__mockMarkSyncError).toHaveBeenCalledWith("r1", "rejected: errors.validation");
});

// ── Feature 009c (T029, research.md R6): a batch's 2xx can still carry per-child failures ──

it("a child_event_batch 2xx response with a non-empty errors array is marked 'partial:' and counted as failed, not synced", async () => {
  offlineQueueMock.__mockGetPending.mockResolvedValue([row({ id: "r1", entity_type: "child_event_batch" })]);
  (global.fetch as jest.Mock).mockResolvedValue(
    fetchResponse(200, { created: [{ childId: "c1", eventId: "e1" }], errors: [{ childId: "c2", reason: "not_present" }] })
  );

  const result = await syncPendingQueue();

  expect(result).toEqual({ succeeded: 0, failed: 1, conflicted: 0 });
  expect(offlineQueueMock.__mockMarkSynced).not.toHaveBeenCalled();
  expect(offlineQueueMock.__mockMarkSyncError).toHaveBeenCalledWith(
    "r1",
    expect.stringContaining("partial:")
  );
});

it("a child_event_batch 2xx response with an empty errors array is marked synced normally", async () => {
  offlineQueueMock.__mockGetPending.mockResolvedValue([row({ id: "r1", entity_type: "child_event_batch" })]);
  (global.fetch as jest.Mock).mockResolvedValue(
    fetchResponse(200, { created: [{ childId: "c1", eventId: "e1" }], errors: [] })
  );

  const result = await syncPendingQueue();

  expect(result).toEqual({ succeeded: 1, failed: 0, conflicted: 0 });
  expect(offlineQueueMock.__mockMarkSynced).toHaveBeenCalledWith("r1");
  expect(offlineQueueMock.__mockMarkSyncError).not.toHaveBeenCalled();
});
