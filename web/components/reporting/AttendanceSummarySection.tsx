"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { apiClient, getAccessToken } from "../../lib/apiClient";
import { ErrorState } from "../ErrorState";
import { Button } from "../ui/button";
import type { AttendanceSummaryResponse } from "../../lib/types";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

type LoadState = "loading" | "loaded" | "error";

function currentMonth(): string {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-01`;
}

/** FR-006/FR-007/FR-008/FR-022: monthly attendance summary, exportable as CSV/PDF — both always
 * computed fresh from GetAttendanceSummaryQuery's shared aggregation, never cached, so they agree
 * exactly with the on-screen totals (spec.md SC-002). */
export function AttendanceSummarySection({ locationId }: { locationId: string }) {
  const t = useTranslations("dashboard.reporting.attendanceSummary");
  const tShared = useTranslations("dashboard.reporting");
  const [month, setMonth] = useState(currentMonth().slice(0, 7));
  const [data, setData] = useState<AttendanceSummaryResponse | null>(null);
  const [state, setState] = useState<LoadState>("loading");
  const [exporting, setExporting] = useState(false);
  const [exportError, setExportError] = useState("");

  const load = useCallback(async () => {
    setState("loading");
    const result = await apiClient.GET("/api/reports/attendance-summary", {
      params: { query: { ...(locationId ? { locationId } : {}), month: `${month}-01` } },
    });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setData(result.data as unknown as AttendanceSummaryResponse);
    setState("loaded");
  }, [locationId, month]);

  useEffect(() => {
    load();
  }, [load]);

  async function exportAs(format: "csv" | "pdf") {
    setExporting(true);
    setExportError("");
    try {
      const params = new URLSearchParams({ month: `${month}-01`, format });
      if (locationId) params.set("locationId", locationId);
      const response = await fetch(`${API_BASE}/api/reports/attendance-summary/export?${params.toString()}`, {
        headers: { Authorization: `Bearer ${getAccessToken()}` },
      });
      if (!response.ok) throw new Error("export failed");
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `attendance-summary-${month}.${format}`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch {
      setExportError(t("exportError"));
    } finally {
      setExporting(false);
    }
  }

  return (
    <section>
      <h2 className="mb-3 text-sm font-semibold text-text dark:text-text-dark">{t("title")}</h2>

      <div className="mb-3 flex flex-wrap items-end gap-4">
        <div className="space-y-1">
          <label htmlFor="attendance-summary-month" className="text-sm font-medium text-text dark:text-text-dark">
            {t("monthLabel")}
          </label>
          <input
            id="attendance-summary-month"
            type="month"
            value={month}
            onChange={(e) => setMonth(e.target.value)}
            className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
          />
        </div>
        <Button variant="secondary" onClick={() => exportAs("csv")} disabled={exporting}>
          {t("exportCsv")}
        </Button>
        <Button variant="secondary" onClick={() => exportAs("pdf")} disabled={exporting}>
          {t("exportPdf")}
        </Button>
      </div>

      {exportError && <p className="mb-3 text-sm text-danger dark:text-danger-dark">{exportError}</p>}

      {state === "loading" && <div className="h-32 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && (
        <ErrorState message={tShared("loadError")} retryLabel={tShared("retry")} onRetry={load} />
      )}
      {state === "loaded" && data && (
        <table className="w-full text-sm">
          <thead>
            <tr className="h-10 border-b border-border text-left text-text-soft dark:border-border-dark dark:text-text-soft-dark">
              <th className="px-3 font-medium">{t("childLabel")}</th>
              <th className="px-3 font-medium">{t("presentDays")}</th>
              <th className="px-3 font-medium">{t("absentJustifiedDays")}</th>
              <th className="px-3 font-medium">{t("absentUnjustifiedDays")}</th>
              <th className="px-3 font-medium">{t("closureDays")}</th>
            </tr>
          </thead>
          <tbody>
            {data.children.map((row) => (
              <tr key={`${row.childId}-${row.locationId}-${row.groupId ?? "none"}`} className="h-10 border-b border-border dark:border-border-dark">
                <td className="px-3 text-text dark:text-text-dark">{row.childName}</td>
                <td className="tabular-nums px-3 text-text dark:text-text-dark">{row.presentDays}</td>
                <td className="tabular-nums px-3 text-text dark:text-text-dark">{row.absentJustifiedDays}</td>
                <td className="tabular-nums px-3 text-text dark:text-text-dark">{row.absentUnjustifiedDays}</td>
                <td className="tabular-nums px-3 text-text dark:text-text-dark">{row.closureDays}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
