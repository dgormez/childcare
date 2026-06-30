import React from "react";
import {
  View, Text, FlatList, TouchableOpacity, RefreshControl,
} from "react-native";
import { useRouter } from "expo-router";
import { useStore } from "../../store/useStore";
import { useSync } from "../../hooks/useSync";
import { ScreenContainer } from "../../components/ScreenContainer";
import { completeHabit, uncompleteHabit } from "../../services/api";
import { saveCompletionsLocally, deleteLocalCompletion } from "../../services/localDb";

function todayDate() {
  return new Date().toISOString().slice(0, 10);
}

const FREE_LIMIT = 3;

export default function HabitsScreen() {
  const router = useRouter();
  const { habits, completions, subscription, isSyncing, addCompletion, removeCompletion } = useStore();
  const { sync } = useSync();
  const today = todayDate();

  const isCompletedToday = (habitId: string) =>
    completions.some((c) => c.habitId === habitId && c.date === today);

  const toggleCompletion = async (habitId: string) => {
    const done = isCompletedToday(habitId);
    if (done) {
      removeCompletion(habitId, today);
      deleteLocalCompletion(habitId, today);
      uncompleteHabit(habitId, today).catch(() => {
        // silently re-add on failure; next sync will reconcile
      });
    } else {
      const optimistic = { id: `${habitId}-${today}`, habitId, userId: "", date: today, createdAt: new Date().toISOString() };
      addCompletion(optimistic);
      try {
        const saved = await completeHabit(habitId, today);
        removeCompletion(habitId, today);
        addCompletion(saved);
        saveCompletionsLocally([saved]);
      } catch {
        removeCompletion(habitId, today);
      }
    }
  };

  const isActive    = subscription?.status === "Active" || subscription?.status === "Trialing";
  const atFreeLimit = !isActive && habits.length >= FREE_LIMIT;

  const handleAdd = () => {
    if (atFreeLimit) {
      router.push("/(tabs)/subscription");
      return;
    }
    router.push("/habit/add");
  };

  return (
    <View className="flex-1 bg-white dark:bg-gray-900">
      <ScreenContainer>

        {/* Header */}
        <View className="px-5 pt-16 pb-4 flex-row items-center justify-between">
          <Text className="text-gray-900 dark:text-white text-2xl font-bold">My Habits</Text>
          <TouchableOpacity
            onPress={handleAdd}
            className="bg-blue-600 w-10 h-10 rounded-full items-center justify-center"
          >
            <Text className="text-white text-2xl font-bold leading-none">+</Text>
          </TouchableOpacity>
        </View>

        {/* Free tier limit banner */}
        {atFreeLimit && (
          <TouchableOpacity
            onPress={() => router.push("/(tabs)/subscription")}
            className="mx-5 mb-4 bg-amber-50 dark:bg-amber-950 border border-amber-200 dark:border-amber-800 rounded-xl px-4 py-3"
          >
            <Text className="text-amber-700 dark:text-amber-300 font-semibold text-sm">
              🔒 Free plan limit reached
            </Text>
            <Text className="text-amber-600 dark:text-amber-400 text-xs mt-0.5">
              Upgrade to Pro to add unlimited habits
            </Text>
          </TouchableOpacity>
        )}

        {/* Habit list */}
        <FlatList
          data={habits}
          keyExtractor={(h) => h.id}
          contentContainerStyle={{ paddingHorizontal: 20, paddingBottom: 30 }}
          refreshControl={
            <RefreshControl refreshing={isSyncing} onRefresh={sync} tintColor="#3b82f6" />
          }
          ListEmptyComponent={
            <View className="items-center pt-24">
              <Text className="text-5xl mb-4">✨</Text>
              <Text className="text-gray-900 dark:text-white text-xl font-bold mb-2">
                No habits yet
              </Text>
              <Text className="text-gray-400 dark:text-gray-500 text-sm text-center mb-6">
                Tap + to create your first habit
              </Text>
            </View>
          }
          renderItem={({ item }) => {
            const done = isCompletedToday(item.id);
            return (
              <TouchableOpacity
                onPress={() => router.push(`/habit/${item.id}`)}
                className="bg-gray-100 dark:bg-gray-800 rounded-xl px-4 py-4 mb-3 flex-row items-center active:opacity-70"
              >
                {/* Color bar */}
                <View
                  className="w-1 h-10 rounded-full mr-3"
                  style={{ backgroundColor: item.color }}
                />

                {/* Icon + name */}
                <Text style={{ fontSize: 24, marginRight: 10 }}>{item.icon}</Text>
                <Text
                  className={`flex-1 font-semibold text-base ${
                    done ? "text-gray-400 dark:text-gray-500 line-through" : "text-gray-900 dark:text-white"
                  }`}
                >
                  {item.name}
                </Text>

                {/* Done toggle */}
                <TouchableOpacity
                  onPress={() => toggleCompletion(item.id)}
                  hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                  className={`w-7 h-7 rounded-full border-2 items-center justify-center ${
                    done
                      ? "border-transparent"
                      : "border-gray-300 dark:border-gray-600"
                  }`}
                  style={done ? { backgroundColor: item.color } : undefined}
                >
                  {done && <Text className="text-white text-xs font-bold">✓</Text>}
                </TouchableOpacity>
              </TouchableOpacity>
            );
          }}
        />

      </ScreenContainer>
    </View>
  );
}
