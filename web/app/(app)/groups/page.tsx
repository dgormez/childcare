"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { CalendarClock, Sparkles } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { GroupTimeline } from "../../../components/GroupTimeline";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import type { GroupActivityResponse, GroupResponse, GroupTimelineResponse, LocationResponse } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

function todayDateString(): string {
  return new Date().toISOString().slice(0, 10);
}

/**
 * Director-web group timeline (feature 009b, User Story 4) — the first "group activities" view
 * on this surface (spec.md Assumptions: no such screen existed before this feature). Follows
 * attendance/page.tsx's location-scoped, date-defaulting-to-today shape.
 */
export default function GroupsPage() {
  const t = useTranslations("groups");
  const [locations, setLocations] = useState<LocationResponse[]>([]);
  const [locationId, setLocationId] = useState("");
  const [groups, setGroups] = useState<GroupResponse[]>([]);
  const [groupId, setGroupId] = useState("");
  const [date, setDate] = useState(todayDateString());
  const [entries, setEntries] = useState<GroupTimelineResponse["entries"]>([]);
  const [state, setState] = useState<LoadState>("loading");
  const [deleteTarget, setDeleteTarget] = useState<GroupActivityResponse | null>(null);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    apiClient.GET("/api/locations").then((result) => {
      if (!result.response.ok) return;
      const fetched = result.data as unknown as LocationResponse[];
      setLocations(fetched);
      if (fetched.length > 0) setLocationId((current) => current || fetched[0].id);
    });
  }, []);

  useEffect(() => {
    if (!locationId) return;
    apiClient.GET("/api/groups", { params: { query: { locationId } } }).then((result) => {
      if (!result.response.ok) return;
      const fetched = result.data as unknown as GroupResponse[];
      setGroups(fetched);
      setGroupId(fetched.length > 0 ? fetched[0].id : "");
    });
  }, [locationId]);

  const load = useCallback(async () => {
    if (!groupId) return;
    setState("loading");
    try {
      const result = await apiClient.GET("/api/group-activities/director-timeline", {
        params: { query: { groupId, date } },
      });
      if (!result.response.ok) {
        setState("error");
        return;
      }
      const timeline = result.data as unknown as GroupTimelineResponse;
      setEntries(timeline.entries);
      setState("loaded");
    } catch {
      setState("error");
    }
  }, [groupId, date]);

  useEffect(() => {
    load();
  }, [load]);

  const handleDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    const result = await apiClient.DELETE("/api/group-activities/{id}", {
      params: { path: { id: deleteTarget.id } },
    });
    setDeleting(false);
    setDeleteTarget(null);
    if (result.response.ok) await load();
  };

  return (
    <div>
      <div className="mb-6 flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
        <div className="flex items-center gap-3">
          <select
            value={locationId}
            onChange={(e) => setLocationId(e.target.value)}
            aria-label={t("locationLabel")}
            className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
          >
            {locations.map((loc) => (
              <option key={loc.id} value={loc.id}>
                {loc.name}
              </option>
            ))}
          </select>
          <select
            value={groupId}
            onChange={(e) => setGroupId(e.target.value)}
            aria-label={t("groupLabel")}
            className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
          >
            {groups.map((group) => (
              <option key={group.id} value={group.id}>
                {group.name}
              </option>
            ))}
          </select>
          <div className="relative">
            <CalendarClock className="pointer-events-none absolute left-2 top-1/2 h-4 w-4 -translate-y-1/2 text-text-soft dark:text-text-soft-dark" strokeWidth={2} />
            <input
              type="date"
              value={date}
              onChange={(e) => setDate(e.target.value)}
              aria-label={t("dateLabel")}
              className="h-10 rounded-lg bg-surface-soft pl-8 pr-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
            />
          </div>
        </div>
      </div>

      {state === "loading" && (
        <div className="h-64 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />
      )}

      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}

      {state === "loaded" && entries.length === 0 && (
        <EmptyState icon={Sparkles} message={t("emptyState")} />
      )}

      {state === "loaded" && entries.length > 0 && (
        <GroupTimeline entries={entries} onDeleteActivity={setDeleteTarget} />
      )}

      {deleteTarget && (
        <ConfirmDialog
          open={Boolean(deleteTarget)}
          onOpenChange={(open) => !open && setDeleteTarget(null)}
          title={t("deleteDialogTitle")}
          description={t("deleteDialogDescription", { title: deleteTarget.title })}
          confirmLabel={t("actionDelete")}
          cancelLabel={t("deleteDialogCancel")}
          confirmDestructive
          confirming={deleting}
          onConfirm={handleDelete}
        />
      )}
    </div>
  );
}
