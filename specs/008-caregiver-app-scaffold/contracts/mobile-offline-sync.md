# Contract: Mobile Offline Queue & Sync Engine (internal interface)

This is the interface features 009 (child events) and 010 (attendance) build against — not an HTTP contract, but the internal TypeScript surface this feature ships and future features consume unchanged.

## `services/offlineQueue.ts`

```ts
function enqueue(entry: {
  entityType: string;
  operation: 'create' | 'update' | 'delete';
  payload: unknown;
  endpoint: string;
  httpMethod: 'POST' | 'PATCH' | 'DELETE';
}): Promise<string>; // returns the client-generated queue row id

function getPending(): Promise<QueueRow[]>; // ordered by created_at ASC
function markSynced(id: string, note?: string): Promise<void>;
function markSyncError(id: string, error: string): Promise<void>;
```

## `services/syncEngine.ts`

```ts
type SyncHandler = {
  // Called before replay, for an entity type to merge a not-yet-synced create with a later
  // update (e.g. feature 009's sleep-end merge) — optional, defaults to "always queue separately".
  onBeforeEnqueue?: (existingPending: QueueRow[], newEntry: QueueEntryDraft) => QueueEntryDraft | 'merge-into-existing';

  // Called when the server responds 409 for this entity type — optional, defaults to
  // FR-014a's server-wins behavior (discard, mark synced with a conflict note).
  onConflict?: (row: QueueRow, serverResponse: unknown) => 'discard' | 'retry';
};

function registerSyncHandler(entityType: string, handler: SyncHandler): void;
function syncPendingQueue(): Promise<{ succeeded: number; failed: number; conflicted: number }>;
```

`syncPendingQueue()` is invoked automatically by the app shell on exactly three triggers (FR-012a) — network reconnect (`useNetworkStatus`), app foreground, and pull-to-refresh — never on a background timer. It processes `getPending()` rows strictly sequentially (FR-013): for each row, replay `httpMethod` against `endpoint` with `payload` via the shared API client; on success call `markSynced`; on a 409 call the entity type's `onConflict` handler if registered, else the FR-014a default; on a 401 attempt exactly one token refresh then retry that one row once (FR-015), stopping the whole sync run (not just that row) if the refreshed retry also fails; on any other error call `markSyncError` and continue to the next row (that row remains pending for the next sync trigger).

## `hooks/useSyncStatus.ts`

```ts
function useSyncStatus(): { pendingCount: number; lastSyncedAt: string | null; isSyncing: boolean };
```

## `hooks/useNetworkStatus.ts`

```ts
function useNetworkStatus(): { isConnected: boolean };
```

## This feature's own test double

`__tests__/services/syncEngine.test.ts` registers `registerSyncHandler('_test_entity', {...})` purely to exercise ordering, retry, and the FR-014a conflict default end-to-end against a real (test-mode) SQLite database — no production code ever registers `'_test_entity'` (research.md R4).
