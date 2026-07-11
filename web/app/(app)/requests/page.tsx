"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Inbox } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { DayReservationsTable } from "../../../components/DayReservationsTable";
import { ApproveDayReservationDialog } from "../../../components/ApproveDayReservationDialog";
import { RejectDayReservationDialog } from "../../../components/RejectDayReservationDialog";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import type { ApiErrorBody, DayReservationResponse, DayReservationStatus } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";
type StatusFilter = "pending" | "all" | DayReservationStatus;

export default function DayReservationsPage() {
  const t = useTranslations("dayReservations");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("pending");
  const [reservations, setReservations] = useState<DayReservationResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");
  const [notice, setNotice] = useState("");
  const [approveTarget, setApproveTarget] = useState<DayReservationResponse | null>(null);
  const [rejectTarget, setRejectTarget] = useState<DayReservationResponse | null>(null);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setState("loading");
    const result = await (apiClient.GET as any)("/api/day-reservations", { params: { query: { status: statusFilter } } });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setReservations((result.data ?? []) as DayReservationResponse[]);
    setState("loaded");
  }, [statusFilter]);

  useEffect(() => {
    load();
  }, [load]);

  function errorKeyToMessage(errorKey: string): string {
    switch (errorKey) {
      case "errors.day_reservations.not_pending":
        return t("notPending");
      case "errors.day_reservations.closure_day":
        return t("closureDayConflict");
      case "errors.day_reservations.no_contracted_location":
        return t("noContractedLocation");
      default:
        return t("genericError");
    }
  }

  async function confirmApprove(absenceJustified: boolean | null) {
    if (!approveTarget) return;
    setSaving(true);
    const result = await (apiClient.POST as any)("/api/day-reservations/{id}/approve", {
      params: { path: { id: approveTarget.id } },
      body: { absenceJustified },
    });
    setSaving(false);
    if (!result.response.ok) {
      const errorKey = ((result.error ?? {}) as ApiErrorBody).errorKey ?? "";
      setNotice(errorKeyToMessage(errorKey));
      return;
    }
    setApproveTarget(null);
    setNotice("");
    await load();
  }

  async function confirmReject(directorNotes: string | null) {
    if (!rejectTarget) return;
    setSaving(true);
    const result = await (apiClient.POST as any)("/api/day-reservations/{id}/reject", {
      params: { path: { id: rejectTarget.id } },
      body: { directorNotes },
    });
    setSaving(false);
    if (!result.response.ok) {
      const errorKey = ((result.error ?? {}) as ApiErrorBody).errorKey ?? "";
      setNotice(errorKeyToMessage(errorKey));
      return;
    }
    setRejectTarget(null);
    setNotice("");
    await load();
  }

  return (
    <div>
      <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value as StatusFilter)}
          aria-label={t("statusLabel")}
          className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
        >
          <option value="pending">{t("statusFilter.pending")}</option>
          <option value="all">{t("statusFilter.all")}</option>
          <option value="approved">{t("statusFilter.approved")}</option>
          <option value="rejected">{t("statusFilter.rejected")}</option>
          <option value="cancelled">{t("statusFilter.cancelled")}</option>
        </select>
      </div>

      {notice && (
        <div className="mb-4 rounded-lg bg-surface-soft p-3 text-sm text-text dark:bg-surface-soft-dark dark:text-text-dark">
          {notice}
        </div>
      )}

      {state === "loading" && <div className="h-64 rounded-lg bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}
      {state === "loaded" && reservations.length === 0 && <EmptyState icon={Inbox} message={t("emptyState")} />}
      {state === "loaded" && reservations.length > 0 && (
        <DayReservationsTable reservations={reservations} onApprove={setApproveTarget} onReject={setRejectTarget} />
      )}

      <ApproveDayReservationDialog
        open={approveTarget !== null}
        reservation={approveTarget}
        onOpenChange={(open) => !open && setApproveTarget(null)}
        onConfirm={confirmApprove}
        saving={saving}
      />

      <RejectDayReservationDialog
        open={rejectTarget !== null}
        reservation={rejectTarget}
        onOpenChange={(open) => !open && setRejectTarget(null)}
        onConfirm={confirmReject}
        saving={saving}
      />
    </div>
  );
}
