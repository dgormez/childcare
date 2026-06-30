import React, { useCallback, useEffect, useState } from "react";
import {
  View, Text, FlatList, TouchableOpacity, RefreshControl, ActivityIndicator,
} from "react-native";
import { useRouter } from "expo-router";
import dayjs from "dayjs";
import { useStore } from "../../store/useStore";
import { useSync } from "../../hooks/useSync";
import { completeHabit, uncompleteHabit } from "../../services/api";
import { saveCompletionsLocally, deleteLocalCompletion } from "../../services/localDb";
import { ScreenContainer } from "../../components/ScreenContainer";
import type { Habit } from "../../types";

function getToday()     { return dayjs().format("YYYY-MM-DD"); }
function getTodayLabel() { return dayjs().format("dddd, MMMM D"); }

function TrialBanner({ endsAt }: { endsAt: string | null }) {
  const router = useRouter();
  const daysLeft = endsAt ? dayjs(endsAt).diff(dayjs(), "day") : null;
  return (
    <TouchableOpacity
      onPress={() => router.push("/(tabs)/subscription")}
      className="mx-5 mb-4 bg-blue-50 dark:bg-blue-950 border border-blue-200 dark:border-blue-800 rounded-xl px-4 py-3 flex-row items-center justify-between"
    >
      <View className="flex-1">
        <Text className="text-blue-700 dark:text-blue-300 font-semibold text-sm">
          ⏳ Free Trial Active
        </Text>
        <Text className="text-blue-600 dark:text-blue-400 text-xs mt-0.5">
          {daysLeft != null && daysLeft >= 0
            ? `${daysLeft} day${daysLeft !== 1 ? "s" : ""} remaining • Tap to upgrade`
            : "Trial ending soon • Tap to upgrade"}
        </Text>
      </View>
      <Text className="text-blue-600 dark:text-blue-400 text-xs font-semibold ml-2">Pro →</Text>
    </TouchableOpacity>
  );
}

function HabitRow({ habit, completed, onToggle }: {
  habit: Habit;
  completed: boolean;
  onToggle: () => void;
}) {
  return (
    <TouchableOpacity
      onPress={onToggle}
      activeOpacity={0.7}
      className="flex-row items-center bg-gray-100 dark:bg-gray-800 rounded-xl px-4 py-4 mb-3"
    >
      {/* Completion circle */}
      <View
        className={`w-7 h-7 rounded-full mr-4 items-center justify-center border-2 ${
          completed
            ? "bg-green-500 border-green-500"
            : "border-gray-300 dark:border-gray-600 bg-transparent"
        }`}
      >
        {completed && <Text className="text-white text-xs font-bold">✓</Text>}
      </View>

      {/* Icon + name */}
      <Text style={{ fontSize: 22, marginRight: 10 }}>{habit.icon}</Text>
      <Text
        className={`flex-1 text-base font-medium ${
          completed
            ? "text-gray-400 dark:text-gray-500 line-through"
            : "text-gray-900 dark:text-white"
        }`}
      >
        {habit.name}
      </Text>

      {/* Color dot */}
      <View
        className="w-3 h-3 rounded-full"
        style={{ backgroundColor: habit.color }}
      />
    </TouchableOpacity>
  );
}

export default function TodayScreen() {
  const router    = useRouter();
  const { habits, completions, subscription, isSyncing, addCompletion, removeCompletion } = useStore();
  const { sync }  = useSync();

  const [today, setToday] = useState(getToday);

  // Re-schedule at each midnight so the screen stays correct for long-running sessions
  useEffect(() => {
    const now = new Date();
    const msUntilMidnight = +new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1) - +now;
    const timer = setTimeout(() => setToday(getToday()), msUntilMidnight);
    return () => clearTimeout(timer);
  }, [today]);

  const completedIds = new Set(
    completions.filter((c) => c.date === today).map((c) => c.habitId)
  );

  const toggle = useCallback(async (habit: Habit) => {
    const isCompleted = completedIds.has(habit.id);
    if (isCompleted) {
      removeCompletion(habit.id, today);
      deleteLocalCompletion(habit.id, today);
      try { await uncompleteHabit(habit.id, today); } catch { /* silent — will re-sync */ }
    } else {
      try {
        const completion = await completeHabit(habit.id, today);
        addCompletion(completion);
        saveCompletionsLocally([completion]);
      } catch { /* silent — offline */ }
    }
  }, [today, completedIds, addCompletion, removeCompletion]);

  const doneCount = habits.filter((h) => completedIds.has(h.id)).length;
  const isTrialing = subscription?.status === "Trialing";

  return (
    <View className="flex-1 bg-white dark:bg-gray-900">
      <ScreenContainer>

        {/* Header */}
        <View className="px-5 pt-16 pb-4 flex-row items-center justify-between">
          <View>
            <Text className="text-gray-900 dark:text-white text-2xl font-bold">Today</Text>
            <Text className="text-gray-500 dark:text-gray-400 text-sm mt-0.5">{getTodayLabel()}</Text>
          </View>
          <TouchableOpacity
            onPress={() => router.push("/(tabs)/habits")}
            className="bg-blue-600 px-4 py-2 rounded-xl"
          >
            <Text className="text-white text-sm font-semibold">Manage</Text>
          </TouchableOpacity>
        </View>

        {/* Trial banner */}
        {isTrialing && <TrialBanner endsAt={subscription?.currentPeriodEnd ?? null} />}

        {/* Progress */}
        {habits.length > 0 && (
          <View className="mx-5 mb-4">
            <View className="flex-row items-center justify-between mb-1">
              <Text className="text-gray-500 dark:text-gray-400 text-xs">
                {doneCount} of {habits.length} done
              </Text>
              <Text className="text-gray-500 dark:text-gray-400 text-xs">
                {habits.length > 0 ? Math.round((doneCount / habits.length) * 100) : 0}%
              </Text>
            </View>
            <View className="h-1.5 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
              <View
                className="h-full bg-green-500 rounded-full"
                style={{ width: `${habits.length > 0 ? (doneCount / habits.length) * 100 : 0}%` }}
              />
            </View>
          </View>
        )}

        {/* List */}
        <FlatList
          data={habits}
          keyExtractor={(h) => h.id}
          contentContainerStyle={{ paddingHorizontal: 20, paddingBottom: 30 }}
          refreshControl={
            <RefreshControl refreshing={isSyncing} onRefresh={sync} tintColor="#3b82f6" />
          }
          ListEmptyComponent={
            <View className="items-center pt-24">
              <Text className="text-5xl mb-4">🌱</Text>
              <Text className="text-gray-900 dark:text-white text-xl font-bold mb-2">
                No habits yet
              </Text>
              <Text className="text-gray-400 dark:text-gray-500 text-sm text-center mb-6">
                Start building better habits today
              </Text>
              <TouchableOpacity
                onPress={() => router.push("/(tabs)/habits")}
                className="bg-blue-600 px-6 py-3 rounded-xl"
              >
                <Text className="text-white font-semibold">Add your first habit</Text>
              </TouchableOpacity>
            </View>
          }
          renderItem={({ item }) => (
            <HabitRow
              habit={item}
              completed={completedIds.has(item.id)}
              onToggle={() => toggle(item)}
            />
          )}
        />

      </ScreenContainer>
    </View>
  );
}
