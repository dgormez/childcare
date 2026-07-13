/**
 * healthSummary.ts — feature 013c, US4. Fetches a child's read-only health/vaccine summary,
 * caching on success and falling back to the cache on failure — same pattern as the group
 * view's `fetchChildren`/`CHILDREN_CACHE_KEY` (app/(app)/index.tsx). Distinguishes "genuinely
 * no data" from "couldn't load and nothing cached" (spec.md Edge Cases) — a caller must not
 * conflate the two into one falsy value.
 */
import { apiClient } from "./apiClient";
import { getCached, setCached } from "./readCache";
import type { ChildHealthSummaryResponse } from "../types";

export type HealthSummaryLoadResult =
  | { status: "loaded"; summary: ChildHealthSummaryResponse }
  | { status: "unavailable" };

export function healthSummaryCacheKey(childId: string): string {
  return `health-summary:${childId}`;
}

export async function getChildHealthSummary(childId: string): Promise<HealthSummaryLoadResult> {
  const cacheKey = healthSummaryCacheKey(childId);
  try {
    const result = await apiClient.GET("/api/children/{id}/health-summary", { params: { path: { id: childId } } });
    if (!result.response.ok) throw new Error("health_summary_load_failed");
    const summary = result.data as unknown as ChildHealthSummaryResponse;
    setCached(cacheKey, summary);
    return { status: "loaded", summary };
  } catch {
    const cached = getCached<ChildHealthSummaryResponse>(cacheKey);
    return cached ? { status: "loaded", summary: cached } : { status: "unavailable" };
  }
}
