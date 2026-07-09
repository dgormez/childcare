"use client";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslations } from "next-intl";
import { CalendarDays, Plus } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { ClosureCalendar } from "../../../components/ClosureCalendar";
import { ClosureDialog } from "../../../components/ClosureDialog";
import { ClosureList } from "../../../components/ClosureList";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import { Button } from "../../../components/ui/button";
import type {
  ApiErrorBody,
  CancelClosureDayResponse,
  ClosureDayResponse,
  ClosureType,
  LocationResponse,
  PublishClosureDayResponse,
} from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

const currentYear = new Date().getFullYear();

function todayDateString(): string {
  return new Date().toISOString().slice(0, 10);
}

export default function ClosuresPage() {
  const t = useTranslations("closures");
  const [locations, setLocations] = useState<LocationResponse[]>([]);
  const [locationId, setLocationId] = useState("");
  const [year, setYear] = useState(currentYear);
  const [closures, setClosures] = useState<ClosureDayResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<ClosureDayResponse | null>(null);
  const [saving, setSaving] = useState(false);
  const [publishTarget, setPublishTarget] = useState<ClosureDayResponse | null>(null);
  const [cancelTarget, setCancelTarget] = useState<ClosureDayResponse | null>(null);
  const [confirmExistingAttendance, setConfirmExistingAttendance] = useState(false);
  const [notice, setNotice] = useState("");

  useEffect(() => {
    (apiClient.GET as any)("/api/locations").then((result: { response: Response; data?: LocationResponse[] }) => {
      if (!result.response.ok || !result.data) return;
      setLocations(result.data);
      if (result.data.length > 0) setLocationId((current) => current || result.data![0].id);
    });
  }, []);

  const load = useCallback(async () => {
    if (!locationId) return;
    setState("loading");
    const result = await (apiClient.GET as any)("/api/closures", { params: { query: { locationId, year } } });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setClosures((result.data ?? []) as ClosureDayResponse[]);
    setState("loaded");
  }, [locationId, year]);

  useEffect(() => {
    load();
  }, [load]);

  const sortedClosures = useMemo(
    () => [...closures].sort((a, b) => a.date.localeCompare(b.date)),
    [closures],
  );

  async function saveClosure(values: { date: string; label: string; closureType: ClosureType; notifyParents: boolean }) {
    if (!locationId) return;
    setSaving(true);
    const result = editing
      ? await (apiClient.PATCH as any)("/api/closures/{id}", {
          params: { path: { id: editing.id } },
          body: { label: values.label, closureType: values.closureType, notifyParents: values.notifyParents },
        })
      : await (apiClient.POST as any)("/api/closures", {
          body: {
            locationId,
            date: values.date,
            label: values.label,
            closureType: values.closureType,
            notifyParents: values.notifyParents,
          },
        });
    setSaving(false);
    if (!result.response.ok) {
      const errorKey = ((result.error ?? {}) as ApiErrorBody).errorKey ?? "";
      setNotice(errorKey === "errors.closures.duplicate_date"
        ? t("duplicateDate")
        : errorKey === "errors.closures.past_date"
          ? t("pastDate")
          : t("genericError"));
      return;
    }
    setDialogOpen(false);
    setEditing(null);
    setNotice("");
    await load();
  }

  async function publishClosure(target: ClosureDayResponse, confirm: boolean) {
    const result = await (apiClient.POST as any)("/api/closures/{id}/publish", {
      params: { path: { id: target.id } },
      body: { confirmExistingAttendance: confirm },
    });
    if (result.response.status === 409) {
      setConfirmExistingAttendance(true);
      setNotice(t("attendanceConfirmationRequired"));
      return;
    }
    if (!result.response.ok) {
      setNotice(t("genericError"));
      return;
    }
    const body = result.data as PublishClosureDayResponse;
    setNotice(t("publishSummary", {
      messages: body.notificationSummary.messagesCreated,
      failed: body.notificationSummary.pushFailed,
    }));
    setPublishTarget(null);
    setConfirmExistingAttendance(false);
    await load();
  }

  async function cancelClosure(target: ClosureDayResponse) {
    const result = await (apiClient.POST as any)("/api/closures/{id}/cancel", {
      params: { path: { id: target.id } },
    });
    if (!result.response.ok) {
      setNotice(t("genericError"));
      return;
    }
    const body = result.data as CancelClosureDayResponse | undefined;
    setNotice(body ? t("cancelSummary", {
      messages: body.notificationSummary.messagesCreated,
      released: body.attendanceRecordsReleased,
    }) : t("draftRemoved"));
    setCancelTarget(null);
    await load();
  }

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
            {locations.map((location) => (
              <option key={location.id} value={location.id}>{location.name}</option>
            ))}
          </select>
          <input
            type="number"
            value={year}
            onChange={(e) => setYear(Number(e.target.value))}
            aria-label={t("yearLabel")}
            className="h-10 w-24 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
          />
          <Button onClick={() => { setEditing(null); setDialogOpen(true); }}>
            <Plus className="h-4 w-4" strokeWidth={2} />
            {t("add")}
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
      {state === "loaded" && sortedClosures.length === 0 && <EmptyState icon={CalendarDays} message={t("emptyState")} />}
      {state === "loaded" && sortedClosures.length > 0 && (
        <div className="grid grid-cols-[1fr_20rem] gap-6">
          <ClosureCalendar year={year} closures={sortedClosures} onSelect={(closure) => { setEditing(closure); setDialogOpen(true); }} />
          <ClosureList
            closures={sortedClosures}
            onEdit={(closure) => { setEditing(closure); setDialogOpen(true); }}
            onPublish={(closure) => { setPublishTarget(closure); setConfirmExistingAttendance(false); }}
            onCancel={setCancelTarget}
          />
        </div>
      )}

      <ClosureDialog
        open={dialogOpen}
        closure={editing}
        defaultDate={`${year}-${todayDateString().slice(5)}`}
        onOpenChange={(open) => { setDialogOpen(open); if (!open) setEditing(null); }}
        onSubmit={saveClosure}
        saving={saving}
      />

      <ConfirmDialog
        open={publishTarget !== null}
        onOpenChange={(open) => !open && setPublishTarget(null)}
        title={t("publishTitle")}
        description={confirmExistingAttendance ? t("publishAttendanceDescription") : t("publishDescription")}
        confirmLabel={confirmExistingAttendance ? t("publishConfirmAttendance") : t("publish")}
        cancelLabel={t("dismiss")}
        onConfirm={() => publishTarget && publishClosure(publishTarget, confirmExistingAttendance)}
      />

      <ConfirmDialog
        open={cancelTarget !== null}
        onOpenChange={(open) => !open && setCancelTarget(null)}
        title={cancelTarget?.status === "draft" ? t("removeTitle") : t("cancelTitle")}
        description={cancelTarget?.status === "draft" ? t("removeDescription") : t("cancelDescription")}
        confirmLabel={cancelTarget?.status === "draft" ? t("remove") : t("cancelClosure")}
        cancelLabel={t("dismiss")}
        onConfirm={() => cancelTarget && cancelClosure(cancelTarget)}
        confirmDestructive
      />
    </div>
  );
}
