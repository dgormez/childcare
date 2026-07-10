"use client";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslations } from "next-intl";
import { CalendarDays, ChevronLeft, ChevronRight } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { SchedulingGrid } from "../../../components/SchedulingGrid";
import { ScheduleEntryDialog } from "../../../components/ScheduleEntryDialog";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import { Button } from "../../../components/ui/button";
import type {
  AbsenceReason,
  ApiErrorBody,
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
  const [projectedOnDutyByDate, setProjectedOnDutyByDate] = useState<Map<string, number>>(new Map());
  const [weekStart, setWeekStart] = useState(() => mondayOf(new Date()));
  const [state, setState] = useState<LoadState>("loading");
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<StaffScheduleResponse | null>(null);
  const [creatingFor, setCreatingFor] = useState<{ staffId: string; date: string } | null>(null);
  const [saving, setSaving] = useState(false);
  const [copyConfirmOpen, setCopyConfirmOpen] = useState(false);
  const [notice, setNotice] = useState("");

  const weekDates = useMemo(() => weekDatesFrom(weekStart), [weekStart]);
  const groupsById = useMemo(() => new Map(groups.map((g) => [g.id, g.name])), [groups]);
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
      const [staffResult, groupsResult, entriesResult] = await Promise.all([
        apiClient.GET("/api/staff", { params: { query: { includeDeactivated: true } } }),
        apiClient.GET("/api/groups", { params: { query: { locationId } } }),
        apiClient.GET("/api/staff-schedules", { params: { query: { locationId, weekStart } } }),
      ]);
      if (!staffResult.response.ok || !groupsResult.response.ok || !entriesResult.response.ok) {
        setState("error");
        return;
      }
      setStaff(staffResult.data as unknown as StaffResponse[]);
      setGroups(groupsResult.data as unknown as GroupResponse[]);
      const loadedEntries = entriesResult.data as unknown as StaffScheduleResponse[];
      setEntries(loadedEntries);

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
        </div>
      </div>

      {notice && (
        <div className="mb-4 rounded-lg bg-surface-soft p-3 text-sm text-text dark:bg-surface-soft-dark dark:text-text-dark">
          {notice}
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
          onAddShift={openCreateDialog}
          onSelectShift={openEditDialog}
        />
      )}

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
