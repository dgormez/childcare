"use client";
import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { ArrowLeft } from "lucide-react";
import { apiClient } from "../../../../lib/apiClient";
import { InvoiceDetail } from "../../../../components/invoices/InvoiceDetail";
import { ErrorState } from "../../../../components/ErrorState";
import type { InvoiceResponse } from "../../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

export default function InvoiceDetailPage() {
  const t = useTranslations("invoices");
  const router = useRouter();
  const params = useParams<{ id: string }>();
  const [invoice, setInvoice] = useState<InvoiceResponse | null>(null);
  const [state, setState] = useState<LoadState>("loading");

  const load = useCallback(async () => {
    setState("loading");
    const result = await apiClient.GET("/api/invoices/{id}", { params: { path: { id: params.id } } });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setInvoice(result.data as unknown as InvoiceResponse);
    setState("loaded");
  }, [params.id]);

  useEffect(() => {
    load();
  }, [load]);

  if (state === "loading") return <div className="h-64 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />;
  if (state === "error" || !invoice) return <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />;

  return (
    <div>
      <button
        onClick={() => router.push("/invoices")}
        className="mb-4 flex items-center gap-2 text-sm text-text-soft hover:text-text dark:text-text-soft-dark dark:hover:text-text-dark"
      >
        <ArrowLeft className="h-4 w-4" strokeWidth={2} />
        {t("backToList")}
      </button>

      <h1 className="mb-6 text-2xl font-semibold text-text dark:text-text-dark">{invoice.childName}</h1>

      <InvoiceDetail invoice={invoice} onUpdated={setInvoice} />
    </div>
  );
}
