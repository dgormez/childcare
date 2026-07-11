/**
 * localDb.ts — SQLite persistence layer (expo-sqlite).
 *
 * Schema:
 *   config         — key/value store for auth state, last-sync timestamp
 *   offline_queue  — caregiver actions taken offline, pending sync (data-model.md)
 *   read_cache     — cached API reads, available offline (data-model.md)
 */
import { Platform } from "react-native";
import * as SQLite from "expo-sqlite";
import type { SQLiteDatabase } from "expo-sqlite";

export const db: SQLiteDatabase = Platform.OS !== "web"
  ? SQLite.openDatabaseSync("childcare.db")
  : (null as unknown as SQLiteDatabase);

// ── Schema ────────────────────────────────────────────────────────────────────

export function initDb() {
  if (Platform.OS === "web") return;
  db.execSync(`
    PRAGMA journal_mode = WAL;

    CREATE TABLE IF NOT EXISTS config (
      key   TEXT PRIMARY KEY,
      value TEXT NOT NULL
    );

    CREATE TABLE IF NOT EXISTS offline_queue (
      id          TEXT PRIMARY KEY,
      tenant_id   TEXT NOT NULL,
      entity_type TEXT NOT NULL,
      operation   TEXT NOT NULL,
      payload     TEXT NOT NULL,
      endpoint    TEXT NOT NULL,
      http_method TEXT NOT NULL,
      created_at  TEXT NOT NULL,
      synced_at   TEXT,
      sync_error  TEXT
    );

    CREATE INDEX IF NOT EXISTS idx_offline_queue_tenant_created ON offline_queue(tenant_id, created_at ASC);

    CREATE TABLE IF NOT EXISTS read_cache (
      cache_key  TEXT PRIMARY KEY,
      tenant_id  TEXT NOT NULL,
      data       TEXT NOT NULL,
      cached_at  TEXT NOT NULL,
      expires_at TEXT
    );

    CREATE INDEX IF NOT EXISTS idx_read_cache_tenant ON read_cache(tenant_id);

    CREATE TABLE IF NOT EXISTS photo_upload_queue (
      id           TEXT PRIMARY KEY,
      tenant_id    TEXT NOT NULL,
      activity_id  TEXT NOT NULL,
      local_uri    TEXT NOT NULL,
      caption      TEXT,
      created_at   TEXT NOT NULL,
      uploaded_at  TEXT,
      upload_error TEXT
    );

    CREATE INDEX IF NOT EXISTS idx_photo_upload_queue_tenant_created ON photo_upload_queue(tenant_id, created_at ASC);
  `);
}

// ── Config ────────────────────────────────────────────────────────────────────

export function getConfigValue(key: string): string | null {
  if (Platform.OS === "web") return localStorage.getItem(key);
  return db.getFirstSync<{ value: string }>(
    "SELECT value FROM config WHERE key = ?", [key]
  )?.value ?? null;
}

export function setConfigValue(key: string, value: string) {
  if (Platform.OS === "web") { localStorage.setItem(key, value); return; }
  db.runSync("INSERT OR REPLACE INTO config (key, value) VALUES (?, ?)", [key, value]);
}

export function deleteConfigValue(key: string) {
  if (Platform.OS === "web") { localStorage.removeItem(key); return; }
  db.runSync("DELETE FROM config WHERE key = ?", [key]);
}

// ── Sync timestamp ────────────────────────────────────────────────────────────

export function getLastSyncTime(): Date | null {
  const v = getConfigValue("lastSyncAt");
  return v ? new Date(v) : null;
}

export function setLastSyncTime(d: Date) {
  setConfigValue("lastSyncAt", d.toISOString());
}

// ── Read cache (FR-009/FR-011/FR-015a) ───────────────────────────────────────

export function getCacheRow(cacheKey: string, tenantId: string): { data: string } | null {
  if (Platform.OS === "web") return null;
  return db.getFirstSync<{ data: string }>(
    "SELECT data FROM read_cache WHERE cache_key = ? AND tenant_id = ?", [cacheKey, tenantId]
  ) ?? null;
}

export function setCacheRow(cacheKey: string, tenantId: string, data: string) {
  if (Platform.OS === "web") return;
  db.runSync(
    "INSERT OR REPLACE INTO read_cache (cache_key, tenant_id, data, cached_at, expires_at) VALUES (?, ?, ?, ?, NULL)",
    [cacheKey, tenantId, data, new Date().toISOString()]
  );
}

// ── Offline queue (FR-012/FR-013/FR-014) ─────────────────────────────────────

export interface QueueRow {
  id:          string;
  tenant_id:   string;
  entity_type: string;
  operation:   string;
  payload:     string;
  endpoint:    string;
  http_method: string;
  created_at:  string;
  synced_at:   string | null;
  sync_error:  string | null;
}

