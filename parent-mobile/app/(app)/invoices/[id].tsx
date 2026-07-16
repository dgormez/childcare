import React, { useEffect, useRef, useState } from "react";
import { View, Text, ScrollView, ActivityIndicator, TouchableOpacity } from "react-native";
import { useLocalSearchParams } from "expo-router";
import { useTranslation } from "react-i18next";
import * as Sharing from "expo-sharing";
import * as WebBrowser from "expo-web-browser";
import { Clock, AlertTriangle, CheckCircle2, Download, Receipt as ReceiptIcon, CreditCard } from "lucide-react-native";
import { getInvoices, downloadInvoicePdf } from "../../../services/invoices";
import { createPaymentLink, getPaymentStatus, downloadBetalingsbewijs } from "../../../services/payments";
import { useColors } from "../../../hooks/useColors";
import type { InvoiceStatus, ParentInvoiceEntry } from "../../../types";

function formatCents(cents: number): string {
  return (cents / 100).toLocaleString(undefined, { style: "currency", currency: "EUR" });
}

function formatDate(value: string | null): string {
  if (!value) return "—";
  return new Date(value).toLocaleDateString(undefined, { day: "numeric", month: "long", year: "numeric" });
}

const STATUS_META: Record<
  "sent" | "overdue" | "paid",
  { icon: typeof Clock; colorKey: "warning" | "danger" | "success"; labelKey: string }
> = {
  sent: { icon: Clock, colorKey: "warning", labelKey: "invoices.statusSent" },
  overdue: { icon: AlertTriangle, colorKey: "danger", labelKey: "invoices.statusOverdue" },
  paid: { icon: CheckCircle2, colorKey: "success", labelKey: "invoices.statusPaid" },
};

function statusKey(status: InvoiceStatus, isOverdue: boolean): "sent" | "overdue" | "paid" {
  if (status === "paid") return "paid";
  return isOverdue ? "overdue" : "sent";
}

// Feature 014a FR-010 — how long to keep polling payment-status after the parent returns from
// Mollie's hosted checkout before giving up and letting them retry manually. Not push-driven:
// this app has no live-update channel, so a bounded poll is the simplest honest approximation
// of "confirming payment" (spec.md UX Requirements).
const CONFIRM_POLL_INTERVAL_MS = 2000;
const CONFIRM_POLL_MAX_ATTEMPTS = 10;

type PaymentAction = "idle" | "creating" | "confirming" | "not_connected" | "error";

