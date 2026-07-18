"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { History } from "lucide-react";
import { apiClient } from "../../lib/apiClient";
import { ErrorState } from "../ErrorState";
import { EmptyState } from "../EmptyState";
import { StatusBadge } from "./StatusBadge";
import type { BkrRatioOverviewResponse, BkrBreachHistoryResponse } from "../../lib/types";

type LoadState = "loading" | "loaded" | "error";

function defaultDateRange() {
  const to = new Date();
  const from = new Date(to);
  from.setDate(from.getDate() - 30);
  const iso = (d: Date) => d.toISOString().slice(0, 10);
  return { from: iso(from), to: iso(to) };
}

/** FR-004/FR-005: live per-group BKR ratio plus, for a director-chosen date range (default last
 * 30 days per spec.md Clarifications), the history of breach windows. */
export function BkrComplianceSection({ locationId }: { locationId: string }) {
  const t = useTranslations("dashboard.reporting.bkr");
  const tShared = useTranslations("dashboard.reporting");
  const [ratio, setRatio] = useState<BkrRatioOverviewResponse | null>(null);
  const [ratioState, setRatioState] = useState<LoadState>("loading");

  const [range, setRange] = useState(defaultDateRange);
  const [breaches, setBreaches] = useState<BkrBreachHistoryResponse | null>(null);
  const [breachState, setBreachState] = useState<LoadState>("loading");

  const loadRatio = useCallback(async () => {
    setRatioState("loading");
    const result = await apiClient.GET("/api/reports/bkr", {
      params: { query: locationId ? { locationId } : {} },
    });
    if (!result.response.ok) {
      setRatioState("error");
      return;
    }
    setRatio(result.data as unknown as BkrRatioOverviewResponse);
    setRatioState("loaded");
  }, [locationId]);

  const loadBreaches = useCallback(async () => {
    setBreachState("loading");
    const result = await apiClient.GET("/api/reports/bkr/breaches", {
      params: { query: { ...(locationId ? { locationId } : {}), from: range.from, to: range.to } },
    });
    if (!result.response.ok) {
      setBreachState("error");
      return;
    }
    setBreaches(result.data as unknown as BkrBreachHistoryResponse);
    setBreachState("loaded");
  }, [locationId, range]);

  useEffect(() => {
    loadRatio();
  }, [loadRatio]);

  useEffect(() => {
    loadBreaches();
  }, [loadBreaches]);

  return (
    <section>
      <h2 className="mb-3 text-sm font-semibold text-text dark:text-text-dark">{t("title")}</h2>

      {ratioState === "loading" && <div className="h-24 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}
      {ratioState === "error" && (
        <ErrorState message={tShared("loadError")} retryLabel={tShared("retry")} onRetry={loadRatio} />
      )}
      {ratioState === "loaded" && ratio && (
        <ul className="divide-y divide-border rounded-xl border border-border dark:divide-border-dark dark:border-border-dark">
          {ratio.groups.map((group) => (
            <li key={group.groupId} className="flex h-10 items-center justify-between px-4">
              <span className="tabular-nums text-sm text-text dark:text-text-dark">
                {t("presentLabel")}: {group.presentCount} — {t("qualifiedStaffLabel")}: {group.qualifiedStaffCount}
              </span>
              <StatusBadge status={group.status} />
            </li>
          ))}
        </ul>
      )}

      <div className="mt-6">
        <h3 className="mb-2 text-xs font-semibold uppercase text-text-soft dark:text-text-soft-dark">
          {t("breachHistory.title")}
        </h3>

        <div className="mb-3 flex flex-wrap items-end gap-4">
          <div className="space-y-1">
            <label htmlFor="bkr-breach-from" className="text-sm font-medium text-text dark:text-text-dark">
              {t("breachHistory.fromLabel")}
            </label>
            <input
              id="bkr-breach-from"
              type="date"
              value={range.from}
              onChange={(e) => setRange((r) => ({ ...r, from: e.target.value }))}
              className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
            />
          </div>
          <div className="space-y-1">
            <label htmlFor="bkr-breach-to" className="text-sm font-medium text-text dark:text-text-dark">
              {t("breachHistory.toLabel")}
            </label>
            <input
              id="bkr-breach-to"
              type="date"
              value={range.to}
              onChange={(e) => setRange((r) => ({ ...r, to: e.target.value }))}
              className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
            />
          </div>
        </div>

        {breachState === "loading" && <div className="h-16 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}
        {breachState === "error" && (
          <ErrorState message={tShared("loadError")} retryLabel={tShared("retry")} onRetry={loadBreaches} />
        )}
        {breachState === "loaded" && breaches && breaches.breaches.length === 0 && (
          <EmptyState icon={History} message={t("breachHistory.emptyState")} />
        )}
        {breachState === "loaded" && breaches && breaches.breaches.length > 0 && (
          <ul className="divide-y divide-border rounded-xl border border-border dark:divide-border-dark dark:border-border-dark">
            {breaches.breaches.map((breach, i) => (
              <li key={`${breach.groupId}-${breach.startedAt}-${i}`} className="flex h-10 items-center justify-between px-4">
                <span className="text-sm text-text dark:text-text-dark">
                  {t("breachHistory.startedAt")}: {new Date(breach.startedAt).toLocaleString()}
                </span>
                <span className="text-sm text-text-soft dark:text-text-soft-dark">
                  {breach.endedAt
                    ? `${t("breachHistory.endedAt")}: ${new Date(breach.endedAt).toLocaleString()}`
                    : t("breachHistory.ongoing")}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}