export function insertQueueRow(row: Omit<QueueRow, "synced_at" | "sync_error">) {
  if (Platform.OS === "web") return;
  db.runSync(
    `INSERT INTO offline_queue (id, tenant_id, entity_type, operation, payload, endpoint, http_method, created_at, synced_at, sync_error)
     VALUES (?, ?, ?, ?, ?, ?, ?, ?, NULL, NULL)`,
    [row.id, row.tenant_id, row.entity_type, row.operation, row.payload, row.endpoint, row.http_method, row.created_at]
  );
}

export function getPendingQueueRows(tenantId: string): QueueRow[] {
  if (Platform.OS === "web") return [];
  return db.getAllSync<QueueRow>(
    "SELECT * FROM offline_queue WHERE tenant_id = ? AND synced_at IS NULL ORDER BY created_at ASC",
    [tenantId]
  );
}

/** Feature 009 (research.md R3/CHK008): syncEngine.ts's replay() re-reads a row's current
 * payload immediately before transmitting, rather than trusting the in-memory batch snapshot
 * getPendingQueueRows() returned at the start of a sync run — closes a race where a sleep-end
 * merge landing after the batch read but before that row's send would otherwise ship stale data. */
export function getQueueRowById(id: string): QueueRow | null {
  if (Platform.OS === "web") return null;
  return db.getFirstSync<QueueRow>("SELECT * FROM offline_queue WHERE id = ?", [id]) ?? null;
}

/** Feature 009 (research.md R3): merges an in-progress sleep event's end into its still-queued
 * create row's payload, in place — used only while the row is still unsynced (synced_at IS
 * NULL); the caller falls back to a normal queued PATCH once it isn't. Returns true if a row was
 * actually updated (i.e. it was still pending at the moment of the call). */
export function updateQueueRowPayload(id: string, payload: string): boolean {
  if (Platform.OS === "web") return false;
  const result = db.runSync("UPDATE offline_queue SET payload = ? WHERE id = ? AND synced_at IS NULL", [payload, id]);
  return result.changes > 0;
}

export function markQueueRowSynced(id: string, note: string | null) {
  if (Platform.OS === "web") return;
  db.runSync("UPDATE offline_queue SET synced_at = ?, sync_error = ? WHERE id = ?", [new Date().toISOString(), note, id]);
}

export function markQueueRowSyncError(id: string, error: string) {
  if (Platform.OS === "web") return;
  db.runSync("UPDATE offline_queue SET sync_error = ? WHERE id = ?", [error, id]);
}

/** Wipes all offline-queue/read-cache rows for a tenant, plus session config — called on
 * logout (FR-019) so a different caregiver signing in on the same device starts clean. */
export function deleteLocalTenantData(tenantId: string) {
  if (Platform.OS === "web") return;
  db.runSync("DELETE FROM offline_queue WHERE tenant_id = ?", [tenantId]);
  db.runSync("DELETE FROM read_cache WHERE tenant_id = ?", [tenantId]);
  db.runSync("DELETE FROM photo_upload_queue WHERE tenant_id = ?", [tenantId]);
  deleteConfigValue("userId");
  deleteConfigValue("userEmail");
  deleteConfigValue("userRole");
  deleteConfigValue("organisationSlug");
  deleteConfigValue("lastSyncAt");
}

// ── Photo upload queue (feature 009b, FR-012) ────────────────────────────────
// Separate from offline_queue: a group-activity photo upload is a multipart/form-data request
// carrying binary image bytes, which syncEngine.ts's JSON-body replay() cannot express — so
// this queue is replayed by photoUploadQueue.ts's own uploader, not syncEngine.ts.

export interface PhotoUploadQueueRow {
  id:           string;
  tenant_id:    string;
  activity_id:  string;
  local_uri:    string;
  caption:      string | null;
  created_at:   string;
  uploaded_at:  string | null;
  upload_error: string | null;
}

export function insertPhotoUploadQueueRow(row: Omit<PhotoUploadQueueRow, "uploaded_at" | "upload_error">) {
  if (Platform.OS === "web") return;
  db.runSync(
    `INSERT INTO photo_upload_queue (id, tenant_id, activity_id, local_uri, caption, created_at, uploaded_at, upload_error)
     VALUES (?, ?, ?, ?, ?, ?, NULL, NULL)`,
    [row.id, row.tenant_id, row.activity_id, row.local_uri, row.caption, row.created_at]
  );
}

export function getPendingPhotoUploadQueueRows(tenantId: string): PhotoUploadQueueRow[] {
  if (Platform.OS === "web") return [];
  return db.getAllSync<PhotoUploadQueueRow>(
    "SELECT * FROM photo_upload_queue WHERE tenant_id = ? AND uploaded_at IS NULL ORDER BY created_at ASC",
    [tenantId]
  );
}

export function markPhotoUploadQueueRowUploaded(id: string) {
  if (Platform.OS === "web") return;
  db.runSync("UPDATE photo_upload_queue SET uploaded_at = ?, upload_error = NULL WHERE id = ?", [new Date().toISOString(), id]);
}

export function markPhotoUploadQueueRowError(id: string, error: string) {
  if (Platform.OS === "web") return;
  db.runSync("UPDATE photo_upload_queue SET upload_error = ? WHERE id = ?", [error, id]);
}
