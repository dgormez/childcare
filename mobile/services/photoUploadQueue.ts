/**
 * photoUploadQueue.ts — local photo-upload queue for group-activity photos (feature 009b,
 * FR-012). A dedicated queue rather than routing through offlineQueue.ts/syncEngine.ts: a photo
 * upload is `multipart/form-data` carrying binary image bytes, which syncEngine's JSON-body
 * `replay()` cannot express. Enqueue is by local file URI; the uploader runs on reconnect/
 * foreground (wired in app/(app)/_layout.tsx, same triggers as syncPendingQueue) and on demand
 * right after a successful online create.
 */
import { Platform } from "react-native";
import {
  insertPhotoUploadQueueRow,
  getPendingPhotoUploadQueueRows,
  markPhotoUploadQueueRowUploaded,
  markPhotoUploadQueueRowError,
} from "./localDb";
import { currentTenantId, getPending as getPendingWrites } from "./offlineQueue";
import { getApiBaseUrl } from "./apiClient";
import { getDeviceToken } from "./deviceTokenStorage";
import { useStore } from "../store/useStore";

function generateId(): string {
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
    const r = Math.floor(Math.random() * 16);
    const v = c === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

export async function enqueuePhotoUpload(activityId: string, localUri: string, caption?: string | null): Promise<void> {
  const tenantId = currentTenantId();
  if (!tenantId) throw new Error("Cannot queue a photo upload without an active session");

  insertPhotoUploadQueueRow({
    id: generateId(),
    tenant_id: tenantId,
    activity_id: activityId,
    local_uri: localUri,
    caption: caption ?? null,
    created_at: new Date().toISOString(),
  });
}

/** True while the activity's own create is still an unsynced offline_queue row — the photo
 * endpoint 404s until the server actually has the activity, so uploading must wait. */
async function activityCreateStillPending(activityId: string): Promise<boolean> {
  const pending = await getPendingWrites();
  return pending.some((row) => {
    if (row.entity_type !== "group_activity" || row.operation !== "create") return false;
    try {
      return JSON.parse(row.payload).id === activityId;
    } catch {
      return false;
    }
  });
}

async function uploadOne(activityId: string, localUri: string, caption: string | null): Promise<Response> {
  const deviceToken = await getDeviceToken();
  const token = deviceToken ?? useStore.getState().auth?.accessToken;

  const form = new FormData();
  const filename = localUri.split("/").pop() ?? "photo.jpg";
  // React Native's fetch/FormData accepts a { uri, name, type } object in place of a Blob —
  // reading the file into memory first isn't needed on this platform.
  form.append("photo", { uri: localUri, name: filename, type: "image/jpeg" } as unknown as Blob);
  if (caption) form.append("caption", caption);

  return fetch(`${getApiBaseUrl()}/api/group-activities/${activityId}/photos`, {
    method: "POST",
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    body: form,
  });
}

/** Replays every queued photo for the current tenant. Skipped (left pending) rows: the parent
 * activity hasn't synced yet. Permanently discarded: the activity was deleted server-side
 * (404) — spec.md Edge Cases, a photo for a deleted activity is rejected, not retried forever. */
/** Read by GroupTimeline.tsx to show a per-activity "Foto's worden geüpload…" indicator
 * (spec.md UX Requirements) for photos still queued locally. */
export function getPendingPhotoCountForActivity(activityId: string): number {
  const tenantId = currentTenantId();
  if (!tenantId) return 0;
  return getPendingPhotoUploadQueueRows(tenantId).filter((r) => r.activity_id === activityId).length;
}

export async function uploadPendingPhotos(): Promise<void> {
  if (Platform.OS === "web") return; // no local file system to read queued URIs from
  const tenantId = currentTenantId();
  if (!tenantId) return;

  for (const row of getPendingPhotoUploadQueueRows(tenantId)) {
    if (await activityCreateStillPending(row.activity_id)) continue;

    try {
      const response = await uploadOne(row.activity_id, row.local_uri, row.caption);
      if (response.ok || response.status === 404) {
        markPhotoUploadQueueRowUploaded(row.id);
      } else {
        markPhotoUploadQueueRowError(row.id, `upload failed: ${response.status}`);
      }
    } catch (e) {
      markPhotoUploadQueueRowError(row.id, e instanceof Error ? e.message : "network error");
    }
  }
}
