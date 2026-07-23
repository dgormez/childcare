"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Clock } from "lucide-react";
import { apiClient } from "../../lib/apiClient";
import { Badge } from "../ui/badge";
import { Button } from "../ui/button";
import { EmptyState } from "../EmptyState";
import { ErrorState } from "../ErrorState";
import { TimeEntryCorrectionDialog } from "./TimeEntryCorrectionDialog";
import type { StaffTimeEntryResponse } from "../../lib/types";

type LoadState = "loading" | "loaded" | "error";

/**
 * Director-facing time-entry list for one staff member (spec.md FR-006/FR-007/FR-008/FR-009,
 * User Story 2) — the last 30 days by default, each row opening TimeEntryCorrectionDialog.
 */
export function StaffTimeEntriesTab({ staffProfileId }: { staffProfileId: string }) {
  const t = useTranslations("staff.timeEntries");
  const [entries, setEntries] = useState<StaffTimeEntryResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");
  const [editing, setEditing] = useState<StaffTimeEntryResponse | null>(null);

  const load = useCallback(async () => {
    setState("loading");
    const to = new Date().toISOString().slice(0, 10);
    const from = new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10);
    const result = await apiClient.GET("/api/staff-time-entries", {
      params: { query: { staffProfileId, from, to } },
    });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setEntries(result.data as unknown as StaffTimeEntryResponse[]);
    setState("loaded");
  }, [staffProfileId]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div>
      {state === "loading" && <div className="h-32 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}
      {state === "loaded" && entries.length === 0 && <EmptyState icon={Clock} message={t("emptyState")} />}
      {state === "loaded" && entries.length > 0 && (
        <div className="overflow-x-auto rounded-xl border border-border dark:border-border-dark">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-border text-left text-text-soft dark:border-border-dark dark:text-text-soft-dark">
                <th className="h-10 px-3 font-medium">{t("columns.date")}</th>
                <th className="h-10 px-3 font-medium">{t("columns.function")}</th>
                <th className="h-10 px-3 font-medium">{t("columns.clockedIn")}</th>
                <th className="h-10 px-3 font-medium">{t("columns.clockedOut")}</th>
                <th className="h-10 px-3 font-medium">{t("columns.status")}</th>
                <th className="h-10 px-3" />
              </tr>
            </thead>
            <tbody>
              {entries.map((entry) => (
                <tr key={entry.id} className="border-b border-border last:border-0 dark:border-border-dark">
                  <td className="h-10 px-3 text-text dark:text-text-dark">
                    {new Date(entry.clockedInAt).toLocaleDateString()}
                  </td>
                  <td className="h-10 px-3 text-text dark:text-text-dark">{t(`functions.${entry.function}`)}</td>
                  <td className="h-10 px-3 tabular-nums text-text dark:text-text-dark">
                    {new Date(entry.clockedInAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                  </td>
                  <td className="h-10 px-3 tabular-nums text-text dark:text-text-dark">
                    {entry.clockedOutAt
                      ? new Date(entry.clockedOutAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })
                      : "—"}
                  </td>
                  <td className="h-10 px-3">
                    {entry.isOpen && <Badge variant="info">{t("status.open")}</Badge>}
                    {!entry.isOpen && entry.isLocked && <Badge variant="neutral">{t("status.locked")}</Badge>}
                    {!entry.isOpen && !entry.isLocked && <Badge variant="success">{t("status.editable")}</Badge>}
                  </td>
                  <td className="h-10 px-3 text-right">
                    <Button variant="secondary" size="sm" onClick={() => setEditing(entry)}>
                      {t("edit")}
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <TimeEntryCorrectionDialog
        entry={editing}
        onOpenChange={(open) => !open && setEditing(null)}
        onSaved={() => {
          setEditing(null);
          load();
        }}
      />
    </div>
  );
}
