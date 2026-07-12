/**
 * locations.ts — feature 013f, contracts/reservation-settings-api.md. Lets the app decide
 * which day-reservation entry points to show/block for a given child without duplicating
 * ReservationPolicyResolver's server-side logic — this is a UI hint only; the server always
 * re-enforces at submission time regardless of what this returns (FR-007/FR-012).
 */
import { apiClient } from "./apiClient";
import type { ReservationAvailabilityResponse } from "../types";

export async function getReservationAvailability(childId: string): Promise<ReservationAvailabilityResponse | null> {
  const result = await apiClient.GET("/api/parent/children/{childId}/reservation-availability", {
    params: { path: { childId } },
  });
  if (!result.response.ok) return null;
  return result.data as unknown as ReservationAvailabilityResponse;
}
