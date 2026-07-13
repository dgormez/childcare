"use client";
import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { Syringe, AlertTriangle, Clock } from "lucide-react";
import { apiClient } from "../../lib/apiClient";
import { Badge } from "../ui/badge";
import { EmptyState } from "../EmptyState";
import { ErrorState } from "../ErrorState";
import type { VaccinationsDueSoonResponse } from "../../lib/types";

type LoadState = "loading" | "loaded" | "error";

/**
 * Director dashboard "Vaccinations due soon" block (spec.md FR-010/FR-011, User Story 3).
 * Sorted soonest/most-overdue first by the backend query — no client-side re-sort needed.
 */
export function DueSoonBlock() {
  const t = useTranslations("dashboard.dueSoon");
  const router = useRouter();
  const [items, setItems] = useState<VaccinationsDueSoonResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");

  const load = useCallback(async () => {
    setState("loading");
    const result = await apiClient.GET("/api/vaccine-records/due-soon");
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setItems(result.data as unknown as VaccinationsDueSoonResponse[]);
    setState("loaded");
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <section>
      <h2 className="mb-3 text-sm font-semibold text-text dark:text-text-dark">{t("title")}</h2>

      {state === "loading" && <div className="h-32 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}
      {state === "loaded" && items.length === 0 && <EmptyState icon={Syringe} message={t("emptyState")} />}
      {state === "loaded" && items.length > 0 && (
        <ul className="divide-y divide-border rounded-xl border border-border dark:divide-border-dark dark:border-border-dark">
          {items.map((item) => (
            <li
              key={`${item.childId}-${item.vaccineName}`}
              onClick={() => router.push(`/children/${item.childId}`)}
              className="flex h-10 cursor-pointer items-center justify-between px-4 hover:bg-surface-soft dark:hover:bg-surface-soft-dark"
            >
              <span className="text-sm text-text dark:text-text-dark">
                {item.childName} — {item.vaccineName}
              </span>
              <div className="flex items-center gap-2">
                <span className="tabular-nums text-sm text-text-soft dark:text-text-soft-dark">{item.nextDueDate}</span>
                {item.isOverdue ? (
                  <Badge variant="danger" className="inline-flex items-center gap-1">
                    <AlertTriangle className="h-3 w-3" strokeWidth={2} />
                    {t("overdue")}
                  </Badge>
                ) : (
                  <Badge variant="warning" className="inline-flex items-center gap-1">
                    <Clock className="h-3 w-3" strokeWidth={2} />
                    {t("dueSoon")}
                  </Badge>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
