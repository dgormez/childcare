import React, { useState, useEffect } from "react";
import {
  View, TextInput, TouchableOpacity, Text,
  KeyboardAvoidingView, ActivityIndicator, Alert, ScrollView,
} from "react-native";
import { useRouter, useLocalSearchParams } from "expo-router";
import Toast from "react-native-toast-message";
import {
  updateHabit as apiUpdateHabit,
  deleteHabit as apiDeleteHabit,
} from "../../services/api";
import { saveHabitsLocally, deleteLocalHabit } from "../../services/localDb";
import { useStore } from "../../store/useStore";
import { useColors } from "../../hooks/useColors";
import { ScreenContainer } from "../../components/ScreenContainer";

const ICONS  = ["✅", "💪", "📚", "🏃", "💧", "🧘", "🥗", "😴", "🎯", "🎨"];
const COLORS = ["#3b82f6", "#10b981", "#f59e0b", "#ef4444", "#8b5cf6", "#ec4899", "#06b6d4", "#f97316"];

export default function EditHabitScreen() {
  const router = useRouter();
  const { id } = useLocalSearchParams<{ id: string }>();
  const { habits, updateHabit, removeHabit } = useStore();
  const colors = useColors();

  const habit = habits.find((h) => h.id === id);

  const [name,     setName]     = useState(habit?.name  ?? "");
  const [icon,     setIcon]     = useState(habit?.icon  ?? "✅");
  const [color,    setColor]    = useState(habit?.color ?? "#3b82f6");
  const [saving,   setSaving]   = useState(false);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    if (!habit) router.back();
  }, [habit]);

  const handleSave = async () => {
    const trimmed = name.trim();
    if (!trimmed || !id) return;

    setSaving(true);
    try {
      const updated = await apiUpdateHabit(id, trimmed, color, icon);
      saveHabitsLocally([updated]);
      updateHabit(updated);
      router.back();
    } catch {
      Toast.show({ type: "error", text1: "Could not save habit" });
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = () => {
    Alert.alert("Delete habit", "This habit and all its completions will be permanently deleted.", [
      { text: "Cancel", style: "cancel" },
      {
        text: "Delete",
        style: "destructive",
        onPress: async () => {
          if (!id) return;
          setDeleting(true);
          try {
            await apiDeleteHabit(id);
            deleteLocalHabit(id);
            removeHabit(id);
            router.back();
          } catch {
            Toast.show({ type: "error", text1: "Could not delete habit" });
            setDeleting(false);
          }
        },
      },
    ]);
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
          <Text className="text-gray-900 dark:text-white text-lg font-bold">Edit Habit</Text>
          <TouchableOpacity onPress={handleSave} disabled={!name.trim() || saving || deleting}>
            {saving
              ? <ActivityIndicator size="small" color="#3b82f6" />
              : <Text className={`text-base font-semibold ${name.trim() ? "text-blue-600" : "text-gray-400"}`}>Save</Text>
            }
          </TouchableOpacity>
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
          placeholder="Habit name"
          placeholderTextColor={colors.placeholder}
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

        {/* Delete */}
        <TouchableOpacity
          onPress={handleDelete}
          disabled={deleting || saving}
          className="border border-red-200 dark:border-red-800 rounded-2xl py-4 items-center mb-8"
        >
          {deleting
            ? <ActivityIndicator color="#ef4444" />
            : <Text className="text-red-500 dark:text-red-400 font-semibold text-base">Delete Habit</Text>
          }
        </TouchableOpacity>

      </ScrollView>
      </ScreenContainer>
    </KeyboardAvoidingView>
  );
}
