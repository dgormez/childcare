/**
 * readCache.ts — thin typed wrapper over the `read_cache` table (FR-009/FR-011).
 * Always writes `expires_at = NULL` (FR-015a — no time-based expiry in this feature).
 */
import { getCacheRow, setCacheRow } from "./localDb";
import { useStore } from "../store/useStore";

function currentTenantId(): string | null {
  return useStore.getState().auth?.organisationSlug ?? null;
}

export function getCached<T>(cacheKey: string): T | null {
  const tenantId = currentTenantId();
  if (!tenantId) return null;
  const row = getCacheRow(cacheKey, tenantId);
  if (!row) return null;
  try {
    return JSON.parse(row.data) as T;
  } catch {
    return null;
  }
}

export function setCached(cacheKey: string, data: unknown): void {
  const tenantId = currentTenantId();
  if (!tenantId) return;
  setCacheRow(cacheKey, tenantId, JSON.stringify(data));
}
