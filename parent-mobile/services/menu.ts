/**
 * menu.ts — feature 013e, US2. Fetches the current month's published menu per location, caching
 * on success and falling back to the cache on failure — mirrors mobile/services/healthSummary.ts's
 * fetch-then-cache-fallback shape (013c). Unlike that caregiver-app precedent, parent-mobile has
 * no persistent offline-sync store (feature 008's SQLite cache is caregiver-tablet-only,
 * spec.md Assumptions: parents are expected to have network access) — this cache is in-memory for
 * the current app session only, reset on cold start, not persisted to disk.
 */
import { apiClient } from "./apiClient";
import type { ParentMonthlyMenuEntry } from "../types";

export type MonthlyMenuLoadResult =
  | { status: "loaded"; entries: ParentMonthlyMenuEntry[] }
  | { status: "unavailable" };

const cache = new Map<string, ParentMonthlyMenuEntry[]>();

export function monthlyMenuCacheKey(year: number, month: number): string {
  return `${year}-${month}`;
}

export async function getMonthlyMenu(year: number, month: number): Promise<MonthlyMenuLoadResult> {
  const cacheKey = monthlyMenuCacheKey(year, month);
  try {
    const result = await apiClient.GET("/api/parent/monthly-menu", { params: { query: { year, month } } });
    if (!result.response.ok) throw new Error("menu_load_failed");
    const entries = result.data as unknown as ParentMonthlyMenuEntry[];
    cache.set(cacheKey, entries);
    return { status: "loaded", entries };
  } catch {
    const cached = cache.get(cacheKey);
    return cached ? { status: "loaded", entries: cached } : { status: "unavailable" };
  }
}
