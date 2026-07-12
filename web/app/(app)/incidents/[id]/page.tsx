"use client";
import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { ArrowLeft, Download } from "lucide-react";
import { apiClient, getAccessToken } from "../../../../lib/apiClient";
import { Button } from "../../../../components/ui/button";
import { Textarea } from "../../../../components/ui/textarea";
import { ErrorState } from "../../../../components/ErrorState";
import type { IncidentReportResponse } from "../../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

function formatDateTime(value: string | null): string {
  return value ? new Date(value).toLocaleString() : "—";
}

function Field({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="space-y-1">
      <p className="text-sm font-medium text-text-soft dark:text-text-soft-dark">{label}</p>
      <p className="text-text dark:text-text-dark">{value}</p>
    </div>
  );
}

export default function IncidentDetailPage() {
  const t = useTranslations("incidents");
  const router = useRouter();
  const params = useParams<{ id: string }>();
  const [report, setReport] = useState<IncidentReportResponse | null>(null);
  const [state, setState] = useState<LoadState>("loading");
  const [followUp, setFollowUp] = useState("");
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState("");
  const [exporting, setExporting] = useState(false);

  const load = useCallback(async () => {
    setState("loading");
    // Feature 013b, research.md R3: this GET is itself the "mark reviewed" action — no separate
    // click required.
    const result = await apiClient.GET("/api/incident-reports/{id}", { params: { path: { id: params.id } } });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    const data = result.data as unknown as IncidentReportResponse;
    setReport(data);
    setFollowUp(data.followUp ?? "");
    setState("loaded");
  }, [params.id]);

  useEffect(() => {
    load();
  }, [load]);

  async function saveFollowUp() {
    if (!report) return;
    setSaving(true);
    setNotice("");
    // Only `followUp` is populated — every other field is omitted (null) so the server applies
    // just this one change, regardless of the report's age (FR-006).
    const result = await apiClient.PUT("/api/incident-reports/{id}", {
      params: { path: { id: report.id } },
      body: {
        occurredAt: null,
        locationDetail: null,
        description: null,
        injuryType: null,
        firstAidGiven: null,
        doctorCalled: null,
        doctorNotes: null,
        parentNotified: null,
        parentNotifiedAt: null,
        parentNotifiedHow: null,
        witnesses: null,
        followUp,
      },
    });
    setSaving(false);
    if (!result.response.ok) {
      setNotice(t("saveError"));
      return;
    }
    setReport(result.data as unknown as IncidentReportResponse);
    setNotice(t("followUpSaved"));
  }

  async function exportPdf() {
    if (!report) return;
    setExporting(true);
    try {
      const response = await fetch(`${API_BASE}/api/incident-reports/${report.id}/pdf`, {
        headers: { Authorization: `Bearer ${getAccessToken()}` },
      });
      if (!response.ok) throw new Error("export failed");
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `incident-report-${report.id}.pdf`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch {
      setNotice(t("exportPdfError"));
    } finally {
      setExporting(false);
    }
  }

  if (state === "loading") return <div className="h-64 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />;
  if (state === "error" || !report) return <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />;

  const isLocked = Date.now() - new Date(report.createdAt).getTime() > 24 * 60 * 60 * 1000;

  return (
    <div>
      <button
        onClick={() => router.push("/incidents")}
        className="mb-4 flex items-center gap-2 text-sm text-text-soft hover:text-text dark:text-text-soft-dark dark:hover:text-text-dark"
      >
        <ArrowLeft className="h-4 w-4" strokeWidth={2} />
        {t("backToList")}
      </button>

      <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
        <Button variant="secondary" onClick={exportPdf} disabled={exporting} className="inline-flex items-center gap-2">
          <Download className="h-4 w-4" strokeWidth={2} />
          {t("exportPdf")}
        </Button>
      </div>

      {isLocked && (
        <div className="mb-6 rounded-lg bg-surface-soft p-3 text-sm text-text-soft dark:bg-surface-soft-dark dark:text-text-soft-dark">
          {t("lockedNotice")}
        </div>
      )}

      <div className="grid max-w-2xl grid-cols-1 gap-4 sm:grid-cols-2">
        <Field label={t("fieldOccurredAt")} value={formatDateTime(report.occurredAt)} />
        <Field label={t("fieldCreatedAt")} value={formatDateTime(report.createdAt)} />
        <Field label={t("fieldInjuryType")} value={t(`injuryTypes.${report.injuryType}`)} />
        <Field label={t("fieldLocationDetail")} value={report.locationDetail ?? t("none")} />
        <Field label={t("fieldDoctorCalled")} value={report.doctorCalled ? t("yes") : t("no")} />
        <Field label={t("fieldParentNotified")} value={report.parentNotified ? t("yes") : t("no")} />
        {report.parentNotifiedAt && <Field label={t("fieldParentNotifiedAt")} value={formatDateTime(report.parentNotifiedAt)} />}
        {report.parentNotifiedHow && (
          <Field label={t("fieldParentNotifiedHow")} value={t(`parentNotifiedHows.${report.parentNotifiedHow}`)} />
        )}
      </div>

      <div className="mt-4 max-w-2xl space-y-4">
        <Field label={t("fieldDescription")} value={report.description} />
        {report.firstAidGiven && <Field label={t("fieldFirstAidGiven")} value={report.firstAidGiven} />}
        {report.doctorNotes && <Field label={t("fieldDoctorNotes")} value={report.doctorNotes} />}
        {report.witnesses && <Field label={t("fieldWitnesses")} value={report.witnesses} />}
      </div>

      <div className="mt-6 max-w-2xl space-y-2">
        <label htmlFor="follow-up" className="text-sm font-medium text-text dark:text-text-dark">
          {t("fieldFollowUp")}
        </label>
        <Textarea
          id="follow-up"
          value={followUp}
          onChange={(e) => setFollowUp(e.target.value)}
          placeholder={t("followUpPlaceholder")}
          rows={4}
        />
        {notice && <p className="text-sm text-text-soft dark:text-text-soft-dark">{notice}</p>}
        <Button onClick={saveFollowUp} disabled={saving}>{t("followUpSave")}</Button>
      </div>
    </div>
  );
}
