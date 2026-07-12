/**
 * incidentReports.ts — file/list/get calls plus the offline sync handler for
 * entity_type = 'incident_report' (feature 013b, contracts/incident-reports-api.md).
 */
import { apiClient } from "./apiClient";
import { enqueue } from "./offlineQueue";
import { registerSyncHandler } from "./syncEngine";
import type { IncidentReportResponse } from "../types";

type ErrorBody = { errorKey?: string; fieldErrors?: Record<string, string> };

function generateId(): string {
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
    const r = Math.floor(Math.random() * 16);
    const v = c === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

export interface FileIncidentReportInput {
  childId: string;
  occurredAt: string;
  locationDetail?: string | null;
  description: string;
  injuryType: string;
  firstAidGiven?: string | null;
  doctorCalled: boolean;
  doctorNotes?: string | null;
  parentNotified: boolean;
  parentNotifiedAt?: string | null;
  parentNotifiedHow?: string | null;
  witnesses?: string | null;
  followUp?: string | null;
}

/**
 * FR-014: shown immediately whether online or offline. Online, this is the server's response
 * (with `reportedBy` resolved server-side); offline, it's an optimistic reconstruction —
 * `reportedBy`/`locationId` aren't known client-side until the server resolves them on sync.
 */
export async function fileIncidentReport(
  input: FileIncidentReportInput, isConnected: boolean,
): Promise<IncidentReportResponse> {
  const body = {
    childId: input.childId,
    occurredAt: input.occurredAt,
    locationDetail: input.locationDetail ?? null,
    description: input.description,
    injuryType: input.injuryType,
    firstAidGiven: input.firstAidGiven ?? null,
    doctorCalled: input.doctorCalled,
    doctorNotes: input.doctorNotes ?? null,
    parentNotified: input.parentNotified,
    parentNotifiedAt: input.parentNotifiedAt ?? null,
    parentNotifiedHow: input.parentNotifiedHow ?? null,
    witnesses: input.witnesses ?? null,
    followUp: input.followUp ?? null,
  };

  if (isConnected) {
    const result = await apiClient.POST("/api/incident-reports", { body });
    if (result.response.ok) return result.data as unknown as IncidentReportResponse;
    const errorBody = result.error as ErrorBody | undefined;
    throw new Error(errorBody?.errorKey ?? "errors.network");
  }

  const localId = generateId();
  await enqueue({
    entityType: "incident_report",
    operation: "create",
    payload: { ...body, localId },
    endpoint: "/api/incident-reports",
    httpMethod: "POST",
  });

  const now = new Date().toISOString();
  return {
    id: localId,
    childId: body.childId,
    locationId: "",
    occurredAt: body.occurredAt,
    locationDetail: body.locationDetail,
    description: body.description,
    injuryType: body.injuryType,
    firstAidGiven: body.firstAidGiven,
    doctorCalled: body.doctorCalled,
    doctorNotes: body.doctorNotes,
    parentNotified: body.parentNotified,
    parentNotifiedAt: body.parentNotifiedAt,
    parentNotifiedHow: body.parentNotifiedHow,
    reportedBy: [],
    witnesses: body.witnesses,
    followUp: body.followUp,
    reviewedAt: null,
    createdAt: now,
    updatedAt: null,
  };
}

export async function getIncidentReport(id: string): Promise<IncidentReportResponse> {
  const result = await apiClient.GET("/api/incident-reports/{id}", { params: { path: { id } } });
  if (!result.response.ok) throw new Error("errors.incident_reports.not_found");
  return result.data as unknown as IncidentReportResponse;
}

// research.md: incident reports are append-only from the caregiver tablet (no offline edit path
// ships in this feature — FR-007's edit window is a same-window correction, not something the
// offline queue replays), so onConflict exists only to satisfy the SyncHandler interface shape,
// mirroring child_event's identical rationale.
registerSyncHandler("incident_report", {
  onConflict: () => "discard",
});
