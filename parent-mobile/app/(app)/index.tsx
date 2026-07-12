import React, { useCallback, useEffect, useState } from "react";
import { View, Text, ScrollView, RefreshControl, ActivityIndicator, TouchableOpacity } from "react-native";
import { useTranslation } from "react-i18next";
import { useRouter } from "expo-router";
import { CalendarOff, CalendarPlus, ArrowLeftRight, ListChecks } from "lucide-react-native";
import { apiClient } from "../../services/apiClient";
import { getReservationAvailability } from "../../services/locations";
import { useColors } from "../../hooks/useColors";
import { ScreenContainer } from "../../components/ScreenContainer";
import { DailySummaryCard } from "../../components/DailySummaryCard";
import type { DailySummaryResponse, ParentChildResponse, ReservationAvailabilityResponse } from "../../types";

export default function HomeScreen() {
  const { t } = useTranslation();
  const colors = useColors();
  const router = useRouter();

  const [children,  setChildren]  = useState<ParentChildResponse[]>([]);
  const [summaries, setSummaries] = useState<Record<string, DailySummaryResponse | null>>({});
  const [loading,   setLoading]   = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error,     setError]     = useState("");
  // research.md R6: hide a quick action only when the type is disabled for every linked child —
  // nothing to do there is genuinely nothing. Defaults to visible while availability loads, so
  // a slow/failed availability fetch never hides a quick action that might actually work.
  const [anyChildAllows, setAnyChildAllows] = useState({ absence: true, extra: true, exchange: true });

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

      const availabilities = (await Promise.all(
        fetchedChildren.map((child) => getReservationAvailability(child.id)),
      )).filter((a): a is ReservationAvailabilityResponse => a !== null);
      if (availabilities.length > 0) {
        setAnyChildAllows({
          absence: availabilities.some((a) => a.absence !== "disabled"),
          extra: availabilities.some((a) => a.extra !== "disabled"),
          exchange: availabilities.some((a) => a.exchange !== "disabled"),
        });
      }
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

        <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium mt-2 mb-2">
          {t("home.quickActions.title")}
        </Text>
        <View className="flex-row flex-wrap" style={{ gap: 8 }}>
          {anyChildAllows.absence && (
            <TouchableOpacity
              onPress={() => router.push("/(app)/requests/absence")}
              className="flex-row items-center bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-4"
              style={{ minHeight: 48 }}
            >
              <CalendarOff color={colors.text} size={20} strokeWidth={2} />
              <Text className="text-text dark:text-text-dark ml-2 font-medium">{t("home.quickActions.reportSick")}</Text>
            </TouchableOpacity>
          )}
          {anyChildAllows.extra && (
            <TouchableOpacity
              onPress={() => router.push("/(app)/requests/extra")}
              className="flex-row items-center bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-4"
              style={{ minHeight: 48 }}
            >
              <CalendarPlus color={colors.text} size={20} strokeWidth={2} />
              <Text className="text-text dark:text-text-dark ml-2 font-medium">{t("home.quickActions.requestExtra")}</Text>
            </TouchableOpacity>
          )}
          {anyChildAllows.exchange && (
            <TouchableOpacity
              onPress={() => router.push("/(app)/requests/exchange")}
              className="flex-row items-center bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-4"
              style={{ minHeight: 48 }}
            >
              <ArrowLeftRight color={colors.text} size={20} strokeWidth={2} />
              <Text className="text-text dark:text-text-dark ml-2 font-medium">{t("home.quickActions.requestExchange")}</Text>
            </TouchableOpacity>
          )}
          <TouchableOpacity
            onPress={() => router.push("/(app)/requests")}
            className="flex-row items-center bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-4"
            style={{ minHeight: 48 }}
          >
            <ListChecks color={colors.text} size={20} strokeWidth={2} />
            <Text className="text-text dark:text-text-dark ml-2 font-medium">{t("home.quickActions.myRequests")}</Text>
          </TouchableOpacity>
        </View>
      </ScrollView>
    </ScreenContainer>
  );
}
