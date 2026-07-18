"use client";
import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { Receipt } from "lucide-react";
import { apiClient } from "../../lib/apiClient";
import { ErrorState } from "../ErrorState";
import { EmptyState } from "../EmptyState";
import type { InvoiceStatusOverviewResponse } from "../../lib/types";

type LoadState = "loading" | "loaded" | "error";

function formatCents(cents: number): string {
  return (cents / 100).toLocaleString(undefined, { style: "currency", currency: "EUR" });
}

/** FR-009/FR-010: current-month paid/outstanding/overdue invoice counts and totals, with an
 * overdue list linking to each invoice's existing detail screen. */
export function InvoiceStatusSection({ locationId }: { locationId: string }) {
  const t = useTranslations("dashboard.reporting.invoiceStatus");
  const tShared = useTranslations("dashboard.reporting");
  const router = useRouter();
  const [data, setData] = useState<InvoiceStatusOverviewResponse | null>(null);
  const [state, setState] = useState<LoadState>("loading");

  const load = useCallback(async () => {
    setState("loading");
    const result = await apiClient.GET("/api/reports/invoices", {
      params: { query: locationId ? { locationId } : {} },
    });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setData(result.data as unknown as InvoiceStatusOverviewResponse);
    setState("loaded");
  }, [locationId]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <section>
      <h2 className="mb-3 text-sm font-semibold text-text dark:text-text-dark">{t("title")}</h2>

      {state === "loading" && <div className="h-32 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && (
        <ErrorState message={tShared("loadError")} retryLabel={tShared("retry")} onRetry={load} />
      )}
      {state === "loaded" && data && (
        <div className="space-y-4">
          <div className="flex flex-wrap gap-4 text-sm">
            <span className="text-text dark:text-text-dark">
              {t("paid")}: {data.paidCount} ({formatCents(data.paidTotalCents)})
            </span>
            <span className="text-text dark:text-text-dark">
              {t("outstanding")}: {data.outstandingCount} ({formatCents(data.outstandingTotalCents)})
            </span>
            <span className="text-text dark:text-text-dark">
              {t("overdue")}: {data.overdueCount} ({formatCents(data.overdueTotalCents)})
            </span>
            <span className="font-medium text-text dark:text-text-dark">
              {t("totalInvoiced")}: {formatCents(data.totalInvoicedCents)}
            </span>
            <span className="font-medium text-text dark:text-text-dark">
              {t("totalCollected")}: {formatCents(data.paidTotalCents)}
            </span>
          </div>

          <div>
            <h3 className="mb-2 text-xs font-semibold uppercase text-text-soft dark:text-text-soft-dark">
              {t("overdueListTitle")}
            </h3>
            {data.overdueInvoices.length === 0 ? (
              <EmptyState icon={Receipt} message={t("emptyState")} />
            ) : (
              <ul className="divide-y divide-border rounded-xl border border-border dark:divide-border-dark dark:border-border-dark">
                {data.overdueInvoices.map((invoice) => (
                  <li
                    key={invoice.invoiceId}
                    role="button"
                    tabIndex={0}
                    onClick={() => router.push(`/invoices/${invoice.invoiceId}`)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        router.push(`/invoices/${invoice.invoiceId}`);
                      }
                    }}
                    className="flex h-10 cursor-pointer items-center justify-between px-4 hover:bg-surface-soft focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:hover:bg-surface-soft-dark"
                  >
                    <span className="text-sm text-text dark:text-text-dark">{invoice.childName}</span>
                    <span className="text-sm text-danger dark:text-danger-dark">
                      {t("daysOverdue", { days: invoice.daysOverdue })}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      )}
    </section>
  );
}
