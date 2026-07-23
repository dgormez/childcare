/**
 * schedule.ts — US2 (FR-003): fetches the caller's own published schedule, caching on success
 * and falling back to the cache on failure — mirrors parent-mobile/services/menu.ts's
 * fetch-then-cache-fallback shape (feature 013c's precedent). In-memory only for the current
 * app session, not persisted to disk — matches spec.md's Offline behavior ("Read-only schedule
 * view may be cached for the (already-fetched) current 4 weeks").
 */
import { apiClient } from "./apiClient";
import type { StaffScheduleResponse } from "../types";

export type ScheduleLoadResult =
  | { status: "loaded"; entries: StaffScheduleResponse[]; fromCache: boolean }
  | { status: "unavailable" };

let cache: StaffScheduleResponse[] | null = null;

export async function getMySchedule(): Promise<ScheduleLoadResult> {
  try {
    const result = await apiClient.GET("/api/staff-schedules/me");
    if (!result.response.ok) throw new Error("schedule_load_failed");
    const entries = result.data as unknown as StaffScheduleResponse[];
    cache = entries;
    return { status: "loaded", entries, fromCache: false };
  } catch {
    return cache ? { status: "loaded", entries: cache, fromCache: true } : { status: "unavailable" };
  }
}

// FR-005/FR-005a: same-day sick report — requires connectivity (spec.md Offline behavior), no
// cache fallback attempted.
export async function reportSick(): Promise<{ succeeded: true } | { succeeded: false; errorKey: string }> {
  try {
    const result = await apiClient.POST("/api/staff-schedules/report-sick", {});
    if (!result.response.ok) {
      const errorKey = (result.error as { errorKey?: string } | undefined)?.errorKey ?? "errors.staff_schedules.report_sick_failed";
      return { succeeded: false, errorKey };
    }
    return { succeeded: true };
  } catch {
    return { succeeded: false, errorKey: "NETWORK_ERROR" };
  }
}
