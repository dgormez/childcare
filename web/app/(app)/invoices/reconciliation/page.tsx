"use client";
import { useCallback, useEffect, useRef, useState } from "react";
import { useTranslations } from "next-intl";
import { Landmark, Upload } from "lucide-react";
import { apiClient } from "../../../../lib/apiClient";
import { Button } from "../../../../components/ui/button";
import { CodaTransactionTable } from "../../../../components/invoices/CodaTransactionTable";
import { EmptyState } from "../../../../components/EmptyState";
import { ErrorState } from "../../../../components/ErrorState";
import type { CodaImportSummaryResponse, CodaTransactionResponse } from "../../../../lib/types";

type LoadState = "loading" | "loaded" | "error";
type Filter = "needsReview" | "all";

export default function CodaReconciliationPage() {
  const t = useTranslations("invoices.codaReconciliation");
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [filter, setFilter] = useState<Filter>("needsReview");
  const [transactions, setTransactions] = useState<CodaTransactionResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");
  const [uploading, setUploading] = useState(false);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [notice, setNotice] = useState("");
  const [importSummary, setImportSummary] = useState<CodaImportSummaryResponse | null>(null);

  const load = useCallback(async () => {
    setState("loading");
    const result = await apiClient.GET("/api/coda-transactions", {
      params: { query: filter === "needsReview" ? { needsReview: true } : {} },
    });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setTransactions(result.data as unknown as CodaTransactionResponse[]);
    setState("loaded");
  }, [filter]);

  useEffect(() => {
    load();
  }, [load]);

  async function handleFileSelected(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    e.target.value = "";

    setUploading(true);
    setNotice("");
    setImportSummary(null);
    // openapi-typescript represents ASP.NET Core's IFormFile as `string` (the OpenAPI binary
    // schema has no dedicated JS type) — openapi-fetch accepts a real File/Blob at runtime for a
    // multipart field regardless, so the cast here just bridges that declared-vs-actual gap.
    const result = await apiClient.POST("/api/coda-imports", { body: { file: file as unknown as string } });
    setUploading(false);

    if (!result.response.ok) {
      setNotice(t("uploadError"));
      return;
    }
    const summary = result.data as unknown as CodaImportSummaryResponse;
    setImportSummary(summary);
    setNotice(t("importSummary", { count: summary.transactionCount }));
    await load();
  }

  async function handleConfirm(id: string) {
    setBusyId(id);
    const result = await apiClient.POST("/api/coda-transactions/{id}/confirm", { params: { path: { id } } });
    setBusyId(null);
    setNotice(result.response.ok ? t("confirmSuccess") : t("confirmError"));
    await load();
  }

  async function handleReject(id: string) {
    setBusyId(id);
    const result = await apiClient.POST("/api/coda-transactions/{id}/reject", { params: { path: { id } } });
    setBusyId(null);
    setNotice(result.response.ok ? t("rejectSuccess") : t("rejectError"));
    await load();
  }

  async function handleReview(id: string) {
    setBusyId(id);
    const result = await apiClient.POST("/api/coda-transactions/{id}/review", { params: { path: { id } } });
    setBusyId(null);
    setNotice(result.response.ok ? t("reviewSuccess") : t("reviewError"));
    await load();
  }

  return (
    <div>
      <div className="mb-6 flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
        <div className="flex items-center gap-3">
          <select
            value={filter}
            onChange={(e) => setFilter(e.target.value as Filter)}
            aria-label={t("needsReviewFilter")}
            className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
          >
            <option value="needsReview">{t("needsReviewFilter")}</option>
            <option value="all">{t("allFilter")}</option>
          </select>
          <input ref={fileInputRef} type="file" accept=".coda,.cod,text/plain" className="hidden" onChange={handleFileSelected} />
          <Button onClick={() => fileInputRef.current?.click()} disabled={uploading}>
            <Upload className="h-4 w-4" strokeWidth={2} />
            {uploading ? t("uploading") : t("uploadButton")}
          </Button>
        </div>
      </div>

      {notice && <p className="mb-4 text-sm text-text-soft dark:text-text-soft-dark">{notice}</p>}

      {importSummary && (
        <div className="mb-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
          <SummaryTile label={t("summaryOgm")} value={importSummary.summary.ogm} />
          <SummaryTile label={t("summaryIbanAmountSuggested")} value={importSummary.summary.ibanAmountSuggested} />
          <SummaryTile label={t("summaryUnmatched")} value={importSummary.summary.unmatched} />
          <SummaryTile label={t("summaryDuplicate")} value={importSummary.summary.duplicate} />
          <SummaryTile label={t("summaryClosedInvoice")} value={importSummary.summary.closedInvoice} />
          <SummaryTile label={t("summaryReversal")} value={importSummary.summary.reversal} />
          <SummaryTile label={t("summarySkipped")} value={importSummary.skippedDuplicateCount} />
        </div>
      )}

      {state === "loading" && <div className="h-64 rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}
      {state === "loaded" && transactions.length === 0 && <EmptyState icon={Landmark} message={t("emptyState")} />}
      {state === "loaded" && transactions.length > 0 && (
        <CodaTransactionTable
          transactions={transactions}
          onConfirm={handleConfirm}
          onReject={handleReject}
          onReview={handleReview}
          busyId={busyId}
        />
      )}
    </div>
  );
}

function SummaryTile({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-xl bg-surface-soft p-4 dark:bg-surface-soft-dark">
      <div className="text-2xl font-semibold tabular-nums text-text dark:text-text-dark">{value}</div>
      <div className="text-xs text-text-soft dark:text-text-soft-dark">{label}</div>
    </div>
  );
}
