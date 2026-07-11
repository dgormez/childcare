/**
 * dayReservations.ts — parent-facing day reservation requests (feature 013a,
 * contracts/day-reservations-api.md). No offline queue: submission/cancellation require live
 * server-side validation (contract/closure/capacity checks), unlike caregiver-tablet event
 * logging (spec.md Assumptions).
 */
import { apiClient } from "./apiClient";
import type { DayReservationResponse, DayReservationType } from "../types";

function errorKeyFrom(result: { error?: unknown }): string {
  return (result.error as { errorKey?: string } | undefined)?.errorKey ?? "errors.validation";
}

export async function submitDayReservation(
  childId: string,
  type: DayReservationType,
  requestedDate: string,
  exchangeForDate: string | null,
  reason: string | null,
): Promise<DayReservationResponse> {
  const result = await apiClient.POST("/api/day-reservations", {
    body: { childId, type, requestedDate, exchangeForDate, reason },
  });
  if (!result.response.ok) throw new Error(errorKeyFrom(result));
  return result.data as unknown as DayReservationResponse;
}

export async function cancelDayReservation(id: string): Promise<DayReservationResponse> {
  const result = await apiClient.POST("/api/day-reservations/{id}/cancel", { params: { path: { id } } });
  if (!result.response.ok) throw new Error(errorKeyFrom(result));
  return result.data as unknown as DayReservationResponse;
}

export async function listMyDayReservations(): Promise<DayReservationResponse[]> {
  const result = await apiClient.GET("/api/day-reservations/mine");
  if (!result.response.ok) throw new Error(errorKeyFrom(result));
  return result.data as unknown as DayReservationResponse[];
}
