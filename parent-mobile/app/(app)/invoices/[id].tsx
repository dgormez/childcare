import React, { useEffect, useState } from "react";
import { View, Text, ScrollView, ActivityIndicator, TouchableOpacity } from "react-native";
import { useLocalSearchParams } from "expo-router";
import { useTranslation } from "react-i18next";
import * as Sharing from "expo-sharing";
import { Clock, AlertTriangle, CheckCircle2, Download, Receipt as ReceiptIcon } from "lucide-react-native";
import { getInvoices, downloadInvoicePdf } from "../../../services/invoices";
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

export default function InvoiceDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { t } = useTranslation();
  const colors = useColors();

  const [invoice, setInvoice] = useState<ParentInvoiceEntry | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [downloading, setDownloading] = useState(false);
  const [downloadError, setDownloadError] = useState("");

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
