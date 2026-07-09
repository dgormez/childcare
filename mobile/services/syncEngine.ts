/**
 * syncEngine.ts — replays the offline queue against the live API (contracts/mobile-offline-sync.md).
 *
 * Uses the raw `fetch()` (not `apiClient`) so this module owns the entire 401 story itself
 * (FR-015: refresh once, retry that one row once, stop the whole run if the retry also fails)
 * rather than double-firing apiClient's own independent 401-retry-once-per-request middleware.
 */
import { getPending, markSynced, markSyncError } from "./offlineQueue";
import type { QueueRow } from "./offlineQueue";
import { refresh } from "./auth";
import { getApiBaseUrl } from "./apiClient";
import { getDeviceToken } from "./deviceTokenStorage";
import { handleDeviceRejection } from "./deviceAuth";
import { setLastSyncTime, getQueueRowById } from "./localDb";
import { useStore } from "../store/useStore";

export interface SyncHandler {
  onBeforeEnqueue?: (existingPending: QueueRow[], newEntry: unknown) => unknown;
  onConflict?: (row: QueueRow, serverResponse: unknown) => "discard" | "retry";
}

export interface SyncResult {
  succeeded:  number;
  failed:     number;
  conflicted: number;
}

const handlers = new Map<string, SyncHandler>();

export function registerSyncHandler(entityType: string, handler: SyncHandler): void {
  handlers.set(entityType, handler);
}

let syncing = false;
type SyncListener = () => void;
const listeners = new Set<SyncListener>();

function notify() {
  listeners.forEach((listener) => listener());
}

export function subscribeSyncState(listener: SyncListener): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function isSyncingNow(): boolean {
  return syncing;
}

/**
 * Feature 008a: a paired tablet's queued rows (entity_type = 'room_shift') must replay with
 * the device token, not any user-session token — mirrors apiClient.ts's own priority (device
 * token first once paired). research.md R3: replay never rotates the token mid-burst — every
 * queued row in one run carries whichever token was current when replay() first reads it, all
 * still valid, since rotation only swaps the *stored* token for the *next* fresh call.
 *
 * Feature 009 (research.md R3/CHK008): re-reads the row's current `payload` from local storage
 * immediately before transmitting, rather than trusting the in-memory batch snapshot
 * syncPendingQueue() read at the start of this run — closes a race where a sleep-end merge
 * (childEvents.ts's endSleepEvent) landing after the batch read but before this row's send would
 * otherwise ship the stale, pre-merge payload. Falls back to the passed-in row if the row has
 * since been deleted/synced (defensive only; markSynced never deletes rows).
 */
async function replay(row: QueueRow): Promise<Response> {
  const deviceToken = await getDeviceToken();
  const token = deviceToken ?? useStore.getState().auth?.accessToken;
  const current = getQueueRowById(row.id) ?? row;
  return fetch(`${getApiBaseUrl()}${current.endpoint}`, {
    method: current.http_method,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: current.payload,
  });
}

export async function syncPendingQueue(): Promise<SyncResult> {
  syncing = true;
  notify();

  let succeeded = 0;
  let failed = 0;
  let conflicted = 0;

  try {
    const rows = await getPending();

    for (const row of rows) {
      let response: Response;
      try {
        response = await replay(row);
      } catch (e) {
        await markSyncError(row.id, e instanceof Error ? e.message : "network error");
        failed += 1;
        continue; // transient — retried on the next sync trigger (FR-014)
      }

      if (response.ok) {
        await markSynced(row.id);
        succeeded += 1;
        continue;
      }

      if (response.status === 409) {
        const serverBody = await response.json().catch(() => null);
        const decision = handlers.get(row.entity_type)?.onConflict?.(row, serverBody) ?? "discard";
        if (decision === "retry") {
          await markSyncError(row.id, "conflict — retry requested");
          failed += 1;
        } else {
          // FR-014a default: server wins — discard the queued write, mark synced with a note.
          await markSynced(row.id, "conflict — server wins, discarded");
          conflicted += 1;
        }
        continue;
      }

      if (response.status === 401) {
        // Feature 008a FR-021/022: a device-authenticated row's 401 means device.revoked or
        // device.token_expired, never a user-session refresh scenario (there is no user
        // session for a paired tablet to refresh). Clear local credentials — _layout.tsx's
        // redirect logic reacts to the store's device state going null — and stop the run;
        // this row and any remaining ones are rejected server-side either way once revoked.
        const deviceToken = await getDeviceToken();
        if (deviceToken) {
          await markSyncError(row.id, "device revoked or token expired during sync");
          await handleDeviceRejection();
          failed += 1;
          break;
        }

        const refreshed = await refresh();
        if (!refreshed) {
          await markSyncError(row.id, "session expired during sync");
          failed += 1;
          break; // FR-015: stop the whole run, not just this row
        }

        let retryResponse: Response;
        try {
          retryResponse = await replay(row);
        } catch (e) {
          await markSyncError(row.id, e instanceof Error ? e.message : "network error");
          failed += 1;
          break;
        }

        if (retryResponse.ok) {
          await markSynced(row.id);
          succeeded += 1;
          continue;
        }

        await markSyncError(row.id, `sync failed after refresh retry: ${retryResponse.status}`);
        failed += 1;
        break; // FR-015: the retried call also failed — stop the whole run
      }

      if (response.status === 422) {
        // Feature 009 FR-014a (analyze finding C1/research.md R10): a genuine validation
        // rejection is a permanent failure, not a transient one — retrying it forever would
        // never succeed, since nothing about the row or the request changes between attempts.
        // The "rejected: " prefix is what EventTimeline reads to render a "needs review" state
        // distinct from ordinary "pending sync".
        const serverBody = await response.json().catch(() => null);
        const errorKey = (serverBody as { errorKey?: string } | null)?.errorKey ?? "errors.validation";
        await markSyncError(row.id, `rejected: ${errorKey}`);
        failed += 1;
        continue;
      }

      // Transient error (5xx or similar) — leave pending, continue to the next row.
      await markSyncError(row.id, `sync failed: ${response.status}`);
      failed += 1;
    }

    setLastSyncTime(new Date());
    return { succeeded, failed, conflicted };
  } finally {
    syncing = false;
    notify();
  }
}
