"use client";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslations } from "next-intl";
import { CalendarDays, ChevronLeft, ChevronRight, TriangleAlert } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { SchedulingGrid } from "../../../components/SchedulingGrid";
import { ScheduleEntryDialog } from "../../../components/ScheduleEntryDialog";
import { SickCoverDialog } from "../../../components/SickCoverDialog";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import { Button } from "../../../components/ui/button";
import { Badge } from "../../../components/ui/badge";
import type {
  AbsenceReason,
  ApiErrorBody,
  ClosureDayResponse,
  CopyWeekResponse,
  GroupResponse,
  LocationResponse,
  StaffResponse,
  StaffScheduleResponse,
} from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

// Deliberately avoids Date.toISOString(), which converts through UTC and shifts the date
// backward by a day for any positive-UTC-offset local timezone — a real bug caught by
// browser verification (T052), not a hypothetical. Uses local date components directly.
function toDateString(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function mondayOf(date: Date): string {
  const day = date.getDay();
  const diff = day === 0 ? -6 : 1 - day;
  const monday = new Date(date);
  monday.setDate(date.getDate() + diff);
  return toDateString(monday);
}

function weekDatesFrom(weekStart: string): string[] {
  const start = new Date(`${weekStart}T00:00:00`);
  return Array.from({ length: 7 }, (_, i) => {
    const d = new Date(start);
    d.setDate(start.getDate() + i);
    return toDateString(d);
  });
}

export default function SchedulingPage() {
  const t = useTranslations("scheduling");
  const [locations, setLocations] = useState<LocationResponse[]>([]);
  const [locationId, setLocationId] = useState("");
  const [staff, setStaff] = useState<StaffResponse[]>([]);
  const [groups, setGroups] = useState<GroupResponse[]>([]);
  const [entries, setEntries] = useState<StaffScheduleResponse[]>([]);
  const [closures, setClosures] = useState<ClosureDayResponse[]>([]);
  const [projectedOnDutyByDate, setProjectedOnDutyByDate] = useState<Map<string, number>>(new Map());
  const [weekStart, setWeekStart] = useState(() => mondayOf(new Date()));
  const [state, setState] = useState<LoadState>("loading");
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<StaffScheduleResponse | null>(null);
  const [creatingFor, setCreatingFor] = useState<{ staffId: string; date: string } | null>(null);
  const [saving, setSaving] = useState(false);
  const [copyConfirmOpen, setCopyConfirmOpen] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [coveringEntry, setCoveringEntry] = useState<StaffScheduleResponse | null>(null);
  const [notice, setNotice] = useState("");

  const weekDates = useMemo(() => weekDatesFrom(weekStart), [weekStart]);
  const groupsById = useMemo(() => new Map(groups.map((g) => [g.id, g.name])), [groups]);
  const closureDates = useMemo(
    () => new Set(closures.filter((c) => c.status === "published" && weekDates.includes(c.date)).map((c) => c.date)),
    [closures, weekDates],
  );
  // FR-001: the week is "published" when it has at least one entry and every entry is
  // currently published — matches PublishScheduleWeekCommand's per-week bulk semantics
  // (research.md R4) even though the underlying flag is per-row.
  const weekIsPublished = entries.length > 0 && entries.every((e) => e.isPublished);
  // FR-006: today's absent-and-uncovered entries within the currently loaded week need an
  // urgent cover-needed banner — computed from what's already loaded rather than a dedicated
  // endpoint, since the contract doesn't define one.
  const today = useMemo(() => toDateString(new Date()), []);
  const uncoveredToday = useMemo(
    () => entries.filter((e) => e.date === today && e.status === "absent" && !e.coverStaffId),
    [entries, today],
  );
  // Convergence finding F1: only offer scheduling staff eligible for the selected location
  // (StaffResponse.eligibleLocationIds, feature 005) — the backend now enforces this too, but
  // the grid shouldn't offer an action that's guaranteed to fail. A staff member who already
  // has an entry here stays visible regardless (deactivated or no-longer-eligible), so nothing
  // the director needs to review/reassign is ever hidden (FR-009b).
  const visibleStaff = useMemo(
    () =>
      staff.filter(
        (s) =>
          entries.some((e) => e.staffProfileId === s.id) ||
          (!s.deactivatedAt && s.eligibleLocationIds.includes(locationId)),
      ),
    [staff, entries, locationId],
  );

  useEffect(() => {
    apiClient.GET("/api/locations").then((result) => {
      if (!result.response.ok || !result.data) return;
      const data = result.data as unknown as LocationResponse[];
      setLocations(data);
      if (data.length > 0) setLocationId((current) => current || data[0].id);
    });
  }, []);

  const load = useCallback(async () => {
    if (!locationId) return;
    setState("loading");
    try {
      const weekYear = Number(weekStart.slice(0, 4));
      const [staffResult, groupsResult, entriesResult, closuresResult] = await Promise.all([
        apiClient.GET("/api/staff", { params: { query: { includeDeactivated: true } } }),
        apiClient.GET("/api/groups", { params: { query: { locationId } } }),
        apiClient.GET("/api/staff-schedules", { params: { query: { locationId, weekStart } } }),
        apiClient.GET("/api/closures", { params: { query: { locationId, year: weekYear } } }),
      ]);
      if (!staffResult.response.ok || !groupsResult.response.ok || !entriesResult.response.ok) {
        setState("error");
        return;
      }
      setStaff(staffResult.data as unknown as StaffResponse[]);
      setGroups(groupsResult.data as unknown as GroupResponse[]);
      const loadedEntries = entriesResult.data as unknown as StaffScheduleResponse[];
      setEntries(loadedEntries);
      setClosures(closuresResult.response.ok ? (closuresResult.data as unknown as ClosureDayResponse[]) : []);

      const projectedResults = await Promise.all(
        weekDatesFrom(weekStart).map((date) =>
          apiClient.GET("/api/staff-schedules/projected-on-duty", { params: { query: { locationId, date, time: "12:00" } } }),
        ),
      );
      const nextProjected = new Map<string, number>();
      weekDatesFrom(weekStart).forEach((date, i) => {
        const result = projectedResults[i];
        if (result.response.ok && result.data) nextProjected.set(date, (result.data as { projectedOnDutyCount: number }).projectedOnDutyCount);
      });
      setProjectedOnDutyByDate(nextProjected);

      setState("loaded");
    } catch {
      setState("error");
    }
  }, [locationId, weekStart]);

  useEffect(() => {
    load();
  }, [load]);

  function openCreateDialog(staffId: string, date: string) {
    setEditing(null);
    setCreatingFor({ staffId, date });
    setDialogOpen(true);
  }

  function openEditDialog(entry: StaffScheduleResponse) {
    setEditing(entry);
    setCreatingFor(null);
    setDialogOpen(true);
  }

  async function saveEntry(values: { groupId: string | null; startTime: string; endTime: string }) {
    setSaving(true);
    const result = editing
      ? await apiClient.PATCH("/api/staff-schedules/{id}", {
          params: { path: { id: editing.id } },
          body: { locationId, groupId: values.groupId, startTime: values.startTime, endTime: values.endTime },
        })
      : await apiClient.POST("/api/staff-schedules", {
          body: {
            staffProfileId: creatingFor!.staffId,
            locationId,
            groupId: values.groupId,
            date: creatingFor!.date,
            startTime: values.startTime,
            endTime: values.endTime,
          },
        });
    setSaving(false);
    if (!result.response.ok) {
      const errorKey = ((result.error ?? {}) as ApiErrorBody).errorKey ?? "";
      setNotice(
        errorKey === "errors.staff_schedules.overlap"
          ? t("overlapError")
          : errorKey === "errors.staff_schedules.past_date"
            ? t("pastDateError")
            : errorKey === "errors.staff_schedules.not_eligible"
              ? t("notEligibleError")
              : t("genericError"),
      );
      return;
    }
    setDialogOpen(false);
    setEditing(null);
    setCreatingFor(null);
    setNotice("");
    await load();
  }

  async function deleteEntry() {
    if (!editing) return;
    setSaving(true);
    const result = await apiClient.DELETE("/api/staff-schedules/{id}", { params: { path: { id: editing.id } } });
    setSaving(false);
    if (!result.response.ok) {
      setNotice(t("genericError"));
      return;
    }
    setDialogOpen(false);
    setEditing(null);
    await load();
  }

  async function markAbsence(isAbsent: boolean, reason: AbsenceReason | null) {
    if (!editing) return;
    setSaving(true);
    const result = await apiClient.POST("/api/staff-schedules/{id}/absence", {
      params: { path: { id: editing.id } },
      body: { isAbsent, absenceReason: reason },
    });
    setSaving(false);
    if (!result.response.ok) {
      setNotice(t("genericError"));
      return;
    }
    setDialogOpen(false);
    setEditing(null);
    await load();
  }

  async function copyWeek() {
    setCopyConfirmOpen(false);
    const targetWeekStart = toDateString(new Date(new Date(`${weekStart}T00:00:00`).getTime() + 7 * 24 * 60 * 60 * 1000));
    const result = await apiClient.POST("/api/staff-schedules/copy-week", {
      body: { locationId, sourceWeekStart: weekStart, targetWeekStart },
    });
    if (!result.response.ok) {
      setNotice(t("genericError"));
      return;
    }
    const body = result.data as unknown as CopyWeekResponse;
    setNotice(body.skipped.length > 0 ? t("copySkippedSummary", { count: body.skipped.length }) : "");
    await load();
  }

  function navigateWeek(deltaWeeks: number) {
    const next = new Date(`${weekStart}T00:00:00`);
    next.setDate(next.getDate() + deltaWeeks * 7);
    setWeekStart(mondayOf(next));
  }

  // FR-001: publishes/unpublishes every entry for this location+week — behaviorally
  // week-granular even though IsPublished is per-row (research.md R4).
  async function togglePublish() {
    setPublishing(true);
    const result = await apiClient.POST("/api/staff-schedules/{locationId}/publish", {
      params: { path: { locationId } },
      body: { weekStart, unpublish: weekIsPublished },
    });
    setPublishing(false);
    if (!result.response.ok) {
      setNotice(t("genericError"));
      return;
    }
    await load();
  }

  return (
    <div>
      <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
        <div className="flex flex-wrap items-center gap-3">
          <select
            value={locationId}
            onChange={(e) => setLocationId(e.target.value)}
            aria-label={t("locationLabel")}
            className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
          >
            {locations.map((location) => (
              <option key={location.id} value={location.id}>{location.name}</option>
            ))}
          </select>
          <div className="flex items-center gap-1">
            <Button
              variant="secondary"
              size="sm"
              onClick={() => navigateWeek(-1)}
              aria-label={t("weekNav.previous")}
            >
              <ChevronLeft className="h-4 w-4" strokeWidth={2} />
            </Button>
            <span className="min-w-32 text-center text-sm font-medium text-text dark:text-text-dark">
              {t("weekLabel", { date: weekStart })}
            </span>
            <Button
              variant="secondary"
              size="sm"
              onClick={() => navigateWeek(1)}
              aria-label={t("weekNav.next")}
            >
              <ChevronRight className="h-4 w-4" strokeWidth={2} />
            </Button>
          </div>
          <Button variant="secondary" onClick={() => setCopyConfirmOpen(true)}>
            {t("copyWeek")}
          </Button>
          <Button
            variant={weekIsPublished ? "secondary" : "primary"}
            onClick={togglePublish}
            disabled={publishing || entries.length === 0}
          >
            {weekIsPublished ? t("unpublish") : t("publish")}
          </Button>
          {weekIsPublished && <Badge variant="success">{t("publishedBadge")}</Badge>}
        </div>
      </div>

      {notice && (
        <div className="mb-4 rounded-lg bg-surface-soft p-3 text-sm text-text dark:bg-surface-soft-dark dark:text-text-dark">
          {notice}
        </div>
      )}

      {uncoveredToday.length > 0 && (
        <div className="mb-4 flex flex-col gap-3 rounded-lg bg-danger-bg p-4 dark:bg-danger-bg-dark">
          <div className="flex items-center gap-2 text-sm font-medium text-danger dark:text-danger-dark">
            <TriangleAlert className="h-4 w-4" strokeWidth={2} />
            {t("sickCoverBanner.title", { count: uncoveredToday.length })}
          </div>
          <div className="flex flex-wrap gap-2">
            {uncoveredToday.map((entry) => {
              const member = staff.find((s) => s.id === entry.staffProfileId);
              return (
                <Button key={entry.id} variant="destructive" size="sm" onClick={() => setCoveringEntry(entry)}>
                  {t("sickCoverBanner.assignCover", { name: member ? `${member.firstName} ${member.lastName}` : "" })}
                </Button>
              );
            })}
          </div>
        </div>
      )}

      {state === "loading" && <div className="h-64 rounded-lg bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}
      {state === "loaded" && visibleStaff.length === 0 && <EmptyState icon={CalendarDays} message={t("emptyState")} />}
      {state === "loaded" && visibleStaff.length > 0 && (
        <SchedulingGrid
          weekDates={weekDates}
          staff={visibleStaff}
          entries={entries}
          groupsById={groupsById}
          projectedOnDutyByDate={projectedOnDutyByDate}
          closureDates={closureDates}
          onAddShift={openCreateDialog}
          onSelectShift={openEditDialog}
        />
      )}

      <SickCoverDialog
        open={coveringEntry !== null}
        entry={coveringEntry}
        onOpenChange={(open) => {
          if (!open) setCoveringEntry(null);
        }}
        onAssigned={async () => {
          setCoveringEntry(null);
          await load();
        }}
      />

      <ScheduleEntryDialog
        open={dialogOpen}
        entry={editing}
        groups={groups}
        onOpenChange={(open) => {
          setDialogOpen(open);
          if (!open) {
            setEditing(null);
            setCreatingFor(null);
          }
        }}
        onSubmit={saveEntry}
        onDelete={deleteEntry}
        onMarkAbsence={markAbsence}
        saving={saving}
      />

      <ConfirmDialog
        open={copyConfirmOpen}
        onOpenChange={setCopyConfirmOpen}
        title={t("copyWeekTitle")}
        description={t("copyWeekDescription")}
        confirmLabel={t("copyWeekConfirm")}
        cancelLabel={t("cancel")}
        onConfirm={copyWeek}
      />
    </div>
  );
}
