"use client";
import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { Receipt, Landmark, CreditCard } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { Button } from "../../../components/ui/button";
import { InvoiceTable } from "../../../components/invoices/InvoiceTable";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import type { InvoiceResponse, LocationResponse } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

function currentYearMonth(): { year: number; month: number } {
  const now = new Date();
  return { year: now.getFullYear(), month: now.getMonth() + 1 };
}

type StatusFilter = "all" | "draft" | "sent" | "paid" | "overdue";

export default function InvoicesPage() {
  const t = useTranslations("invoices");
  const [locations, setLocations] = useState<LocationResponse[]>([]);
  const [locationId, setLocationId] = useState<string>("");
  const [{ year, month }, setYearMonth] = useState(currentYearMonth());
  const [status, setStatus] = useState<StatusFilter>("all");
  const [invoices, setInvoices] = useState<InvoiceResponse[]>([]);
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
    const result = await apiClient.GET("/api/locations/{locationId}/invoices", {
      params: { path: { locationId }, query: { year, month, status: status === "all" ? undefined : status } },
    });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setInvoices(result.data as unknown as InvoiceResponse[]);
    setState("loaded");
  }, [locationId, year, month, status]);

  useEffect(() => {
    load();
  }, [load]);

  async function generate() {
    setGenerating(true);
    setNotice("");
    const result = await apiClient.POST("/api/locations/{locationId}/invoices/generate", {
      params: { path: { locationId } },
      body: { year, month },
    });
    setGenerating(false);
    if (!result.response.ok) {
      setNotice(t("generateError"));
      return;
    }
    const generated = result.data as unknown as InvoiceResponse[];
    setNotice(t("generateSuccess", { count: generated.length }));
    setInvoices(generated);
  }

  const monthInputValue = `${year}-${String(month).padStart(2, "0")}`;

  return (
    <div>
      <div className="mb-6 flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
        <div className="flex items-center gap-3">
          <Link href="/invoices/sepa-batches">
            <Button variant="secondary" size="sm">
              <CreditCard className="h-4 w-4" strokeWidth={2} />
              {t("sepaBatches.title")}
            </Button>
          </Link>
          <Link href="/invoices/reconciliation">
            <Button variant="secondary" size="sm">
              <Landmark className="h-4 w-4" strokeWidth={2} />
              {t("codaReconciliation.title")}
            </Button>
          </Link>
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
            value={monthInputValue}
            onChange={(e) => {
              const [y, m] = e.target.value.split("-").map(Number);
              if (y && m) setYearMonth({ year: y, month: m });
            }}
            aria-label={t("monthLabel")}
            className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
          />
          <select
            value={status}
            onChange={(e) => setStatus(e.target.value as StatusFilter)}
            aria-label={t("statusFilterLabel")}
            className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
          >
            <option value="all">{t("statusFilterAll")}</option>
            <option value="draft">{t("statusDraft")}</option>
            <option value="sent">{t("statusSent")}</option>
            <option value="overdue">{t("statusOverdue")}</option>
            <option value="paid">{t("statusPaid")}</option>
          </select>
          <Button onClick={generate} disabled={generating || !locationId}>
            {t("generateButton")}
          </Button>
        </div>
      </div>

      {notice && <p className="mb-4 text-sm text-text-soft dark:text-text-soft-dark">{notice}</p>}

      {state === "loading" && <div className="h-64 rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}
      {state === "loaded" && invoices.length === 0 && <EmptyState icon={Receipt} message={t("emptyState")} />}
      {state === "loaded" && invoices.length > 0 && <InvoiceTable invoices={invoices} />}
    </div>
  );
}
