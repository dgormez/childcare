"use client";
import { useState } from "react";
import { useTranslations } from "next-intl";
import { Send, CheckCircle2, RefreshCw, Download, Plus, X } from "lucide-react";
import { Button } from "../ui/button";
import { Input } from "../ui/input";
import { apiClient, getAccessToken } from "../../lib/apiClient";
import type { InvoiceExtraChargeResponse, InvoiceResponse } from "../../lib/types";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

interface InvoiceDetailProps {
  invoice: InvoiceResponse;
  onUpdated: (updated: InvoiceResponse) => void;
}

function formatCents(cents: number): string {
  return (cents / 100).toLocaleString(undefined, { style: "currency", currency: "EUR" });
}

export function InvoiceDetail({ invoice, onUpdated }: InvoiceDetailProps) {
  const t = useTranslations("invoices");
  const [extraCharges, setExtraCharges] = useState<InvoiceExtraChargeResponse[]>(invoice.lineItems.extraCharges);
  const [newLabel, setNewLabel] = useState("");
  const [newAmount, setNewAmount] = useState("");
  const [paidAt, setPaidAt] = useState(new Date().toISOString().slice(0, 10));
  const [busy, setBusy] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const [notice, setNotice] = useState("");

  function addCharge() {
    const amountCents = Math.round(Number(newAmount) * 100);
    if (!newLabel || !amountCents || amountCents <= 0) return;
    setExtraCharges((current) => [...current, { label: newLabel, amountCents }]);
    setNewLabel("");
    setNewAmount("");
  }

  function removeCharge(index: number) {
    setExtraCharges((current) => current.filter((_, i) => i !== index));
  }

  async function saveExtraCharges() {
    setBusy(true);
    setNotice("");
    const result = await apiClient.PUT("/api/invoices/{id}/extra-charges", {
      params: { path: { id: invoice.id } },
      body: { extraCharges },
    });
    setBusy(false);
    if (!result.response.ok) {
      setNotice(t("saveExtraChargesError"));
      return;
    }
    setNotice(t("saveExtraChargesSuccess"));
    onUpdated(result.data as unknown as InvoiceResponse);
  }

  async function send() {
    setBusy(true);
    setNotice("");
    const result = await apiClient.POST("/api/invoices/send", { body: { invoiceIds: [invoice.id] } });
    setBusy(false);
    if (!result.response.ok) {
      setNotice(t("sendError"));
      return;
    }
    setNotice(t("sendSuccess"));
    onUpdated((result.data as unknown as InvoiceResponse[])[0]);
  }

  async function markPaid() {
    setBusy(true);
    setNotice("");
    const result = await apiClient.POST("/api/invoices/{id}/mark-paid", {
      params: { path: { id: invoice.id } },
      body: { paidAt },
    });
    setBusy(false);
    if (!result.response.ok) {
      setNotice(t("markPaidError"));
      return;
    }
    setNotice(t("markPaidSuccess"));
    onUpdated(result.data as unknown as InvoiceResponse);
  }

  async function regenerate() {
    setBusy(true);
    setNotice("");
    const result = await apiClient.POST("/api/invoices/{id}/regenerate", { params: { path: { id: invoice.id } } });
    setBusy(false);
    if (!result.response.ok) {
      setNotice(t("regenerateError"));
      return;
    }
    setNotice(t("regenerateSuccess"));
    onUpdated(result.data as unknown as InvoiceResponse);
  }

  async function downloadPdf() {
    setDownloading(true);
    setNotice("");
    try {
      const response = await fetch(`${API_BASE}/api/invoices/${invoice.id}/pdf`, {
        headers: { Authorization: `Bearer ${getAccessToken()}` },
      });
      if (!response.ok) throw new Error("download failed");
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `invoice-${invoice.id}.pdf`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch {
      setNotice(t("downloadPdfError"));
    } finally {
      setDownloading(false);
    }
  }

  const chargesChanged = JSON.stringify(extraCharges) !== JSON.stringify(invoice.lineItems.extraCharges);

  return (
    <div className="max-w-2xl space-y-6">
      <div className="space-y-2 rounded-lg bg-surface-soft p-4 dark:bg-surface-soft-dark">
        <h2 className="text-sm font-semibold text-text dark:text-text-dark">{t("breakdownTitle")}</h2>
        <div className="flex justify-between text-sm">
          <span className="text-text-soft dark:text-text-soft-dark">{t("presentDays")}</span>
          <span className="tabular-nums text-text dark:text-text-dark">{invoice.lineItems.presentDays}</span>
        </div>
        <div className="flex justify-between text-sm">
          <span className="text-text-soft dark:text-text-soft-dark">{t("unjustifiedAbsentDays")}</span>
          <span className="tabular-nums text-text dark:text-text-dark">{invoice.lineItems.unjustifiedAbsentDays}</span>
        </div>
        <div className="flex justify-between text-sm">
          <span className="text-text-soft dark:text-text-soft-dark">{t("closureDaysExcluded")}</span>
          <span className="tabular-nums text-text dark:text-text-dark">{invoice.lineItems.closureDaysExcluded}</span>
        </div>
        <div className="flex justify-between text-sm">
          <span className="text-text-soft dark:text-text-soft-dark">{t("dailyRate")}</span>
          <span className="tabular-nums text-text dark:text-text-dark">{formatCents(invoice.lineItems.dailyRateCents)}</span>
        </div>
        <div className="flex justify-between border-t border-border pt-2 text-sm dark:border-border-dark">
          <span className="text-text-soft dark:text-text-soft-dark">{t("subtotal")}</span>
          <span className="tabular-nums text-text dark:text-text-dark">{formatCents(invoice.subtotalCents)}</span>
        </div>
      </div>

      <div className="space-y-3">
        <h2 className="text-sm font-semibold text-text dark:text-text-dark">{t("extraCharges")}</h2>
        {extraCharges.map((charge, index) => (
          <div key={index} className="flex items-center justify-between gap-3 rounded-lg bg-surface-soft px-3 py-2 dark:bg-surface-soft-dark">
            <span className="text-sm text-text dark:text-text-dark">{charge.label}</span>
            <div className="flex items-center gap-3">
              <span className="tabular-nums text-sm text-text dark:text-text-dark">{formatCents(charge.amountCents)}</span>
              {invoice.status === "draft" && (
                <Button variant="ghost" size="sm" aria-label={t("removeExtraCharge")} onClick={() => removeCharge(index)}>
                  <X className="h-3 w-3" strokeWidth={2} />
                </Button>
              )}
            </div>
          </div>
        ))}
        {invoice.status === "draft" && (
          <div className="flex items-end gap-2">
            <div className="flex-1 space-y-1">
              <label htmlFor="extraLabel" className="text-xs text-text-soft dark:text-text-soft-dark">{t("extraChargeLabel")}</label>
              <Input id="extraLabel" value={newLabel} onChange={(e) => setNewLabel(e.target.value)} />
            </div>
            <div className="w-28 space-y-1">
              <label htmlFor="extraAmount" className="text-xs text-text-soft dark:text-text-soft-dark">{t("extraChargeAmount")}</label>
              <Input id="extraAmount" type="number" min={0} step="0.01" value={newAmount} onChange={(e) => setNewAmount(e.target.value)} className="tabular-nums" />
            </div>
            <Button variant="secondary" size="sm" onClick={addCharge} aria-label={t("addExtraCharge")}>
              <Plus className="h-4 w-4" strokeWidth={2} />
            </Button>
          </div>
        )}
        {invoice.status === "draft" && chargesChanged && (
          <Button variant="secondary" size="sm" onClick={saveExtraCharges} disabled={busy}>
            {t("saveExtraCharges")}
          </Button>
        )}
      </div>

      <div className="flex items-center justify-between border-t border-border pt-4 dark:border-border-dark">
        <span className="text-base font-semibold text-text dark:text-text-dark">{t("total")}</span>
        <span className="text-lg font-semibold tabular-nums text-text dark:text-text-dark">{formatCents(invoice.totalCents)}</span>
      </div>

      {invoice.status !== "draft" && (
        <div className="space-y-1 text-sm">
          <div className="flex justify-between">
            <span className="text-text-soft dark:text-text-soft-dark">{t("dueDate")}</span>
            <span className="text-text dark:text-text-dark">{invoice.dueDate}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-text-soft dark:text-text-soft-dark">{t("ogmReference")}</span>
            <span className="font-mono text-text dark:text-text-dark">{invoice.ogmReference}</span>
          </div>
        </div>
      )}

      {notice && <p className="text-sm text-text-soft dark:text-text-soft-dark">{notice}</p>}

      <div className="flex flex-wrap items-center gap-3">
        {invoice.status === "draft" && (
          <Button onClick={send} disabled={busy}>
            <Send className="h-4 w-4" strokeWidth={2} />
            {t("sendButton")}
          </Button>
        )}
        {invoice.status !== "paid" && (
          <Button variant="secondary" onClick={regenerate} disabled={busy}>
            <RefreshCw className="h-4 w-4" strokeWidth={2} />
            {t("regenerateButton")}
          </Button>
        )}
        {invoice.status === "sent" && (
          <div className="flex items-center gap-2">
            <Input type="date" value={paidAt} onChange={(e) => setPaidAt(e.target.value)} aria-label={t("markPaidDateLabel")} className="h-10" />
            <Button variant="secondary" onClick={markPaid} disabled={busy}>
              <CheckCircle2 className="h-4 w-4" strokeWidth={2} />
              {t("markPaidButton")}
            </Button>
          </div>
        )}
        {invoice.status !== "draft" && (
          <Button variant="secondary" onClick={downloadPdf} disabled={downloading}>
            <Download className="h-4 w-4" strokeWidth={2} />
            {t("downloadPdf")}
          </Button>
        )}
      </div>
    </div>
  );
}
