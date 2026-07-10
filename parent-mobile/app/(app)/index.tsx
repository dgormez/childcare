import React, { useCallback, useEffect, useState } from "react";
import { View, Text, ScrollView, RefreshControl, ActivityIndicator } from "react-native";
import { useTranslation } from "react-i18next";
import { apiClient } from "../../services/apiClient";
import { useColors } from "../../hooks/useColors";
import { ScreenContainer } from "../../components/ScreenContainer";
import { DailySummaryCard } from "../../components/DailySummaryCard";
import type { DailySummaryResponse, ParentChildResponse } from "../../types";

export default function HomeScreen() {
  const { t } = useTranslation();
  const colors = useColors();

  const [children,  setChildren]  = useState<ParentChildResponse[]>([]);
  const [summaries, setSummaries] = useState<Record<string, DailySummaryResponse | null>>({});
  const [loading,   setLoading]   = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error,     setError]     = useState("");

  const load = useCallback(async () => {
    setError("");
    try {
      const childrenResult = await apiClient.GET("/api/parent/children");
      if (!childrenResult.response.ok) throw new Error("failed");
      const fetchedChildren = childrenResult.data as unknown as ParentChildResponse[];
      setChildren(fetchedChildren);

      const entries = await Promise.all(fetchedChildren.map(async (child) => {
        const summaryResult = await apiClient.GET("/api/parent/children/{childId}/daily-summary", {
          params: { path: { childId: child.id } },
        });
        const summary = summaryResult.response.ok ? (summaryResult.data as unknown as DailySummaryResponse) : null;
        return [child.id, summary] as const;
      }));
      setSummaries(Object.fromEntries(entries));
    } catch {
      setError(t("home.loadFailed"));
    }
  }, [t]);

  useEffect(() => {
    setLoading(true);
    load().finally(() => setLoading(false));
    // Deliberately mount-only (mirrors mobile/app/(app)/index.tsx's own load effect): `load`
    // is recreated whenever `t`'s identity changes, which — unlike real react-i18next, which
    // memoizes it — a naive test mock typically does not, so depending on `load` here would
    // retrigger the fetch every render under test (found while wiring this screen's test).
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
        {!!error && (
          <Text className="text-danger dark:text-danger-dark text-sm mb-4">{error}</Text>
        )}
        {children.map((child) => (
          <DailySummaryCard key={child.id} child={child} summary={summaries[child.id] ?? null} />
        ))}
      </ScrollView>
    </ScreenContainer>
  );
}