export default function InvoiceDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { t } = useTranslation();
  const colors = useColors();

  const [invoice, setInvoice] = useState<ParentInvoiceEntry | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [downloading, setDownloading] = useState(false);
  const [downloadError, setDownloadError] = useState("");

  const [paymentAction, setPaymentAction] = useState<PaymentAction>("idle");
  const [downloadingReceipt, setDownloadingReceipt] = useState(false);
  const [receiptError, setReceiptError] = useState("");
  const pollCancelled = useRef(false);

  useEffect(() => {
    // No per-id parent endpoint exists (only GET /api/parent/invoices and .../pdf, contracts/
    // invoicing-api.md) — reuses the list service's cache/fetch and finds the matching entry.
    async function load() {
      setError("");
      const result = await getInvoices();
      if (result.status === "unavailable") {
        setError(t("invoices.loadFailed"));
        setLoading(false);
        return;
      }
      const found = result.invoices.find((i) => i.id === id) ?? null;
      if (!found) setError(t("invoices.loadFailed"));
      setInvoice(found);
      setLoading(false);
    }
    load();
    return () => {
      pollCancelled.current = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function handleDownload() {
    setDownloading(true);
    setDownloadError("");
    try {
      const file = await downloadInvoicePdf(id);
      if (await Sharing.isAvailableAsync()) {
        await Sharing.shareAsync(file.uri, { UTI: "com.adobe.pdf", mimeType: "application/pdf" });
      }
    } catch {
      setDownloadError(t("invoices.downloadFailed"));
    } finally {
      setDownloading(false);
    }
  }

  async function handlePayNow() {
    setPaymentAction("creating");
    const result = await createPaymentLink(id);
    if (pollCancelled.current) return;
    if (result.status === "not_connected") {
      setPaymentAction("not_connected");
      return;
    }
    if (result.status === "error") {
      setPaymentAction("error");
      return;
    }

    await WebBrowser.openBrowserAsync(result.checkoutUrl);
    if (pollCancelled.current) return;
    setPaymentAction("confirming");

    for (let attempt = 0; attempt < CONFIRM_POLL_MAX_ATTEMPTS; attempt++) {
      if (pollCancelled.current) return;
      await new Promise((resolve) => setTimeout(resolve, CONFIRM_POLL_INTERVAL_MS));
      if (pollCancelled.current) return;

      const status = await getPaymentStatus(id);
      if (pollCancelled.current) return;
      if (status?.invoiceStatus === "paid") {
        setInvoice((prev) => (prev ? { ...prev, status: "paid", paidAt: new Date().toISOString() } : prev));
        setPaymentAction("idle");
        return;
      }
    }

    // Still not confirmed after the poll window — back to idle, "Pay now" stays available so
    // the parent can check again later (the webhook may simply be slow, not failed).
    if (pollCancelled.current) return;
    setPaymentAction("idle");
  }

  async function handleDownloadReceipt() {
    setDownloadingReceipt(true);
    setReceiptError("");
    try {
      const file = await downloadBetalingsbewijs(id);
      if (await Sharing.isAvailableAsync()) {
        await Sharing.shareAsync(file.uri, { UTI: "com.adobe.pdf", mimeType: "application/pdf" });
      }
    } catch {
      setReceiptError(t("invoices.receiptDownloadFailed"));
    } finally {
      setDownloadingReceipt(false);
    }
  }

  if (loading) {
    return (
      <View style={{ flex: 1, backgroundColor: colors.background, alignItems: "center", justifyContent: "center" }}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  if (error || !invoice) {
    return (
      <View className="flex-1 bg-background dark:bg-background-dark items-center justify-center px-6">
        <Text className="text-danger dark:text-danger-dark text-sm text-center">{error}</Text>
      </View>
    );
  }

  const meta = STATUS_META[statusKey(invoice.status, invoice.isOverdue)];
  const StatusIcon = meta.icon;

  return (
    <ScrollView className="flex-1 bg-background dark:bg-background-dark" contentContainerStyle={{ padding: 16 }}>
      <View className="flex-row items-center mb-1">
        <ReceiptIcon color={colors.text} size={24} strokeWidth={2} />
        <Text className="text-text dark:text-text-dark text-xl font-bold ml-2 flex-1">{invoice.childName}</Text>
      </View>
      <View className="flex-row items-center mb-6">
        <StatusIcon color={colors[meta.colorKey]} size={14} strokeWidth={2} />
        <Text className="text-text-soft dark:text-text-soft-dark text-xs ml-1">{t(meta.labelKey)}</Text>
      </View>

      <View className="bg-surface dark:bg-surface-dark rounded-xl p-4 mb-4">
        <View className="flex-row justify-between mb-2">
          <Text className="text-text-soft dark:text-text-soft-dark text-sm">{t("invoices.amount")}</Text>
          <Text className="text-text dark:text-text-dark text-lg font-semibold">{formatCents(invoice.totalCents)}</Text>
        </View>
        {invoice.dueDate && (
          <Text className="text-text-soft dark:text-text-soft-dark text-xs">
            {t("invoices.dueDate", { date: formatDate(invoice.dueDate) })}
          </Text>
        )}
        {invoice.paidAt && (
          <Text className="text-text-soft dark:text-text-soft-dark text-xs mt-1">
            {t("invoices.paidOn", { date: formatDate(invoice.paidAt) })}
          </Text>
        )}
      </View>

      {!!invoice.ogmReference && (
        <View className="bg-surface dark:bg-surface-dark rounded-xl p-4 mb-4">
          <Text className="text-text-soft dark:text-text-soft-dark text-xs mb-1">{t("invoices.ogmReference")}</Text>
          <Text className="text-text dark:text-text-dark text-base font-mono">{invoice.ogmReference}</Text>
        </View>
      )}

      {invoice.status === "sent" && (
        <>
          {paymentAction === "confirming" && (
            <View className="flex-row items-center justify-center mb-4">
              <ActivityIndicator size="small" color={colors.primary} />
              <Text className="text-text-soft dark:text-text-soft-dark text-sm ml-2">{t("invoices.confirmingPayment")}</Text>
            </View>
          )}
          {paymentAction === "not_connected" && (
            <Text className="text-text-soft dark:text-text-soft-dark text-sm mb-4">{t("invoices.payNotAvailable")}</Text>
          )}
          {paymentAction === "error" && (
            <Text className="text-danger dark:text-danger-dark text-sm mb-4">{t("invoices.payLinkFailed")}</Text>
          )}
          {paymentAction !== "not_connected" && (
            <TouchableOpacity
              onPress={handlePayNow}
              disabled={paymentAction === "creating" || paymentAction === "confirming"}
              className="flex-row items-center justify-center bg-primary dark:bg-primary-dark rounded-lg mb-3"
              style={{ minHeight: 48, paddingHorizontal: 16, opacity: paymentAction === "creating" || paymentAction === "confirming" ? 0.6 : 1 }}
            >
              {paymentAction === "creating" ? (
                <ActivityIndicator size="small" color="#fff" />
              ) : (
                <>
                  <CreditCard color="#fff" size={20} strokeWidth={2} />
                  <Text className="text-white font-semibold ml-2">{t("invoices.payNow")}</Text>
                </>
              )}
            </TouchableOpacity>
          )}
        </>
      )}

      {invoice.status === "paid" && (
        <>
          {!!receiptError && <Text className="text-danger dark:text-danger-dark text-sm mb-4">{receiptError}</Text>}
          <TouchableOpacity
            onPress={handleDownloadReceipt}
            disabled={downloadingReceipt}
            className="flex-row items-center justify-center border border-border dark:border-border-dark rounded-lg mb-3"
            style={{ minHeight: 48, paddingHorizontal: 16, opacity: downloadingReceipt ? 0.6 : 1 }}
          >
            {downloadingReceipt ? (
              <ActivityIndicator size="small" color={colors.text} />
            ) : (
              <>
                <ReceiptIcon color={colors.text} size={20} strokeWidth={2} />
                <Text className="text-text dark:text-text-dark font-semibold ml-2">{t("invoices.viewReceipt")}</Text>
              </>
            )}
          </TouchableOpacity>
        </>
      )}

      {!!downloadError && <Text className="text-danger dark:text-danger-dark text-sm mb-4">{downloadError}</Text>}

      <TouchableOpacity
        onPress={handleDownload}
        disabled={downloading}
        className="flex-row items-center justify-center bg-primary dark:bg-primary-dark rounded-lg"
        style={{ minHeight: 48, paddingHorizontal: 16, opacity: downloading ? 0.6 : 1 }}
      >
        {downloading ? (
          <ActivityIndicator size="small" color="#fff" />
        ) : (
          <>
            <Download color="#fff" size={20} strokeWidth={2} />
            <Text className="text-white font-semibold ml-2">{t("invoices.downloadPdf")}</Text>
          </>
        )}
      </TouchableOpacity>
    </ScrollView>
  );
}
