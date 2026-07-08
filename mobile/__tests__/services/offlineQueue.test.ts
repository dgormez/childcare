import { useStore } from "../../store/useStore";

interface MockRow {
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

jest.mock("../../services/localDb", () => {
  const mockRows: MockRow[] = [];
  return {
    insertQueueRow: (row: Omit<MockRow, "synced_at" | "sync_error">) => {
      mockRows.push({ ...row, synced_at: null, sync_error: null });
    },
    getPendingQueueRows: (tenantId: string) =>
      mockRows
        .filter((r) => r.tenant_id === tenantId && r.synced_at === null)
        .sort((a, b) => a.created_at.localeCompare(b.created_at)),
    markQueueRowSynced: (id: string, note: string | null) => {
      const row = mockRows.find((r) => r.id === id);
      if (row) { row.synced_at = new Date().toISOString(); row.sync_error = note; }
    },
    markQueueRowSyncError: (id: string, error: string) => {
      const row = mockRows.find((r) => r.id === id);
      if (row) row.sync_error = error;
    },
    // Feature 008a: offlineQueue.ts's currentTenantId() falls back to this when useStore's
    // in-memory auth slice is null (a paired tablet's cold-start case) — null here matches
    // "no active session" for these tests.
    getConfigValue: (_key: string) => null,
    __mockRows: mockRows,
  };
});

import { enqueue, getPending, markSynced, markSyncError } from "../../services/offlineQueue";

const localDbMock = jest.requireMock("../../services/localDb") as { __mockRows: MockRow[] };

beforeEach(() => {
  localDbMock.__mockRows.length = 0;
  useStore.setState({
    auth: { userId: "u1", email: "carer@test.com", role: "staff", organisationSlug: "org-a", accessToken: "tok" },
  });
});

it("enqueue() records a pending row (synced_at = NULL); getPending() returns rows ordered by created_at ASC", async () => {
  const id1 = await enqueue({ entityType: "_test_entity", operation: "create", payload: { a: 1 }, endpoint: "/api/test-entity", httpMethod: "POST" });
  const id2 = await enqueue({ entityType: "_test_entity", operation: "create", payload: { a: 2 }, endpoint: "/api/test-entity", httpMethod: "POST" });

  const pending = await getPending();

  expect(pending.map((r) => r.id)).toEqual([id1, id2]);
  expect(pending.every((r) => r.synced_at === null)).toBe(true);
  expect(pending[0].entity_type).toBe("_test_entity");
  expect(JSON.parse(pending[0].payload)).toEqual({ a: 1 });
});

it("enqueue() rejects when there is no active session", async () => {
  useStore.setState({ auth: null });

  await expect(
    enqueue({ entityType: "_test_entity", operation: "create", payload: {}, endpoint: "/api/test-entity", httpMethod: "POST" })
  ).rejects.toThrow();
});

it("getPending() resolves to an empty array (not a rejection) when there is no active session", async () => {
  useStore.setState({ auth: null });

  await expect(getPending()).resolves.toEqual([]);
});

it("markSynced()/markSyncError() update the underlying row", async () => {
  const id = await enqueue({ entityType: "_test_entity", operation: "create", payload: {}, endpoint: "/api/test-entity", httpMethod: "POST" });

  await markSynced(id, "conflict — server wins, discarded");
  expect(localDbMock.__mockRows.find((r) => r.id === id)?.sync_error).toBe("conflict — server wins, discarded");
  expect(localDbMock.__mockRows.find((r) => r.id === id)?.synced_at).not.toBeNull();

  const id2 = await enqueue({ entityType: "_test_entity", operation: "create", payload: {}, endpoint: "/api/test-entity", httpMethod: "POST" });
  await markSyncError(id2, "network timeout");
  expect(localDbMock.__mockRows.find((r) => r.id === id2)?.sync_error).toBe("network timeout");
  expect(localDbMock.__mockRows.find((r) => r.id === id2)?.synced_at).toBeNull();
});
