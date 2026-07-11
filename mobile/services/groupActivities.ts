/**
 * groupActivities.ts — create/list calls plus the offline sync handler for entity_type =
 * 'group_activity' (feature 009b, contracts/group-activities-api.md). Photos are a distinct
 * queue (photoUploadQueue.ts) — this module only ever sends JSON, never multipart bodies.
 */
import { apiClient } from "./apiClient";
import { enqueue } from "./offlineQueue";
import { registerSyncHandler } from "./syncEngine";
import type { GroupActivityResponse, GroupActivityType, GroupTimelineResponse } from "../types";

type ErrorBody = { errorKey?: string; fieldErrors?: Record<string, string> };

function generateId(): string {
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
    const r = Math.floor(Math.random() * 16);
    const v = c === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

export interface CreateGroupActivityInput {
  activityType: GroupActivityType;
  title:        string;
  description?: string | null;
  occurredAt:   string;
}

/**
 * FR-012: created immediately in the local UI whether online or offline. The client-generated
 * `id` is what the photo-upload queue later attaches photos against (photoUploadQueue.ts), and
 * is also what makes a retried/offline-queued create idempotent server-side.
 */
export async function createGroupActivity(input: CreateGroupActivityInput, isConnected: boolean): Promise<GroupActivityResponse> {
  const id = generateId();
  const body = {
    id,
    activityType: input.activityType,
    title: input.title,
    description: input.description ?? null,
    occurredAt: input.occurredAt,
  };

  if (isConnected) {
    const result = await apiClient.POST("/api/group-activities", { body });
    if (result.response.ok) return result.data as unknown as GroupActivityResponse;
    const errorBody = result.error as ErrorBody | undefined;
    throw new Error(errorBody?.errorKey ?? "errors.network");
  }

  await enqueue({
    entityType: "group_activity",
    operation: "create",
    payload: body,
    endpoint: "/api/group-activities",
    httpMethod: "POST",
  });

  const now = new Date().toISOString();
  return { ...body, groupId: "", recordedBy: [], photos: [], createdAt: now };
}

export async function getGroupTimeline(groupId: string, date?: string): Promise<GroupTimelineResponse> {
  const result = await apiClient.GET("/api/group-activities/timeline", { params: { query: { groupId, date } } });
  if (!result.response.ok) throw new Error("errors.group_activities.timeline_failed");
  return result.data as unknown as GroupTimelineResponse;
}

// Group activities are append-only from this client's perspective (no update path, spec.md
// FR-014) — a 409 conflict is not actually expected in practice, same reasoning as
// childEvents.ts's registration; onConflict exists only to satisfy the SyncHandler shape.
registerSyncHandler("group_activity", {
  onConflict: () => "discard",
});
