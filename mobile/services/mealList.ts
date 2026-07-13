/**
 * mealList.ts — feature 013d, US1. Fetches the caregiver's own-group meal list, caching on
 * success and falling back to the cache on failure — same pattern as healthSummary.ts's
 * getChildHealthSummary (research.md R6). The device's own group scoping happens server-side
 * (GetMealListQuery's RestrictToGroupId, derived from the device token's GroupId claim) — this
 * client never filters by group itself.
 */
import { apiClient } from "./apiClient";
import { getCached, setCached } from "./readCache";
import { useStore } from "../store/useStore";
import type { MealListResponse } from "../types";

export type MealListLoadResult =
  | { status: "loaded"; mealList: MealListResponse }
  | { status: "unavailable" };

function todayDateString(): string {
  return new Date().toISOString().slice(0, 10);
}

export function mealListCacheKey(locationId: string, date: string): string {
  return `meal-list:${locationId}:${date}`;
}

export async function getMealList(includeExpected = false): Promise<MealListLoadResult> {
  const locationId = useStore.getState().device?.locationId;
  if (!locationId) return { status: "unavailable" };

  const date = todayDateString();
  const cacheKey = mealListCacheKey(locationId, date);
  try {
    const result = await apiClient.GET("/api/locations/{locationId}/meal-list", {
      params: { path: { locationId }, query: { date, includeExpected } },
    });
    if (!result.response.ok) throw new Error("meal_list_load_failed");
    const mealList = result.data as unknown as MealListResponse;
    setCached(cacheKey, mealList);
    return { status: "loaded", mealList };
  } catch {
    const cached = getCached<MealListResponse>(cacheKey);
    return cached ? { status: "loaded", mealList: cached } : { status: "unavailable" };
  }
}
