/**
 * milestones.ts — catalog fetch, portfolio fetch, and observation recording (feature 016,
 * contracts/developmental-milestones-api.md). Recording mirrors childEvents.ts's offline-first
 * pattern exactly: append-only, so there is no update/delete path to reconcile on sync.
 */
import { apiClient } from "./apiClient";
import { enqueue } from "./offlineQueue";
import { registerSyncHandler } from "./syncEngine";
import type { DevelopmentalDomainResponse, MilestoneObservationResponse, MilestoneObservationStatus } from "../types";

function generateId(): string {
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
    const r = Math.floor(Math.random() * 16);
    const v = c === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

export async function fetchDevelopmentalDomains(): Promise<DevelopmentalDomainResponse[]> {
  const result = await apiClient.GET("/api/developmental-domains");
  if (!result.response.ok) throw new Error("errors.milestones.catalog_failed");
  return result.data as unknown as DevelopmentalDomainResponse[];
}

export async function fetchMilestonePortfolio(childId: string): Promise<DevelopmentalDomainResponse[]> {
  const result = await apiClient.GET("/api/children/{childId}/milestone-portfolio", { params: { path: { childId } } });
  if (!result.response.ok) throw new Error("errors.milestones.portfolio_failed");
  return (result.data as unknown as { domains: DevelopmentalDomainResponse[] }).domains;
}

export interface RecordMilestoneObservationInput {
  childId: string;
  milestoneId: string;
  status: MilestoneObservationStatus;
  observedAt: string;
  notes?: string | null;
}

/**
 * Shown immediately whether online or offline, mirroring recordChildEvent()'s optimistic-local
 * reconstruction (createdAt isn't known client-side until the server resolves it on sync).
 */
export async function recordMilestoneObservation(
  input: RecordMilestoneObservationInput,
  isConnected: boolean
): Promise<MilestoneObservationResponse> {
  const id = generateId();
  const body = {
    milestoneId: input.milestoneId,
    status: input.status,
    observedAt: input.observedAt,
    notes: input.notes ?? null,
  };
  const endpoint = `/api/children/${input.childId}/milestone-observations`;

  if (isConnected) {
    const result = await apiClient.POST("/api/children/{childId}/milestone-observations", {
      params: { path: { childId: input.childId } },
      body,
    });
    if (result.response.ok) return result.data as unknown as MilestoneObservationResponse;
    const errorBody = result.error as { errorKey?: string } | undefined;
    throw new Error(errorBody?.errorKey ?? "errors.network");
  }

  await enqueue({
    entityType: "milestone_observation",
    operation: "create",
    payload: body,
    endpoint,
    httpMethod: "POST",
  });

  return { id, status: input.status, observedAt: input.observedAt, notes: input.notes ?? null, createdAt: new Date().toISOString() };
}

// Append-only (research.md R3, spec.md FR-003) — no conflict is ever expected, mirrors
// child_event's own registration; exists only to satisfy the SyncHandler interface shape.
registerSyncHandler("milestone_observation", {
  onConflict: () => "discard",
});
