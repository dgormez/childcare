/**
 * offlineQueue.ts — generic offline write queue (contracts/mobile-offline-sync.md).
 * No entity_type handlers are registered by this feature; features 009/010 register their
 * own against syncEngine.ts. Only a synthetic `_test_entity` value is used, by this feature's
 * own tests (research.md R4).
 */
import { insertQueueRow, getPendingQueueRows, markQueueRowSynced, markQueueRowSyncError, getConfigValue } from "./localDb";
import type { QueueRow } from "./localDb";
import { useStore } from "../store/useStore";

export type { QueueRow };

export interface QueueEntryDraft {
  entityType: string;
  operation:  "create" | "update" | "delete";
  payload:    unknown;
  endpoint:   string;
  httpMethod: "POST" | "PATCH" | "DELETE";
}

function generateId(): string {
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
    const r = Math.floor(Math.random() * 16);
    const v = c === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

/**
 * Feature 008a: a paired tablet skips tryRestoreSession() entirely on cold start (device
 * pairing takes priority — app/_layout.tsx), so useStore's in-memory `auth` slice stays null
 * for the whole room-mode session after a restart. organisationSlug is also mirrored into the
 * persisted config table by both login() and pairDevice()'s prerequisite login step, so that's
 * the reliable source here — the in-memory store is checked first only because it's already
 * live (no config-table read) whenever a session *is* active.
 */
export function currentTenantId(): string | null {
  return useStore.getState().auth?.organisationSlug ?? getConfigValue("organisationSlug");
}

export async function enqueue(entry: QueueEntryDraft): Promise<string> {
  const tenantId = currentTenantId();
  if (!tenantId) throw new Error("Cannot queue an offline write without an active session");

  const id = generateId();
  insertQueueRow({
    id,
    tenant_id:   tenantId,
    entity_type: entry.entityType,
    operation:   entry.operation,
    payload:     JSON.stringify(entry.payload),
    endpoint:    entry.endpoint,
    http_method: entry.httpMethod,
    created_at:  new Date().toISOString(),
  });
  return id;
}

/** Returns [] (rather than throwing) when there's no active session — callers like
 * syncPendingQueue()/useSyncStatus() may legitimately run during a transitional
 * unauthenticated moment (e.g. mid-logout) and should degrade to "nothing pending". */
export async function getPending(): Promise<QueueRow[]> {
  const tenantId = currentTenantId();
  if (!tenantId) return [];
  return getPendingQueueRows(tenantId);
}

export async function markSynced(id: string, note?: string): Promise<void> {
  markQueueRowSynced(id, note ?? null);
}

export async function markSyncError(id: string, error: string): Promise<void> {
  markQueueRowSyncError(id, error);
}
