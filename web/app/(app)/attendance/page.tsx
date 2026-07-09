"use client";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslations } from "next-intl";
import { CalendarClock } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { AttendanceTable } from "../../../components/AttendanceTable";
import { AttendanceCorrectionDialog } from "../../../components/AttendanceCorrectionDialog";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import type { AttendanceRecordResponse, AttendanceStatus, LocationResponse, PagedAttendanceResponse } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

interface ChildSummary {
  id: string;
  firstName: string;
  lastName: string;
}

function todayDateString(): string {
  return new Date().toISOString().slice(0, 10);
}

export default function AttendancePage() {
  const t = useTranslations("attendance");
  const [locations, setLocations] = useState<LocationResponse[]>([]);
  const [locationId, setLocationId] = useState<string>("");
  const [date, setDate] = useState(todayDateString());
  const [records, setRecords] = useState<AttendanceRecordResponse[]>([]);
  const [childNamesById, setChildNamesById] = useState<Map<string, string>>(new Map());
  const [state, setState] = useState<LoadState>("loading");
  const [correctionTarget, setCorrectionTarget] = useState<AttendanceRecordResponse | null>(null);

  useEffect(() => {
    apiClient.GET("/api/locations").then((result) => {
      if (!result.response.ok) return;
      const fetched = result.data as unknown as LocationResponse[];
      setLocations(fetched);
      if (fetched.length > 0) setLocationId((current) => current || fetched[0].id);
    });
  }, []);

  const load = useCallback(async () => {
    if (!locationId) return;
    setState("loading");
    try {
      const [attendanceResult, childrenResult] = await Promise.all([
        apiClient.GET("/api/attendance", { params: { query: { locationId, date } } }),
        apiClient.GET("/api/children"),
      ]);
      if (!attendanceResult.response.ok) {
        setState("error");
        return;
      }
      const page = attendanceResult.data as unknown as PagedAttendanceResponse;
      setRecords(page.items);
      if (childrenResult.response.ok) {
        const children = childrenResult.data as unknown as ChildSummary[];
        setChildNamesById(new Map(children.map((c) => [c.id, `${c.firstName} ${c.lastName}`])));
      }
      setState("loaded");
    } catch {
      setState("error");
    }
  }, [locationId, date]);

  useEffect(() => {
    load();
  }, [load]);

  const handleCorrectionSubmit = async (changes: {
    status?: AttendanceStatus;
    checkInAt?: string | null;
    checkOutAt?: string | null;
    absenceJustified?: boolean;
    absenceReason?: string | null;
  }): Promise<{ ok: true } | { ok: false; errorKey: string }> => {
    if (!correctionTarget) return { ok: false, errorKey: "errors.unexpected" };
    const result = await apiClient.PATCH("/api/attendance/{id}", {
      params: { path: { id: correctionTarget.id } },
      body: {
        status: changes.status ?? null,
        checkInAt: changes.checkInAt ?? null,
        checkOutAt: changes.checkOutAt ?? null,
        absenceJustified: changes.absenceJustified ?? null,
        absenceReason: changes.absenceReason ?? null,
      },
    });
    if (result.response.ok) {
      await load();
      return { ok: true };
    }
    const errorKey = (result.error as { errorKey?: string } | undefined)?.errorKey ?? "errors.unexpected";
    return { ok: false, errorKey };
  };

  const correctionChildName = useMemo(
    () => (correctionTarget ? childNamesById.get(correctionTarget.childId) ?? "" : ""),
    [correctionTarget, childNamesById]
  );

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

      {state === "loaded" && records.length === 0 && (
        <EmptyState icon={CalendarClock} message={t("emptyState")} />
      )}

      {state === "loaded" && records.length > 0 && (
        <AttendanceTable records={records} childNamesById={childNamesById} onCorrect={setCorrectionTarget} />
      )}

      <AttendanceCorrectionDialog
        record={correctionTarget}
        childName={correctionChildName}
        onOpenChange={(open) => !open && setCorrectionTarget(null)}
        onSubmit={handleCorrectionSubmit}
      />
    </div>
  );
}
