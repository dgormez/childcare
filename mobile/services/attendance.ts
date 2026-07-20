/**
 * attendance.ts — check-in/check-out/absence calls plus the offline sync handler for
 * entity_type = 'attendance_record' (feature 010, contracts/attendance-api.md).
 */
import { apiClient } from "./apiClient";
import { enqueue } from "./offlineQueue";
import { registerSyncHandler } from "./syncEngine";
import { useStore } from "../store/useStore";
import type { AttendanceRecordResponse, BkrRatioResponse, VerifyCheckInCodeResponse } from "../types";

type ErrorBody = { errorKey?: string };

/** Today's calendar date (device-local) in `yyyy-MM-dd` form — the server independently
 * anchors "same day" checks to Europe/Brussels (research.md R5); this is only what the client
 * sends as the attendance day, not a source of truth for edit-window enforcement. */
export function todayDateString(): string {
  return new Date().toISOString().slice(0, 10);
}

/**
 * FR-001: a single tap from the group view. LocationId/GroupId are resolved server-side from
 * the device's own token claims (contracts/attendance-api.md) — never sent by the client, same
 * convention as recordChildEvent.
 */
export async function checkIn(childId: string, date: string, isConnected: boolean): Promise<AttendanceRecordResponse> {
  const body = { childId, date };

  if (isConnected) {
    const result = await apiClient.POST("/api/attendance/check-in", { body });
    if (result.response.ok) return result.data as unknown as AttendanceRecordResponse;
    const errorBody = result.error as ErrorBody | undefined;
    throw new Error(errorBody?.errorKey ?? "errors.network");
  }

  await enqueue({
    entityType: "attendance_record",
    operation: "create",
    payload: body,
    endpoint: "/api/attendance/check-in",
    httpMethod: "POST",
  });

  const now = new Date().toISOString();
  return {
    id: "", childId, locationId: "", date, status: "present", checkInAt: now, checkOutAt: null,
    plannedDurationMinutes: null, absenceJustified: null, absenceReason: null, recordedBy: [],
    createdAt: now, updatedAt: now,
  };
}

/**
 * FR-002: queued check-outs run through the same FIFO offline queue as check-ins (feature 008's
 * sync engine processes rows in `created_at` order), so a check-out enqueued after its own
 * check-in naturally replays after it — no payload merge needed here, unlike feature 009's
 * sleep-end case (there, create and complete are the *same* entity/endpoint, so merging avoids
 * a redundant second call; check-in and check-out are separate endpoints with different request
 * shapes, so merging one into the other's payload would silently drop the check-out instead).
 */
export async function checkOut(childId: string, date: string, isConnected: boolean): Promise<AttendanceRecordResponse | null> {
  if (isConnected) {
    const result = await apiClient.POST("/api/attendance/check-out", { body: { childId, date } });
    if (result.response.ok) return result.data as unknown as AttendanceRecordResponse;
    const errorBody = result.error as ErrorBody | undefined;
    throw new Error(errorBody?.errorKey ?? "errors.network");
  }

  await enqueue({
    entityType: "attendance_record",
    operation: "update",
    payload: { childId, date },
    endpoint: "/api/attendance/check-out",
    httpMethod: "POST",
  });
  return null;
}

export async function markAbsent(
  childId: string, date: string, absenceJustified: boolean, absenceReason: string | null, isConnected: boolean,
): Promise<AttendanceRecordResponse> {
  const device = useStore.getState().device;
  const body = { childId, locationId: device?.locationId ?? "", groupId: device?.groupId ?? null, date, absenceJustified, absenceReason };

  if (isConnected) {
    const result = await apiClient.POST("/api/attendance/absence", { body });
    if (result.response.ok) return result.data as unknown as AttendanceRecordResponse;
    const errorBody = result.error as ErrorBody | undefined;
    throw new Error(errorBody?.errorKey ?? "errors.network");
  }

  await enqueue({
    entityType: "attendance_record",
    operation: "create",
    payload: body,
    endpoint: "/api/attendance/absence",
    httpMethod: "POST",
  });

  const now = new Date().toISOString();
  return {
    id: "", childId, locationId: body.locationId, date, status: "absent", checkInAt: null, checkOutAt: null,
    plannedDurationMinutes: null, absenceJustified, absenceReason, recordedBy: [], createdAt: now, updatedAt: now,
  };
}

export async function getBkrRatio(locationId: string): Promise<BkrRatioResponse> {
  const result = await apiClient.GET("/api/attendance/bkr", { params: { query: { locationId } } });
  if (!result.response.ok) throw new Error("errors.attendance.bkr_failed");
  return result.data as unknown as BkrRatioResponse;
}

/** Today's attendance state at the paired device's own location, keyed by childId — used to
 * render each child's current present/absent state on load (and after another tablet's action),
 * since every other read endpoint (ListAttendanceQuery) is DirectorOnly. */
export async function getTodayAttendanceByChildId(): Promise<Record<string, AttendanceRecordResponse>> {
  const result = await apiClient.GET("/api/attendance/today");
  if (!result.response.ok) return {};
  const records = result.data as unknown as AttendanceRecordResponse[];
  return Object.fromEntries(records.map((r) => [r.childId, r]));
}

/**
 * Feature 021 — research.md R6: verification is a single online round-trip (signature check +
 * the resulting attendance write happen atomically server-side), so there is no offline branch
 * here the way checkIn/checkOut have one — the scan screen itself refuses to even attempt a
 * scan while fully offline (FR-012's first clause), directing the caregiver to manual tap
 * instead. The thrown error's message is the server's own errorKey
 * (`errors.qrCheckIn.wrong_location`, `.code_expired`, `.invalid_code`, `.already_used`), which
 * the scan screen maps to distinct copy per FR-010/FR-011/FR-007/FR-019.
 *
 * FR-012's second clause: connectivity can still drop *during* an in-flight request (the tablet
 * was online when the scan started) — the fetch itself throws in that case, distinct from a
 * genuine server rejection. The server may or may not have completed the write before the
 * connection dropped, and replaying the same code isn't safely idempotent (a already-consumed
 * code would just fail with `already_used`), so rather than guessing, this surfaces a distinct
 * `errors.qrCheckIn.connection_lost` the scan screen shows as "check the roster" guidance
 * instead of implying the code itself was invalid.
 */
export async function scanCheckInCode(code: string): Promise<VerifyCheckInCodeResponse> {
  let result;
  try {
    result = await apiClient.POST("/api/attendance/qr-code/verify", { body: { code } });
  } catch {
    throw new Error("errors.qrCheckIn.connection_lost");
  }
  if (result.response.ok) return result.data as unknown as VerifyCheckInCodeResponse;
  const errorBody = result.error as ErrorBody | undefined;
  throw new Error(errorBody?.errorKey ?? "errors.network");
}

// research.md R4: server-wins — a 409 (duplicate check-in/absence-mark) means the server's
// existing record is authoritative; the queued write is discarded (marked synced with a
// conflict note), not retried. Distinct from feature 009's "all writes preserved" policy since
// attendance has a real per-child-per-day uniqueness constraint.
registerSyncHandler("attendance_record", {
  onConflict: () => "discard",
});
