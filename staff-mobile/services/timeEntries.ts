/**
 * timeEntries.ts — feature 028/US1: clock in/out, mirrors schedule.ts's reportSick() shape
 * (requires connectivity, no cache fallback — spec.md Offline behavior).
 */
import { apiClient } from "./apiClient";

export interface StaffTimeEntry {
  id: string;
  staffProfileId: string;
  locationId: string;
  groupId: string | null;
  clockedInAt: string;
  clockedOutAt: string | null;
  function: string;
  notes: string | null;
  isOpen: boolean;
  isLocked: boolean;
  unlockedAt: string | null;
}

export type TimeEntryResult =
  | { succeeded: true; entry: StaffTimeEntry }
  | { succeeded: false; errorKey: string };

export async function clockIn(locationId: string, groupId: string | null, staffFunction: string | null): Promise<TimeEntryResult> {
  try {
    const result = await apiClient.POST("/api/staff-time-entries/clock-in", {
      body: { locationId, groupId, function: staffFunction },
    });
    if (!result.response.ok) {
      const errorKey = (result.error as { errorKey?: string } | undefined)?.errorKey ?? "errors.staff_time_entries.clock_in_failed";
      return { succeeded: false, errorKey };
    }
    return { succeeded: true, entry: result.data as unknown as StaffTimeEntry };
  } catch {
    return { succeeded: false, errorKey: "NETWORK_ERROR" };
  }
}

export async function clockOut(): Promise<TimeEntryResult> {
  try {
    const result = await apiClient.POST("/api/staff-time-entries/clock-out", {});
    if (!result.response.ok) {
      const errorKey = (result.error as { errorKey?: string } | undefined)?.errorKey ?? "errors.staff_time_entries.clock_out_failed";
      return { succeeded: false, errorKey };
    }
    return { succeeded: true, entry: result.data as unknown as StaffTimeEntry };
  } catch {
    return { succeeded: false, errorKey: "NETWORK_ERROR" };
  }
}
