"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { apiClient, getAccessToken } from "../../../lib/apiClient";
import { Button } from "../../../components/ui/button";
import { ErrorState } from "../../../components/ErrorState";
import { StaffHoursReportTable } from "../../../components/reporting/StaffHoursReportTable";
import type { LocationResponse, StaffHoursReportResponse } from "../../../lib/types";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

function firstOfMonth(): string {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-01`;
}

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

type State = "idle" | "loading" | "loaded" | "error";

/**
 * Medewerkersbeleid subsidy report page (spec.md FR-016/SC-003, User Story 4) — a new flat
 * top-level route (this codebase has no "Rapporten" parent nav; feature 018's reports live
 * inline on /dashboard, and every other report-like screen is its own flat sidebar item,
 * research.md/plan.md).
 */
export default function StaffHoursReportPage() {
  const t = useTranslations("staffHoursReport");
  const [locations, setLocations] = useState<LocationResponse[]>([]);
  const [locationId, setLocationId] = useState("");
  const [from, setFrom] = useState(firstOfMonth());
  const [to, setTo] = useState(today());
  const [report, setReport] = useState<StaffHoursReportResponse | null>(null);
  const [state, setState] = useState<State>("idle");
  const [exporting, setExporting] = useState(false);
  const [exportError, setExportError] = useState("");

  useEffect(() => {
    apiClient.GET("/api/locations").then((result) => {
      if (!result.response.ok) return;
      const list = result.data as unknown as LocationResponse[];
      setLocations(list);
      if (list.length > 0) setLocationId(list[0].id);
    });
  }, []);

  async function generate() {
    if (!locationId) return;
    setState("loading");
    const result = await apiClient.GET("/api/reports/staff-hours", {
      params: { query: { locationId, from, to } },
    });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setReport(result.data as unknown as StaffHoursReportResponse);
    setState("loaded");
  }

  async function exportCsv() {
    setExporting(true);
    setExportError("");
    try {
      const params = new URLSearchParams({ locationId, from, to });
      const response = await fetch(`${API_BASE}/api/reports/staff-hours/export?${params.toString()}`, {
        headers: { Authorization: `Bearer ${getAccessToken()}` },
      });
      if (!response.ok) throw new Error("export failed");
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `staff-hours-${from}-${to}.csv`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch {
      setExportError(t("exportError"));
    } finally {
      setExporting(false);
    }
  }

  return (
    <div>
      <h1 className="mb-6 text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>

      <div className="mb-6 flex flex-wrap items-end gap-4">
        <label className="text-sm font-medium text-text dark:text-text-dark">
          {t("locationLabel")}
          <select
            value={locationId}
            onChange={(e) => setLocationId(e.target.value)}
            className="mt-1 block rounded-lg border-0 bg-surface-soft px-3 py-2 text-sm text-text dark:bg-surface-soft-dark dark:text-text-dark"
          >
            {locations.map((location) => (
              <option key={location.id} value={location.id}>
                {location.name}
              </option>
            ))}
          </select>
        </label>
        <label className="text-sm font-medium text-text dark:text-text-dark">
          {t("fromLabel")}
          <input
            type="date"
            value={from}
            onChange={(e) => setFrom(e.target.value)}
            className="mt-1 block rounded-lg border-0 bg-surface-soft px-3 py-2 text-sm text-text dark:bg-surface-soft-dark dark:text-text-dark"
          />
        </label>
        <label className="text-sm font-medium text-text dark:text-text-dark">
          {t("toLabel")}
          <input
            type="date"
            value={to}
            onChange={(e) => setTo(e.target.value)}
            className="mt-1 block rounded-lg border-0 bg-surface-soft px-3 py-2 text-sm text-text dark:bg-surface-soft-dark dark:text-text-dark"
          />
        </label>
        <Button onClick={generate} disabled={!locationId || state === "loading"}>
          {t("generate")}
        </Button>
        {report && (
          <Button variant="secondary" onClick={exportCsv} disabled={exporting}>
            {t("exportCsv")}
          </Button>
        )}
      </div>

      {exportError && <p className="mb-4 text-sm text-danger dark:text-danger-dark">{exportError}</p>}
      {state === "loading" && <div className="h-32 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={generate} />}
      {state === "loaded" && report && <StaffHoursReportTable report={report} />}
    </div>
  );
}
