"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { ClipboardCheck } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { LeaveRequestTable } from "../../../components/LeaveRequestTable";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import type { StaffLeaveRequestResponse, StaffLeaveRequestStatus, StaffResponse } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";
type StatusFilter = "pending" | "all" | StaffLeaveRequestStatus;

/** FR-010: director's "Verlofaanvragen" approval queue (contracts/staff-app-api.md). */
export default function LeaveRequestsPage() {
  const t = useTranslations("leaveRequests");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("pending");
  const [requests, setRequests] = useState<StaffLeaveRequestResponse[]>([]);
  const [staffNamesById, setStaffNamesById] = useState<Map<string, string>>(new Map());
  const [state, setState] = useState<LoadState>("loading");
  const [notice, setNotice] = useState("");
  const [approveTarget, setApproveTarget] = useState<StaffLeaveRequestResponse | null>(null);
  const [rejectTarget, setRejectTarget] = useState<StaffLeaveRequestResponse | null>(null);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setState("loading");
    const [requestsResult, staffResult] = await Promise.all([
      apiClient.GET("/api/staff-leave-requests", {
        params: { query: statusFilter === "all" ? {} : { status: statusFilter } },
      }),
      apiClient.GET("/api/staff", { params: { query: { includeDeactivated: true } } }),
    ]);
    if (!requestsResult.response.ok || !staffResult.response.ok) {
      setState("error");
      return;
    }
    setRequests((requestsResult.data ?? []) as unknown as StaffLeaveRequestResponse[]);
    const staff = (staffResult.data ?? []) as unknown as StaffResponse[];
    setStaffNamesById(new Map(staff.map((s) => [s.id, `${s.firstName} ${s.lastName}`])));
    setState("loaded");
  }, [statusFilter]);

  useEffect(() => {
    load();
  }, [load]);

  async function decide(request: StaffLeaveRequestResponse, approve: boolean) {
    setSaving(true);
    const result = await apiClient.POST("/api/staff-leave-requests/{id}/decide", {
      params: { path: { id: request.id } },
      body: { approve },
    });
    setSaving(false);
    if (!result.response.ok) {
      setNotice(t("genericError"));
      return;
    }
    setApproveTarget(null);
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
        </select>
      </div>

      {notice && (
        <div className="mb-4 rounded-lg bg-surface-soft p-3 text-sm text-text dark:bg-surface-soft-dark dark:text-text-dark">
          {notice}
        </div>
      )}

      {state === "loading" && <div className="h-64 rounded-lg bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}
      {state === "loaded" && requests.length === 0 && <EmptyState icon={ClipboardCheck} message={t("emptyState")} />}
      {state === "loaded" && requests.length > 0 && (
        <LeaveRequestTable
          requests={requests}
          staffNamesById={staffNamesById}
          onApprove={setApproveTarget}
          onReject={setRejectTarget}
        />
      )}

      <ConfirmDialog
        open={approveTarget !== null}
        onOpenChange={(open) => !open && setApproveTarget(null)}
        title={t("approveTitle")}
        description={t("approveDescription")}
        confirmLabel={t("approve")}
        cancelLabel={t("cancel")}
        onConfirm={() => approveTarget && decide(approveTarget, true)}
        confirming={saving}
      />

      <ConfirmDialog
        open={rejectTarget !== null}
        onOpenChange={(open) => !open && setRejectTarget(null)}
        title={t("rejectTitle")}
        description={t("rejectDescription")}
        confirmLabel={t("reject")}
        cancelLabel={t("cancel")}
        onConfirm={() => rejectTarget && decide(rejectTarget, false)}
        confirmDestructive
        confirming={saving}
      />
    </div>
  );
}
