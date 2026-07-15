import React, { useCallback, useEffect, useState } from "react";
import { View, Text, FlatList, TouchableOpacity, RefreshControl, ActivityIndicator } from "react-native";
import { useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import { Receipt, Clock, AlertTriangle, CheckCircle2 } from "lucide-react-native";
import { getInvoices } from "../../../services/invoices";
import { useColors } from "../../../hooks/useColors";
import { ScreenContainer } from "../../../components/ScreenContainer";
import type { ParentInvoiceEntry, InvoiceStatus } from "../../../types";

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

  const [invoices, setInvoices] = useState<ParentInvoiceEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [unavailable, setUnavailable] = useState(false);

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
            keyExtractor={(item) => item.id}
            contentContainerStyle={{ padding: 16 }}
            refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
            renderItem={({ item }) => {
              const meta = STATUS_META[statusKey(item.status, item.isOverdue)];
              const StatusIcon = meta.icon;
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
