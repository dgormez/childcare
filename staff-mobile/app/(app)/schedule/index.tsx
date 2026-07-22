import React, { useCallback, useEffect, useMemo, useState } from "react";
import { View, Text, ScrollView, TouchableOpacity, RefreshControl, ActivityIndicator } from "react-native";
import { useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import { CalendarX, WifiOff, ThermometerSun } from "lucide-react-native";
import dayjs from "dayjs";
import { apiClient } from "../../../services/apiClient";
import { getMySchedule } from "../../../services/schedule";
import { ScreenContainer } from "../../../components/ScreenContainer";
import { ScheduleWeekList } from "../../../components/ScheduleWeekList";
import { ScheduleDayCard } from "../../../components/ScheduleDayCard";
import { useColors } from "../../../hooks/useColors";
import type { StaffScheduleResponse } from "../../../types";

type LoadStatus = "loading" | "loaded" | "unavailable";
type ViewMode = "week" | "day";

function toDateString(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function mondayOf(date: Date): string {
  const day = date.getDay();
  const diff = day === 0 ? -6 : 1 - day;
  const monday = new Date(date);
  monday.setDate(date.getDate() + diff);
  return toDateString(monday);
}

// FR-003: "the next 4 weeks" — 28 days starting from this week's Monday.
function fourWeeksFrom(weekStart: string): string[][] {
  const start = new Date(`${weekStart}T00:00:00`);
  const weeks: string[][] = [];
  for (let w = 0; w < 4; w++) {
    const week: string[] = [];
    for (let d = 0; d < 7; d++) {
      const date = new Date(start);
      date.setDate(start.getDate() + w * 7 + d);
      week.push(toDateString(date));
    }
    weeks.push(week);
  }
  return weeks;
}

/**
 * US2 (FR-003/FR-004): the app's headline screen — "where am I working next Wednesday?" in
 * under 5 seconds of opening the app (spec.md Success Criteria SC-001). Week view is the
 * default, with a day-view toggle; the "Ik ben ziek" quick action lives here since it's the
 * screen a staff member opens first (spec.md Main flow).
 */
export default function ScheduleScreen() {
  const { t } = useTranslation();
  const colors = useColors();
  const router = useRouter();

  const [status, setStatus] = useState<LoadStatus>("loading");
  const [fromCache, setFromCache] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [entries, setEntries] = useState<StaffScheduleResponse[]>([]);
  const [contractedDays, setContractedDays] = useState<string[]>([]);
  const [locationNamesById, setLocationNamesById] = useState<Map<string, string>>(new Map());
  const [groupNamesById, setGroupNamesById] = useState<Map<string, string>>(new Map());
  const [closedLocationIdsByDate, setClosedLocationIdsByDate] = useState<Map<string, Set<string>>>(new Map());
  const [viewMode, setViewMode] = useState<ViewMode>("week");
  const todayStr = useMemo(() => toDateString(new Date()), []);
  const [selectedDate, setSelectedDate] = useState(todayStr);

  const weeks = useMemo(() => fourWeeksFrom(mondayOf(new Date())), []);
  const allDates = useMemo(() => weeks.flat(), [weeks]);

  const load = useCallback(async () => {
    const scheduleResult = await getMySchedule();
    if (scheduleResult.status === "unavailable") {
      setStatus("unavailable");
      return;
    }
    setEntries(scheduleResult.entries);
    setFromCache(scheduleResult.fromCache);
    setStatus("loaded");

    const [meResult, locationsResult, groupsResult, closuresResult] = await Promise.all([
      apiClient.GET("/api/staff/me"),
      apiClient.GET("/api/locations/names"),
      apiClient.GET("/api/groups"),
      apiClient.GET("/api/closures/dates", {
        params: { query: { from: allDates[0], to: allDates[allDates.length - 1] } },
      }),
    ]);

    if (meResult.response.ok && meResult.data) {
      setContractedDays((meResult.data as unknown as { contractedDays: string[] }).contractedDays ?? []);
    }
    if (locationsResult.response.ok && locationsResult.data) {
      const locations = locationsResult.data as unknown as { id: string; name: string }[];
      setLocationNamesById(new Map(locations.map((l) => [l.id, l.name])));
    }
    if (groupsResult.response.ok && groupsResult.data) {
      const groups = groupsResult.data as unknown as { id: string; name: string }[];
      setGroupNamesById(new Map(groups.map((g) => [g.id, g.name])));
    }
    if (closuresResult.response.ok && closuresResult.data) {
      const closures = closuresResult.data as unknown as { locationId: string; date: string }[];
      const byDate = new Map<string, Set<string>>();
      for (const closure of closures) {
        const set = byDate.get(closure.date) ?? new Set<string>();
        set.add(closure.locationId);
        byDate.set(closure.date, set);
      }
      setClosedLocationIdsByDate(byDate);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const onRefresh = async () => {
    setRefreshing(true);
    await load();
    setRefreshing(false);
  };

  const entriesByDate = useMemo(() => {
    const map = new Map<string, StaffScheduleResponse[]>();
    for (const entry of entries) {
      const list = map.get(entry.date) ?? [];
      list.push(entry);
      map.set(entry.date, list);
    }
    return map;
  }, [entries]);

  const hasAnyEntries = allDates.some((date) => (entriesByDate.get(date) ?? []).length > 0);

  if (status === "loading") {
    return (
      <View style={{ flex: 1, backgroundColor: colors.background, alignItems: "center", justifyContent: "center" }}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  if (status === "unavailable") {
    return (
      <ScreenContainer>
        <View className="flex-1 items-center justify-center px-6" style={{ backgroundColor: colors.background }}>
          <WifiOff color={colors.textSoft} size={32} strokeWidth={2} />
          <Text className="text-text-soft dark:text-text-soft-dark text-sm text-center mt-3">
            {t("schedule.networkError")}
          </Text>
        </View>
      </ScreenContainer>
    );
  }

  return (
    <ScreenContainer>
      <ScrollView
        className="flex-1 bg-background dark:bg-background-dark"
        contentContainerStyle={{ padding: 16 }}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
      >
        {fromCache && (
          <View className="bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-3 py-2 mb-3">
            <Text className="text-text-soft dark:text-text-soft-dark text-xs">{t("schedule.offlineCached")}</Text>
          </View>
        )}

        <TouchableOpacity
          onPress={() => router.push("/(app)/report-sick")}
          className="bg-danger dark:bg-danger-dark rounded-lg py-4 items-center mb-4 flex-row justify-center gap-2"
          style={{ minHeight: 48 }}
          testID="report-sick-button"
        >
          <ThermometerSun color="#fff" size={20} strokeWidth={2} />
          <Text className="text-white text-base font-bold">{t("schedule.reportSickAction")}</Text>
        </TouchableOpacity>

        <View className="flex-row bg-surface-soft dark:bg-surface-soft-dark rounded-lg p-1 mb-4">
          <TouchableOpacity
            onPress={() => setViewMode("week")}
            className={`flex-1 items-center py-2 rounded-lg ${viewMode === "week" ? "bg-surface dark:bg-surface-dark" : ""}`}
            style={{ minHeight: 40 }}
          >
            <Text className="text-text dark:text-text-dark text-sm font-medium">{t("schedule.weekView")}</Text>
          </TouchableOpacity>
          <TouchableOpacity
            onPress={() => setViewMode("day")}
            className={`flex-1 items-center py-2 rounded-lg ${viewMode === "day" ? "bg-surface dark:bg-surface-dark" : ""}`}
            style={{ minHeight: 40 }}
          >
            <Text className="text-text dark:text-text-dark text-sm font-medium">{t("schedule.dayView")}</Text>
          </TouchableOpacity>
        </View>

        {!hasAnyEntries && (
          <View className="items-center" style={{ paddingVertical: 32 }}>
            <CalendarX color={colors.textSoft} size={28} strokeWidth={2} />
            <Text className="text-text-soft dark:text-text-soft-dark text-sm mt-3">{t("schedule.empty")}</Text>
          </View>
        )}

        {viewMode === "week" &&
          weeks.map((weekDates, i) => (
            <View key={weekDates[0]}>
              <Text className="text-text-soft dark:text-text-soft-dark text-xs font-semibold uppercase mt-2 mb-2">
                {t("schedule.weekOf", { date: dayjs(weekDates[0]).format("D MMM") })}
              </Text>
              <ScheduleWeekList
                weekDates={weekDates}
                entriesByDate={entriesByDate}
                locationNamesById={locationNamesById}
                groupNamesById={groupNamesById}
                closedLocationIdsByDate={closedLocationIdsByDate}
                contractedDays={contractedDays}
              />
            </View>
          ))}

        {viewMode === "day" && (
          <>
            <ScrollView horizontal showsHorizontalScrollIndicator={false} className="mb-4">
              {allDates.map((date) => (
                <TouchableOpacity
                  key={date}
                  onPress={() => setSelectedDate(date)}
                  className={`rounded-lg px-3 py-2 mr-2 ${date === selectedDate ? "bg-primary dark:bg-primary-dark" : "bg-surface-soft dark:bg-surface-soft-dark"}`}
                  style={{ minHeight: 48, justifyContent: "center" }}
                >
                  <Text className={date === selectedDate ? "text-white text-sm font-semibold" : "text-text dark:text-text-dark text-sm"}>
                    {dayjs(date).format("ddd D")}
                  </Text>
                </TouchableOpacity>
              ))}
            </ScrollView>
            <ScheduleDayCard
              date={selectedDate}
              entries={entriesByDate.get(selectedDate) ?? []}
              locationNamesById={locationNamesById}
              groupNamesById={groupNamesById}
              closedLocationIds={closedLocationIdsByDate.get(selectedDate) ?? new Set()}
              isNonContractedDay={
                contractedDays.length > 0 &&
                !contractedDays.includes(["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"][new Date(`${selectedDate}T00:00:00`).getDay()])
              }
            />
          </>
        )}
      </ScrollView>
    </ScreenContainer>
  );
}
