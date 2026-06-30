import React, { useState } from "react";
import {
  View, TextInput, TouchableOpacity, Text,
  KeyboardAvoidingView, ActivityIndicator, ScrollView,
} from "react-native";
import { useRouter } from "expo-router";
import Toast from "react-native-toast-message";
import { createHabit } from "../../services/api";
import { saveHabitsLocally } from "../../services/localDb";
import { useStore } from "../../store/useStore";
import { useColors } from "../../hooks/useColors";
import { ScreenContainer } from "../../components/ScreenContainer";

const ICONS  = ["✅", "💪", "📚", "🏃", "💧", "🧘", "🥗", "😴", "🎯", "🎨"];
const COLORS = ["#3b82f6", "#10b981", "#f59e0b", "#ef4444", "#8b5cf6", "#ec4899", "#06b6d4", "#f97316"];

export default function AddHabitScreen() {
  const router  = useRouter();
  const { addHabit, subscription, habits } = useStore();
  const colors  = useColors();

  const [name,   setName]   = useState("");
  const [icon,   setIcon]   = useState("✅");
  const [color,  setColor]  = useState("#3b82f6");
  const [saving, setSaving] = useState(false);

  const handleSave = async () => {
    const trimmed = name.trim();
    if (!trimmed) return;

    setSaving(true);
    try {
      const habit = await createHabit(trimmed, color, icon);
      saveHabitsLocally([habit]);
      addHabit(habit);
      router.back();
    } catch (err) {
      const msg = (err as Error).message ?? "";
      if (msg.includes("[403]")) {
        router.replace("/(tabs)/subscription");
        return;
      }
      Toast.show({ type: "error", text1: "Could not save habit", text2: "Check your connection and try again." });
    } finally {
      setSaving(false);
    }
  };

  return (
    <KeyboardAvoidingView behavior="padding" className="flex-1 bg-white dark:bg-gray-900">
      <ScreenContainer>
      <ScrollView className="flex-1 px-5 pt-4" keyboardShouldPersistTaps="handled">

        {/* Header */}
        <View className="flex-row items-center justify-between mb-6">
          <TouchableOpacity onPress={() => router.back()}>
            <Text className="text-blue-600 text-base">Cancel</Text>
          </TouchableOpacity>
          <Text className="text-gray-900 dark:text-white text-lg font-bold">New Habit</Text>
          <View style={{ width: 55 }} />
        </View>

        {/* Preview */}
        <View className="items-center mb-8">
          <View
            className="w-20 h-20 rounded-2xl items-center justify-center mb-2"
            style={{ backgroundColor: color + "22" }}
          >
            <Text style={{ fontSize: 40 }}>{icon}</Text>
          </View>
          <Text className="text-gray-900 dark:text-white font-semibold text-base">
            {name.trim() || "Habit name"}
          </Text>
        </View>

        {/* Name */}
        <Text className="text-gray-500 dark:text-gray-400 text-xs font-semibold uppercase tracking-wider mb-2">
          Name
        </Text>
        <TextInput
          value={name}
          onChangeText={setName}
          placeholder="e.g. Read 30 minutes"
          placeholderTextColor={colors.placeholder}
          autoFocus
          returnKeyType="done"
          className="bg-gray-100 dark:bg-gray-800 text-gray-900 dark:text-white text-base rounded-xl px-4 py-3 mb-6"
        />

        {/* Icon picker */}
        <Text className="text-gray-500 dark:text-gray-400 text-xs font-semibold uppercase tracking-wider mb-2">
          Icon
        </Text>
        <View className="flex-row flex-wrap gap-2 mb-6">
          {ICONS.map((i) => (
            <TouchableOpacity
              key={i}
              onPress={() => setIcon(i)}
              className={`w-12 h-12 rounded-xl items-center justify-center ${
                icon === i ? "bg-blue-100 dark:bg-blue-900" : "bg-gray-100 dark:bg-gray-800"
              }`}
            >
              <Text style={{ fontSize: 24 }}>{i}</Text>
            </TouchableOpacity>
          ))}
        </View>

        {/* Color picker */}
        <Text className="text-gray-500 dark:text-gray-400 text-xs font-semibold uppercase tracking-wider mb-2">
          Color
        </Text>
        <View className="flex-row flex-wrap gap-3 mb-8">
          {COLORS.map((c) => (
            <TouchableOpacity
              key={c}
              onPress={() => setColor(c)}
              className={`w-10 h-10 rounded-full items-center justify-center ${
                color === c ? "border-4 border-gray-400 dark:border-gray-500" : ""
              }`}
              style={{ backgroundColor: c }}
            />
          ))}
        </View>

        {/* Save button */}
        <TouchableOpacity
          onPress={handleSave}
          disabled={!name.trim() || saving}
          className={`rounded-2xl py-4 items-center mb-8 ${name.trim() && !saving ? "bg-blue-600" : "bg-gray-300 dark:bg-gray-600"}`}
        >
          {saving
            ? <ActivityIndicator color="#fff" />
            : <Text className="text-white font-bold text-base">Save habit</Text>
          }
        </TouchableOpacity>

      </ScrollView>
      </ScreenContainer>
    </KeyboardAvoidingView>
  );
}
