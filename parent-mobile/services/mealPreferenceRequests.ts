/**
 * mealPreferenceRequests.ts — parent-facing meal-preference-change requests (feature 013e, US3,
 * contracts/monthly-menu-api.md). No offline queue: submission requires live server-side
 * duplicate-pending validation, mirroring dayReservations.ts's identical rationale.
 */
import { apiClient } from "./apiClient";
import type { ParentMealPreferenceResponse, MealPreferenceChangeRequestResponse, MealTexture } from "../types";

function errorKeyFrom(result: { error?: unknown }): string {
  return (result.error as { errorKey?: string } | undefined)?.errorKey ?? "errors.validation";
}

export async function getMealPreference(childId: string): Promise<ParentMealPreferenceResponse> {
  const result = await apiClient.GET("/api/parent/children/{childId}/meal-preference", { params: { path: { childId } } });
  if (!result.response.ok) throw new Error(errorKeyFrom(result));
  return result.data as unknown as ParentMealPreferenceResponse;
}

export async function submitMealPreferenceChangeRequest(
  childId: string,
  newTexture: MealTexture | null,
  newDietaryType: string[] | null,
  notes: string | null,
): Promise<MealPreferenceChangeRequestResponse> {
  const result = await apiClient.POST("/api/parent/children/{childId}/meal-preference-requests", {
    params: { path: { childId } },
    body: { newTexture, newDietaryType, notes },
  });
  if (!result.response.ok) throw new Error(errorKeyFrom(result));
  return result.data as unknown as MealPreferenceChangeRequestResponse;
}
