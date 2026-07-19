import React, { useCallback, useEffect, useState } from "react";
import { View, Text, FlatList, TouchableOpacity, RefreshControl, ActivityIndicator } from "react-native";
import { useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import * as Sharing from "expo-sharing";
import { Receipt, Clock, AlertTriangle, CheckCircle2, Download } from "lucide-react-native";
import { getInvoices, downloadFamilyInvoicePdf } from "../../../services/invoices";
import { useColors } from "../../../hooks/useColors";
import { ScreenContainer } from "../../../components/ScreenContainer";
import { isFamilyInvoiceEntry } from "../../../types";
import type { ParentInvoiceListEntry, InvoiceStatus } from "../../../types";

function formatCents(cents: number): string {
  return (cents / 100).toLocaleString(undefined, { style: "currency", currency: "EUR" });
}

function periodParts(periodMonth: string): { month: string; year: number } {
  const [year, month] = periodMonth.split("-").map(Number);
  return { month: new Date(year, month - 1, 1).toLocaleDateString(undefined, { month: "long" }), year };
}

// FR-008: plain-language statuses only, each paired with an icon — never color alone
// (design-system.md's Status indicators rule). `draft` never appears here (backend never
// returns it to a parent).
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

export default function InvoicesScreen() {
  const { t } = useTranslation();
  const colors = useColors();
  const router = useRouter();

  const [invoices, setInvoices] = useState<ParentInvoiceListEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [unavailable, setUnavailable] = useState(false);
  const [downloadingFamilyId, setDownloadingFamilyId] = useState<string | null>(null);

  const load = useCallback(async () => {
    const result = await getInvoices();
    if (result.status === "unavailable") {
      setUnavailable(true);
    } else {
      setUnavailable(false);
      setInvoices(result.invoices);
    }
  }, []);

  useEffect(() => {
    setLoading(true);
    load().finally(() => setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const onRefresh = async () => {
    setRefreshing(true);
    await load();
    setRefreshing(false);
  };

  async function handleDownloadFamily(familyGroupId: string) {
    setDownloadingFamilyId(familyGroupId);
    try {
      const file = await downloadFamilyInvoicePdf(familyGroupId);
      if (await Sharing.isAvailableAsync()) {
        await Sharing.shareAsync(file.uri, { UTI: "com.adobe.pdf", mimeType: "application/pdf" });
      }
    } catch {
      // Best-effort — the download button is retryable, mirrors the per-invoice detail screen's
      // own silent-retry shape rather than adding a second error surface to this list screen.
    } finally {
      setDownloadingFamilyId(null);
    }
  }

  if (loading) {
    return (
      <View style={{ flex: 1, backgroundColor: colors.background, alignItems: "center", justifyContent: "center" }}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  return (
    <ScreenContainer>
      <View className="flex-1 bg-background dark:bg-background-dark">
        {unavailable && (
          <View className="items-center" style={{ paddingVertical: 48, paddingHorizontal: 24 }}>
            <Receipt color={colors.textSoft} size={28} strokeWidth={2} />
            <Text className="text-text-soft dark:text-text-soft-dark text-sm text-center mt-3">{t("invoices.loadFailed")}</Text>
          </View>
        )}

        {!unavailable && invoices.length === 0 && (
          <View className="items-center" style={{ paddingVertical: 48 }}>
            <Receipt color={colors.textSoft} size={28} strokeWidth={2} />
            <Text className="text-text-soft dark:text-text-soft-dark text-sm mt-3">{t("invoices.noInvoices")}</Text>
          </View>
        )}

        {!unavailable && (
          <FlatList
            data={invoices}
            keyExtractor={(item) => (isFamilyInvoiceEntry(item) ? `family-${item.familyGroupId}` : item.id)}
            contentContainerStyle={{ padding: 16 }}
            refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
            renderItem={({ item }) => {
              const meta = STATUS_META[statusKey(item.status, item.isOverdue)];
              const StatusIcon = meta.icon;

              // Feature 030 (US3) — siblings sharing a FamilyGroupId render as one combined
              // tile (per-child lines, combined total, single download action) instead of one
              // tile each; never navigable to the per-invoice detail screen (no single id).
              if (isFamilyInvoiceEntry(item)) {
                return (
                  <View
                    className="bg-surface dark:bg-surface-dark rounded-xl px-4 mb-3 py-3"
                    style={{ minHeight: 64 }}
                  >
                    {item.children.map((child) => (
                      <Text key={child.childId} className="text-text dark:text-text-dark text-sm" numberOfLines={1}>
                        {t("invoices.familyGroup.perChildLine", { childName: child.childName, amount: formatCents(child.subtotalCents) })}
                      </Text>
                    ))}
                    <View className="flex-row items-center justify-between mt-2">
                      <View>
                        <Text className="text-text-soft dark:text-text-soft-dark text-xs">{t("invoices.familyGroup.combinedTotal")}</Text>
                        <Text className="text-text dark:text-text-dark text-base font-semibold">{formatCents(item.totalCents)}</Text>
                        <View className="flex-row items-center mt-1">
                          <StatusIcon color={colors[meta.colorKey]} size={14} strokeWidth={2} />
                          <Text className="text-text-soft dark:text-text-soft-dark text-xs ml-1">{t(meta.labelKey)}</Text>
                        </View>
                      </View>
                      <TouchableOpacity
                        onPress={() => handleDownloadFamily(item.familyGroupId)}
                        disabled={downloadingFamilyId === item.familyGroupId}
                        accessibilityLabel={t("invoices.downloadPdf")}
                        style={{ minHeight: 48, minWidth: 48, alignItems: "center", justifyContent: "center" }}
                      >
                        {downloadingFamilyId === item.familyGroupId ? (
                          <ActivityIndicator color={colors.primary} />
                        ) : (
                          <Download color={colors.primary} size={20} strokeWidth={2} />
                        )}
                      </TouchableOpacity>
                    </View>
                  </View>
                );
              }

              return (
                <TouchableOpacity
                  onPress={() => router.push(`/(app)/invoices/${item.id}`)}
                  className="bg-surface dark:bg-surface-dark rounded-xl px-4 mb-3"
                  style={{ minHeight: 64, justifyContent: "center", paddingVertical: 12 }}
                >
                  <View className="flex-row items-center justify-between">
                    <View className="flex-1">
                      <Text className="text-text dark:text-text-dark text-base font-medium" numberOfLines={1}>
                        {item.childName}
                      </Text>
                      <Text className="text-text-soft dark:text-text-soft-dark text-xs mt-1">
                        {t("invoices.period", periodParts(item.periodMonth))}
                      </Text>
                    </View>
                    <View className="items-end">
                      <Text className="text-text dark:text-text-dark text-base font-semibold">
                        {formatCents(item.totalCents)}
                      </Text>
                      <View className="flex-row items-center mt-1">
                        <StatusIcon color={colors[meta.colorKey]} size={14} strokeWidth={2} />
                        <Text className="text-text-soft dark:text-text-soft-dark text-xs ml-1">{t(meta.labelKey)}</Text>
                      </View>
                    </View>
                  </View>
                </TouchableOpacity>
              );
            }}
          />
        )}
      </View>
    </ScreenContainer>
  );
}
