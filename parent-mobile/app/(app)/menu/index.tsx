import React, { useCallback, useEffect, useState } from "react";
import { View, Text, ScrollView, RefreshControl, ActivityIndicator } from "react-native";
import { useTranslation } from "react-i18next";
import { CalendarOff, UtensilsCrossed } from "lucide-react-native";
import { getMonthlyMenu } from "../../../services/menu";
import { useColors } from "../../../hooks/useColors";
import { ScreenContainer } from "../../../components/ScreenContainer";
import type { ParentMonthlyMenuEntry, MonthlyMenuDayEntry } from "../../../types";

function currentYearMonth(): { year: number; month: number } {
  const now = new Date();
  return { year: now.getFullYear(), month: now.getMonth() + 1 };
}

function daysInMonth(year: number, month: number): string[] {
  const count = new Date(year, month, 0).getDate();
  return Array.from({ length: count }, (_, i) => {
    const day = String(i + 1).padStart(2, "0");
    const monthStr = String(month).padStart(2, "0");
    return `${year}-${monthStr}-${day}`;
  });
}

/**
 * "Menu" tab (feature 013e, US2) — every location where a linked child holds an active contract,
 * each showing the current month's published menu. Closure days are greyed out and labeled (not
 * color alone, per platform-rules.md accessibility rule), still listed rather than hidden.
 */
export default function MenuScreen() {
  const { t } = useTranslation();
  const colors = useColors();
  const { year, month } = currentYearMonth();

  const [entries, setEntries] = useState<ParentMonthlyMenuEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [unavailable, setUnavailable] = useState(false);

  const load = useCallback(async () => {
    const result = await getMonthlyMenu(year, month);
    if (result.status === "unavailable") {
      setUnavailable(true);
      return;
    }
    setUnavailable(false);
    setEntries(result.entries);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [year, month]);

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
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
      >
        {unavailable && (
          <View className="items-center" style={{ paddingVertical: 48, paddingHorizontal: 24 }}>
            <UtensilsCrossed color={colors.textSoft} size={28} strokeWidth={2} />
            <Text className="text-text-soft dark:text-text-soft-dark text-sm text-center mt-3">{t("menu.loadFailed")}</Text>
          </View>
        )}

        {!unavailable && entries.length === 0 && (
          <View className="items-center" style={{ paddingVertical: 48 }}>
            <Text className="text-text-soft dark:text-text-soft-dark text-sm">{t("menu.noLocations")}</Text>
          </View>
        )}

        {!unavailable &&
          entries.map((entry) => (
            <View key={entry.locationId} style={{ paddingHorizontal: 16, paddingTop: 16 }}>
              <Text className="text-text dark:text-text-dark text-lg font-semibold mb-3">{entry.locationName}</Text>

              {!entry.isPublished ? (
                <View className="bg-surface-soft dark:bg-surface-soft-dark" style={{ borderRadius: 12, padding: 16, marginBottom: 16 }}>
                  <Text className="text-text-soft dark:text-text-soft-dark text-sm">{t("menu.notAvailable")}</Text>
                </View>
              ) : (
                <View style={{ marginBottom: 24 }}>
                  {daysInMonth(year, month).map((date) => (
                    <MenuDayRow key={date} date={date} day={entry.days.find((d) => d.date === date) ?? null} isClosure={entry.closureDates.includes(date)} />
                  ))}
                </View>
              )}
            </View>
          ))}
      </ScrollView>
    </ScreenContainer>
  );
}

function MenuDayRow({ date, day, isClosure }: { date: string; day: MonthlyMenuDayEntry | null; isClosure: boolean }) {
  const { t } = useTranslation();
  const colors = useColors();
  const label = new Date(`${date}T00:00:00`).toLocaleDateString(undefined, { weekday: "short", day: "numeric", month: "short" });

  return (
    <View
      style={{
        flexDirection: "row",
        alignItems: "center",
        paddingVertical: 8,
        borderBottomWidth: 1,
        borderBottomColor: colors.border,
        opacity: isClosure ? 0.5 : 1,
      }}
    >
      <View style={{ width: 96 }}>
        <Text className="text-text dark:text-text-dark text-sm font-medium">{label}</Text>
        {isClosure && (
          <View style={{ flexDirection: "row", alignItems: "center", marginTop: 2 }}>
            <CalendarOff color={colors.textSoft} size={12} strokeWidth={2} />
            <Text className="text-text-soft dark:text-text-soft-dark text-xs ml-1">{t("menu.closed")}</Text>
          </View>
        )}
      </View>
      <View style={{ flex: 1 }}>
        <Text className="text-text dark:text-text-dark text-sm">
          {[day?.soup, day?.mainCourse, day?.dessert].filter(Boolean).join(" · ") || "—"}
        </Text>
        {day?.notes && <Text className="text-text-soft dark:text-text-soft-dark text-xs mt-1">{day.notes}</Text>}
      </View>
    </View>
  );
}
