"use client";
import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { CreditCard, ArrowLeft, Download } from "lucide-react";
import { apiClient, getAccessToken } from "../../../../lib/apiClient";
import { Button } from "../../../../components/ui/button";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../../../../components/ui/table";
import { EmptyState } from "../../../../components/EmptyState";
import { ErrorState } from "../../../../components/ErrorState";
import type { SepaBatchEligibilityResponse, SepaBatchResponse, LocationResponse } from "../../../../lib/types";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

type LoadState = "loading" | "loaded" | "error";

function currentYearMonth(): { year: number; month: number } {
  const now = new Date();
  return { year: now.getFullYear(), month: now.getMonth() + 1 };
}

function formatCents(cents: number): string {
  return (cents / 100).toLocaleString(undefined, { style: "currency", currency: "EUR" });
}

function tomorrowIso(): string {
  const d = new Date();
  d.setDate(d.getDate() + 1);
  return d.toISOString().slice(0, 10);
}

export default function SepaBatchesPage() {
  const t = useTranslations("invoices.sepaBatches");
  const [locations, setLocations] = useState<LocationResponse[]>([]);
  const [locationId, setLocationId] = useState<string>("");
  const [{ year, month }, setYearMonth] = useState(currentYearMonth());
  const [eligibility, setEligibility] = useState<SepaBatchEligibilityResponse | null>(null);
  const [batches, setBatches] = useState<SepaBatchResponse[]>([]);
  const [selected, setSelected] = useState<Record<string, boolean>>({});
  const [executionDate, setExecutionDate] = useState(tomorrowIso());
  const [state, setState] = useState<LoadState>("loading");
  const [generating, setGenerating] = useState(false);
  const [notice, setNotice] = useState("");

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
    const monthValue = `${year}-${String(month).padStart(2, "0")}-01`;
    const [eligibilityResult, batchesResult] = await Promise.all([
      apiClient.GET("/api/locations/{locationId}/sepa-batch-eligibility", {
        params: { path: { locationId }, query: { month: monthValue } },
      }),
      apiClient.GET("/api/locations/{locationId}/sepa-batches", { params: { path: { locationId } } }),
    ]);
    if (!eligibilityResult.response.ok || !batchesResult.response.ok) {
      setState("error");
      return;
    }
    const eligibilityData = eligibilityResult.data as unknown as SepaBatchEligibilityResponse;
    setEligibility(eligibilityData);
    setBatches(batchesResult.data as unknown as SepaBatchResponse[]);
    setSelected(Object.fromEntries(eligibilityData.eligible.map((e) => [e.invoiceId, true])));
    setState("loaded");
  }, [locationId, year, month]);

  useEffect(() => {
    load();
  }, [load]);

  const selectedIds = Object.entries(selected).filter(([, v]) => v).map(([id]) => id);
  const minExecutionDate = tomorrowIso();

  async function generate() {
    if (selectedIds.length === 0) return;
    setGenerating(true);
    setNotice("");
    try {
      const response = await fetch(`${API_BASE}/api/locations/${locationId}/sepa-batches`, {
        method: "POST",
        headers: { "Content-Type": "application/json", Authorization: `Bearer ${getAccessToken()}` },
        body: JSON.stringify({ invoiceIds: selectedIds, executionDate }),
      });
      if (!response.ok) {
        const body = await response.json().catch(() => null);
        setNotice(t(errorKeyToMessageKey(body?.errorKey)));
        return;
      }
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `sepa-batch-${locationId}.xml`;
      anchor.click();
      URL.revokeObjectURL(url);
      setNotice(t("generateSuccess", { count: selectedIds.length }));
      await load();
    } catch {
      setNotice(t("generateError"));
    } finally {
      setGenerating(false);
    }
  }

  function errorKeyToMessageKey(errorKey: string | undefined): string {
    switch (errorKey) {
      case "errors.sepa_batch.creditor_not_configured":
        return "creditorNotConfiguredError";
      case "errors.sepa_batch.execution_date_too_soon":
        return "executionDateTooSoonError";
      case "errors.sepa_batch.invoice_not_eligible":
        return "invoiceNotEligibleError";
      case "errors.sepa_batch.no_invoices_selected":
        return "noInvoicesSelectedError";
      default:
        return "generateError";
    }
  }

  return (
    <div>
      <Link
        href="/invoices"
        className="mb-4 flex items-center gap-2 text-sm text-text-soft hover:text-text dark:text-text-soft-dark dark:hover:text-text-dark"
      >
        <ArrowLeft className="h-4 w-4" strokeWidth={2} />
        {t("backToInvoices")}
      </Link>

      <div className="mb-6 flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
        <div className="flex items-center gap-3">
          {locations.length > 1 && (
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
          )}
          <input
            type="month"
            value={`${year}-${String(month).padStart(2, "0")}`}
            onChange={(e) => {
              const [y, m] = e.target.value.split("-").map(Number);
              setYearMonth({ year: y, month: m });
            }}
            aria-label={t("monthLabel")}
            className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
          />
        </div>
      </div>

      {state === "loading" && <div className="h-64 rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}

      {state === "loaded" && eligibility && !eligibility.creditorConfigured && (
        <EmptyState icon={CreditCard} message={t("creditorNotConfiguredMessage")} />
      )}

      {state === "loaded" && eligibility && eligibility.creditorConfigured && (
        <>
          {eligibility.eligible.length === 0 && eligibility.excluded.length === 0 && (
            <EmptyState icon={CreditCard} message={t("emptyState")} />
          )}

          {eligibility.eligible.length > 0 && (
            <div className="mb-8">
              <h2 className="mb-3 text-sm font-semibold text-text dark:text-text-dark">{t("eligibleSectionTitle")}</h2>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-10"></TableHead>
                    <TableHead>{t("columnChild")}</TableHead>
                    <TableHead>{t("columnAmount")}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {eligibility.eligible.map((row) => (
                    <TableRow key={row.invoiceId}>
                      <TableCell>
                        <input
                          type="checkbox"
                          checked={!!selected[row.invoiceId]}
                          onChange={(e) => setSelected((current) => ({ ...current, [row.invoiceId]: e.target.checked }))}
                          aria-label={row.childName}
                        />
                      </TableCell>
                      <TableCell className="font-medium">{row.childName}</TableCell>
                      <TableCell className="tabular-nums">{formatCents(row.totalCents)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>

              <div className="mt-4 flex items-center gap-3">
                <input
                  type="date"
                  value={executionDate}
                  min={minExecutionDate}
                  onChange={(e) => setExecutionDate(e.target.value)}
                  aria-label={t("executionDateLabel")}
                  className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
                />
                <Button onClick={generate} disabled={generating || selectedIds.length === 0}>
                  <Download className="h-4 w-4" strokeWidth={2} />
                  {generating ? t("generating") : t("generateButton")}
                </Button>
              </div>
            </div>
          )}

          {eligibility.excluded.length > 0 && (
            <div className="mb-8">
              <h2 className="mb-3 text-sm font-semibold text-text dark:text-text-dark">{t("excludedSectionTitle")}</h2>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t("columnChild")}</TableHead>
                    <TableHead>{t("columnAmount")}</TableHead>
                    <TableHead>{t("columnReason")}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {eligibility.excluded.map((row) => (
                    <TableRow key={row.invoiceId}>
                      <TableCell className="font-medium">{row.childName}</TableCell>
                      <TableCell className="tabular-nums">{formatCents(row.totalCents)}</TableCell>
                      <TableCell className="text-text-soft dark:text-text-soft-dark">{t(`reason${row.reason}`)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}

          {notice && <p className="mb-4 text-sm text-text-soft dark:text-text-soft-dark">{notice}</p>}

          <h2 className="mb-3 text-sm font-semibold text-text dark:text-text-dark">{t("historySectionTitle")}</h2>
          {batches.length === 0 && <EmptyState icon={CreditCard} message={t("noBatchesYet")} />}
          {batches.length > 0 && (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("columnExecutionDate")}</TableHead>
                  <TableHead>{t("columnGeneratedAt")}</TableHead>
                  <TableHead>{t("columnInvoiceCount")}</TableHead>
                  <TableHead>{t("columnAmount")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {batches.map((batch) => (
                  <TableRow key={batch.id}>
                    <TableCell className="tabular-nums">{batch.executionDate}</TableCell>
                    <TableCell className="tabular-nums">{new Date(batch.generatedAt).toLocaleString()}</TableCell>
                    <TableCell className="tabular-nums">{batch.invoiceCount}</TableCell>
                    <TableCell className="tabular-nums">{formatCents(batch.totalCents)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </>
      )}
    </div>
  );
}
