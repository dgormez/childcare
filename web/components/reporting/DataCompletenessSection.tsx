"use client";
import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { ClipboardCheck } from "lucide-react";
import { apiClient } from "../../lib/apiClient";
import { ErrorState } from "../ErrorState";
import { EmptyState } from "../EmptyState";
import type { DataCompletenessResponse, DataCompletenessFlagType } from "../../lib/types";

type LoadState = "loading" | "loaded" | "error";

/** FR-011: a flat list of data-completeness gaps, each linking to the affected child's or staff
 * member's existing screen (staff has no per-record detail page yet, so it links to the staff
 * list where a director can find and fix the flagged member). */
export function DataCompletenessSection({ locationId }: { locationId: string }) {
  const t = useTranslations("dashboard.reporting.dataCompleteness");
  const tShared = useTranslations("dashboard.reporting");
  const router = useRouter();
  const [data, setData] = useState<DataCompletenessResponse | null>(null);
  const [state, setState] = useState<LoadState>("loading");

  const load = useCallback(async () => {
    setState("loading");
    const result = await apiClient.GET("/api/reports/data-completeness", {
      params: { query: locationId ? { locationId } : {} },
    });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setData(result.data as unknown as DataCompletenessResponse);
    setState("loaded");
  }, [locationId]);

  useEffect(() => {
    load();
  }, [load]);

  const LABEL_KEY: Record<DataCompletenessFlagType, string> = {
    missing_pickup_contact: "missingPickupContact",
    overdue_vaccine: "overdueVaccine",
    missing_qualification: "missingQualification",
    missing_pin: "missingPin",
    missing_identity_verification: "missingIdentityVerification",
  };

  function labelFor(type: DataCompletenessFlagType): string {
    return t(LABEL_KEY[type]);
  }

  return (
    <section>
      <h2 className="mb-3 text-sm font-semibold text-text dark:text-text-dark">{t("title")}</h2>

      {state === "loading" && <div className="h-32 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && (
        <ErrorState message={tShared("loadError")} retryLabel={tShared("retry")} onRetry={load} />
      )}
      {state === "loaded" && data && data.flags.length === 0 && (
        <EmptyState icon={ClipboardCheck} message={t("emptyState")} />
      )}
      {state === "loaded" && data && data.flags.length > 0 && (
        <ul className="divide-y divide-border rounded-xl border border-border dark:divide-border-dark dark:border-border-dark">
          {data.flags.map((flag, i) => {
            const target = flag.subjectType === "child" ? `/children/${flag.subjectId}` : "/staff";
            return (
            <li
              key={`${flag.subjectId}-${flag.type}-${i}`}
              role="button"
              tabIndex={0}
              onClick={() => router.push(target)}
              onKeyDown={(e) => {
                if (e.key === "Enter" || e.key === " ") {
                  e.preventDefault();
                  router.push(target);
                }
              }}
              className="flex h-10 cursor-pointer items-center justify-between px-4 hover:bg-surface-soft focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:hover:bg-surface-soft-dark"
            >
              <span className="text-sm text-text dark:text-text-dark">{flag.subjectName}</span>
              <span className="text-sm text-text-soft dark:text-text-soft-dark">
                {labelFor(flag.type)}{flag.detail ? ` — ${flag.detail}` : ""}
              </span>
            </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
