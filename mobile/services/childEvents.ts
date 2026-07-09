/**
 * childEvents.ts — create/list/update/delete calls plus the offline sync handler for
 * entity_type = 'child_event' (feature 009, contracts/child-events-api.md).
 */
import { apiClient } from "./apiClient";
import { enqueue, getPending } from "./offlineQueue";
import { registerSyncHandler } from "./syncEngine";
import { updateQueueRowPayload } from "./localDb";
import type { ChildEventResponse, ChildEventType, DailySummaryResponse, PagedChildEventsResponse } from "../types";

type ErrorBody = { errorKey?: string; fieldErrors?: Record<string, string> };

function generateId(): string {
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
    const r = Math.floor(Math.random() * 16);
    const v = c === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

export interface RecordChildEventInput {
  childId: string;
  eventType: ChildEventType;
  occurredAt: string;
  endedAt?: string | null;
  payload: Record<string, unknown>;
  visibleToParent?: boolean;
  administeredByStaffId?: string | null;
}

/**
 * FR-012: shown immediately in the local UI whether online or offline. Online, this is simply
 * the server's response; offline, it's an optimistic reconstruction from the request itself
 * (`recordedBy`/timestamps aren't known client-side until the server resolves them on sync).
 */
export async function recordChildEvent(input: RecordChildEventInput, isConnected: boolean): Promise<ChildEventResponse> {
  const id = generateId();
  const body = {
    id,
    childId: input.childId,
    eventType: input.eventType,
    occurredAt: input.occurredAt,
    endedAt: input.endedAt ?? null,
    payload: input.payload,
    visibleToParent: input.visibleToParent ?? true,
    administeredByStaffId: input.administeredByStaffId ?? null,
  };

  if (isConnected) {
    const result = await apiClient.POST("/api/child-events", { body });
    if (result.response.ok) return result.data as unknown as ChildEventResponse;
    const errorBody = result.error as ErrorBody | undefined;
    throw new Error(errorBody?.errorKey ?? "errors.network");
  }

  await enqueue({
    entityType: "child_event",
    operation: "create",
    payload: body,
    endpoint: "/api/child-events",
    httpMethod: "POST",
  });

  const now = new Date().toISOString();
  return { ...body, recordedBy: [], administeredBy: body.administeredByStaffId, createdAt: now, updatedAt: now };
}

/**
 * FR-013/research.md R3: if the sleep event's original create is still queued (unsynced), the
 * end is merged directly into that row's payload rather than queuing a separate PATCH. If the
 * create has already synced (or we're online, so it never needed queuing), a normal PATCH is
 * sent/queued instead.
 */
export async function endSleepEvent(
  eventId: string, endedAt: string, quality: string, isConnected: boolean,
): Promise<ChildEventResponse | null> {
  if (isConnected) {
    const result = await apiClient.PATCH("/api/child-events/{id}", {
      params: { path: { id: eventId } },
      body: { endedAt, payload: { quality }, visibleToParent: null, administeredByStaffId: null },
    });
    if (result.response.ok) return result.data as unknown as ChildEventResponse;
    const errorBody = result.error as ErrorBody | undefined;
    throw new Error(errorBody?.errorKey ?? "errors.network");
  }

  const pending = await getPending();
  const createRow = pending.find((r) => {
    if (r.entity_type !== "child_event" || r.operation !== "create") return false;
    try {
      return JSON.parse(r.payload).id === eventId;
    } catch {
      return false;
    }
  });

  if (createRow) {
    const merged = JSON.parse(createRow.payload);
    merged.endedAt = endedAt;
    merged.payload = { ...merged.payload, quality };
    // Re-checks synced_at IS NULL at update time (not just at the read above) — closes the same
    // race research.md R3/CHK008 describes for replay(); if it lost the race, fall through to a
    // normal queued PATCH below instead of silently dropping the end.
    if (updateQueueRowPayload(createRow.id, JSON.stringify(merged))) return null;
  }

  await enqueue({
    entityType: "child_event",
    operation: "update",
    payload: { endedAt, payload: { quality } },
    endpoint: `/api/child-events/${eventId}`,
    httpMethod: "PATCH",
  });
  return null;
}

export async function updateChildEvent(
  eventId: string,
  changes: { payload?: Record<string, unknown>; visibleToParent?: boolean; administeredByStaffId?: string | null },
  isConnected: boolean,
): Promise<ChildEventResponse | null> {
  const body = {
    endedAt: null,
    payload: changes.payload ?? null,
    visibleToParent: changes.visibleToParent ?? null,
    administeredByStaffId: changes.administeredByStaffId ?? null,
  };

  if (isConnected) {
    const result = await apiClient.PATCH("/api/child-events/{id}", { params: { path: { id: eventId } }, body });
    if (result.response.ok) return result.data as unknown as ChildEventResponse;
    const errorBody = result.error as ErrorBody | undefined;
    throw new Error(errorBody?.errorKey ?? "errors.network");
  }

  await enqueue({
    entityType: "child_event",
    operation: "update",
    payload: body,
    endpoint: `/api/child-events/${eventId}`,
    httpMethod: "PATCH",
  });
  return null;
}

export async function deleteChildEvent(eventId: string, isConnected: boolean): Promise<void> {
  if (isConnected) {
    const result = await apiClient.DELETE("/api/child-events/{id}", { params: { path: { id: eventId } } });
    if (!result.response.ok) {
      const errorBody = result.error as ErrorBody | undefined;
      throw new Error(errorBody?.errorKey ?? "errors.network");
    }
    return;
  }

  await enqueue({
    entityType: "child_event",
    operation: "delete",
    payload: {},
    endpoint: `/api/child-events/${eventId}`,
    httpMethod: "DELETE",
  });
}

export async function listChildEvents(childId: string, before?: string, limit = 20): Promise<PagedChildEventsResponse> {
  const result = await apiClient.GET("/api/child-events", { params: { query: { childId, before, limit } } });
  if (!result.response.ok) throw new Error("errors.child_events.list_failed");
  return result.data as unknown as PagedChildEventsResponse;
}

export async function getDailySummary(childId: string, date: string): Promise<DailySummaryResponse> {
  const result = await apiClient.GET("/api/child-events/daily-summary", { params: { query: { childId, date } } });
  if (!result.response.ok) throw new Error("errors.child_events.summary_failed");
  return result.data as unknown as DailySummaryResponse;
}

// research.md R10: a 422 (genuine validation rejection) is a permanent failure, not a transient
// one — syncEngine.ts marks it distinctly (FR-014a) rather than retrying forever. No conflict is
// actually expected in practice (child events are append-only, spec.md: "ALL WRITES PRESERVED"),
// so onConflict exists only to satisfy the SyncHandler interface shape (contracts/
// child-events-api.md).
registerSyncHandler("child_event", {
  onConflict: () => "discard",
});
