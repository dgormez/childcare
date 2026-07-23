/**
 * leaveRequests.ts — US4 (FR-009/FR-012): submits a planned leave request and lists the
 * caller's own. Requires connectivity (spec.md Offline behavior) — no cache fallback, unlike
 * schedule.ts's read-only view.
 */
import { apiClient } from "./apiClient";
import type { StaffLeaveRequestResponse, StaffLeaveRequestType } from "../types";

export async function getMyLeaveRequests(): Promise<StaffLeaveRequestResponse[] | null> {
  try {
    const result = await apiClient.GET("/api/staff-leave-requests/me");
    if (!result.response.ok) return null;
    return result.data as unknown as StaffLeaveRequestResponse[];
  } catch {
    return null;
  }
}

export async function submitLeaveRequest(
  type: StaffLeaveRequestType,
  dateFrom: string,
  dateTo: string,
  notes: string | null,
): Promise<{ succeeded: true } | { succeeded: false; errorKey: string }> {
  try {
    const result = await apiClient.POST("/api/staff-leave-requests", {
      body: { type, dateFrom, dateTo, notes },
    });
    if (!result.response.ok) {
      const errorKey = (result.error as { errorKey?: string } | undefined)?.errorKey ?? "errors.validation";
      return { succeeded: false, errorKey };
    }
    return { succeeded: true };
  } catch {
    return { succeeded: false, errorKey: "NETWORK_ERROR" };
  }
}
