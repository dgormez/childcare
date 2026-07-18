"use client";
import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { apiClient } from "../../lib/apiClient";
import { ErrorState } from "../ErrorState";
import { StatusBadge } from "./StatusBadge";
import type { OccupancySummaryResponse } from "../../lib/types";

type LoadState = "loading" | "loaded" | "error";

/** FR-001/FR-002/FR-003: today's actual occupancy per group/location, plus a week-ahead
 * projection per location — colour-coded, never colour alone (FR-018/FR-020). Each group row
 * links to `/groups` (its existing screen — matches spec.md's "reach that record's own detail
 * screen in one click"). */
export function OccupancySection({ locationId }: { locationId: string }) {
  const t = useTranslations("dashboard.reporting.occupancy");
  const router = useRouter();
  const tShared = useTranslations("dashboard.reporting");
  const [data, setData] = useState<OccupancySummaryResponse | null>(null);
  const [state, setState] = useState<LoadState>("loading");

  const load = useCallback(async () => {
    setState("loading");
    const result = await apiClient.GET("/api/reports/occupancy", {
      params: { query: locationId ? { locationId } : {} },
    });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setData(result.data as unknown as OccupancySummaryResponse);
    setState("loaded");
  }, [locationId]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <section>
      <h2 className="mb-3 text-sm font-semibold text-text dark:text-text-dark">{t("title")}</h2>

      {state === "loading" && <div className="h-32 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && (
        <ErrorState message={tShared("loadError")} retryLabel={tShared("retry")} onRetry={load} />
      )}
      {state === "loaded" && data && (
        <div className="space-y-4">
          {data.locations.map((location) => (
            <div key={location.locationId} className="rounded-xl border border-border p-4 dark:border-border-dark">
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium text-text dark:text-text-dark">{location.locationName}</span>
                <div className="flex items-center gap-2">
                  <span className="tabular-nums text-sm text-text-soft dark:text-text-soft-dark">
                    {t("presentOfCapacity", { present: location.presentCount, capacity: location.capacity })}
                  </span>
                  <StatusBadge status={location.status} />
                </div>
              </div>

              {location.groups.length > 0 && (
                <ul className="mt-3 divide-y divide-border dark:divide-border-dark">
                  {location.groups.map((group) => (
                    <li
                      key={group.groupId}
                      role="button"
                      tabIndex={0}
                      onClick={() => router.push("/groups")}
                      onKeyDown={(e) => {
                        if (e.key === "Enter" || e.key === " ") {
                          e.preventDefault();
                          router.push("/groups");
                        }
                      }}
                      className="flex h-10 cursor-pointer items-center justify-between px-1 hover:bg-surface-soft focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:hover:bg-surface-soft-dark"
                    >
                      <span className="text-sm text-text dark:text-text-dark">{group.groupName}</span>
                      <div className="flex items-center gap-2">
                        {group.capacity === null ? (
                          <span className="text-sm text-text-soft dark:text-text-soft-dark">{group.presentCount}</span>
                        ) : (
                          <>
                            <span className="tabular-nums text-sm text-text-soft dark:text-text-soft-dark">
                              {t("presentOfCapacity", { present: group.presentCount, capacity: group.capacity })}
                            </span>
                            <StatusBadge status={group.status} />
                          </>
                        )}
                      </div>
                    </li>
                  ))}
                </ul>
              )}

              {location.weekAhead.length > 0 && (
                <div className="mt-3">
                  <span className="text-xs font-medium uppercase text-text-soft dark:text-text-soft-dark">
                    {t("weekAheadLabel")}
                  </span>
                  <div className="mt-1 flex flex-wrap gap-2">
                    {location.weekAhead.map((day) => (
                      <span
                        key={day.date}
                        className="tabular-nums rounded-lg bg-surface-soft px-2 py-1 text-xs text-text-soft dark:bg-surface-soft-dark dark:text-text-soft-dark"
                      >
                        {day.date}: {day.closed ? t("closedLabel") : day.freeCapacity}
                      </span>
                    ))}
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
