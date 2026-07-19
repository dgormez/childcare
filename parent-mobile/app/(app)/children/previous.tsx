import React, { useCallback, useEffect, useState } from "react";
import { View, Text, ScrollView, RefreshControl, ActivityIndicator, TouchableOpacity } from "react-native";
import { useTranslation } from "react-i18next";
import { useRouter } from "expo-router";
import { Receipt } from "lucide-react-native";
import { apiClient } from "../../../services/apiClient";
import { useColors } from "../../../hooks/useColors";
import { ScreenContainer } from "../../../components/ScreenContainer";
import { DailySummaryCard } from "../../../components/DailySummaryCard";
import type { DailySummaryResponse, ParentPreviousChildResponse } from "../../../types";

/**
 * Feature 030 (US5) — a parent-facing "previous children" view surfacing deactivated linked
 * children and their historical daily reports/invoices read-only (spec.md FR-015/FR-016).
 * DailySummaryCard is already a read-only display (no action buttons) so it's reused as-is;
 * invoices link into the existing invoices screen, which already includes a deactivated child's
 * invoices unmodified (research.md R8 — no active/deactivated filter on that query).
 */
export default function PreviousChildrenScreen() {
  const { t } = useTranslation();
  const colors = useColors();
  const router = useRouter();

  const [children, setChildren] = useState<ParentPreviousChildResponse[]>([]);
  const [summaries, setSummaries] = useState<Record<string, DailySummaryResponse | null>>({});
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    setError("");
    try {
      const result = await apiClient.GET("/api/parent/children/previous");
      if (!result.response.ok) throw new Error("failed");
      const fetched = result.data as unknown as ParentPreviousChildResponse[];
      setChildren(fetched);

      const entries = await Promise.all(fetched.map(async (child) => {
        const summaryResult = await apiClient.GET("/api/parent/children/{childId}/daily-summary", {
          params: { path: { childId: child.id } },
        });
        const summary = summaryResult.response.ok ? (summaryResult.data as unknown as DailySummaryResponse) : null;
        return [child.id, summary] as const;
      }));
      setSummaries(Object.fromEntries(entries));
    } catch {
      setError(t("previousChildren.loadFailed"));
    }
  }, [t]);

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
      <ScrollView
        className="flex-1 bg-background dark:bg-background-dark"
        contentContainerStyle={{ padding: 16 }}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
      >
        {!!error && <Text className="text-danger dark:text-danger-dark text-sm mb-4">{error}</Text>}

        {!error && children.length === 0 && (
          <Text className="text-text-soft dark:text-text-soft-dark text-sm">{t("previousChildren.emptyState")}</Text>
        )}

        {children.map((child) => (
          <View key={child.id} className="mb-6">
            <Text className="text-text-soft dark:text-text-soft-dark text-xs mb-1">
              {t("previousChildren.readOnlyBanner")}
            </Text>
            <Text className="text-text-soft dark:text-text-soft-dark text-xs mb-2">
              {t("previousChildren.enrollmentPeriod", { start: child.enrollmentStart ?? "—", end: child.enrollmentEnd })}
            </Text>
            <DailySummaryCard child={child} summary={summaries[child.id] ?? null} />
            <TouchableOpacity
              onPress={() => router.push("/(app)/invoices")}
              className="flex-row items-center bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-4 mt-2"
              style={{ minHeight: 48 }}
            >
              <Receipt color={colors.text} size={20} strokeWidth={2} />
              <Text className="text-text dark:text-text-dark ml-2 font-medium">{t("home.quickActions.viewInvoices")}</Text>
            </TouchableOpacity>
          </View>
        ))}
      </ScrollView>
    </ScreenContainer>
  );
}
