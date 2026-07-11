// Web shim — expo-sqlite's WASM worker doesn't resolve under Metro web bundler.
// All operations are no-ops; the app relies on API data on web.
import type { QueueRow, PhotoUploadQueueRow } from "./localDb";

const store = new Map<string, string>();
let queueRows: QueueRow[] = [];
let photoUploadQueueRows: PhotoUploadQueueRow[] = [];

export function initDb() {}

export function getConfigValue(key: string): string | null {
  return store.get(key) ?? null;
}

export function setConfigValue(key: string, value: string) {
  store.set(key, value);
}

export function deleteConfigValue(key: string) {
  store.delete(key);
}

export function getLastSyncTime(): Date | null {
  const v = store.get("lastSyncAt");
  return v ? new Date(v) : null;
}

export function setLastSyncTime(d: Date) {
  store.set("lastSyncAt", d.toISOString());
}

export function deleteLocalTenantData(tenantId: string) {
  store.clear();
  queueRows = queueRows.filter((r) => r.tenant_id !== tenantId);
  photoUploadQueueRows = photoUploadQueueRows.filter((r) => r.tenant_id !== tenantId);
}

export function insertPhotoUploadQueueRow(row: Omit<PhotoUploadQueueRow, "uploaded_at" | "upload_error">) {
  photoUploadQueueRows.push({ ...row, uploaded_at: null, upload_error: null });
}

export function getPendingPhotoUploadQueueRows(tenantId: string): PhotoUploadQueueRow[] {
  return photoUploadQueueRows
    .filter((r) => r.tenant_id === tenantId && r.uploaded_at === null)
    .sort((a, b) => a.created_at.localeCompare(b.created_at));
}

export function markPhotoUploadQueueRowUploaded(id: string) {
  const row = photoUploadQueueRows.find((r) => r.id === id);
  if (row) { row.uploaded_at = new Date().toISOString(); row.upload_error = null; }
}

export function markPhotoUploadQueueRowError(id: string, error: string) {
  const row = photoUploadQueueRows.find((r) => r.id === id);
  if (row) row.upload_error = error;
}

export function getCacheRow(cacheKey: string, tenantId: string): { data: string } | null {
  const v = store.get(`cache:${tenantId}:${cacheKey}`);
  return v ? { data: v } : null;
}

export function setCacheRow(cacheKey: string, tenantId: string, data: string) {
  store.set(`cache:${tenantId}:${cacheKey}`, data);
}

export function insertQueueRow(row: Omit<QueueRow, "synced_at" | "sync_error">) {
  queueRows.push({ ...row, synced_at: null, sync_error: null });
}

export function getPendingQueueRows(tenantId: string): QueueRow[] {
  return queueRows
    .filter((r) => r.tenant_id === tenantId && r.synced_at === null)
    .sort((a, b) => a.created_at.localeCompare(b.created_at));
}

export function markQueueRowSynced(id: string, note: string | null) {
  const row = queueRows.find((r) => r.id === id);
  if (row) { row.synced_at = new Date().toISOString(); row.sync_error = note; }
}

export function markQueueRowSyncError(id: string, error: string) {
  const row = queueRows.find((r) => r.id === id);
  if (row) row.sync_error = error;
}

export function getQueueRowById(id: string): QueueRow | null {
  return queueRows.find((r) => r.id === id) ?? null;
}

export function updateQueueRowPayload(id: string, payload: string): boolean {
  const row = queueRows.find((r) => r.id === id && r.synced_at === null);
  if (!row) return false;
  row.payload = payload;
  return true;
}
