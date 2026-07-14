import React, { useState } from "react";
import { View, Text, TextInput, TouchableOpacity, ScrollView } from "react-native";
import { useRouter, useLocalSearchParams } from "expo-router";
import { useTranslation } from "react-i18next";
import { submitMealPreferenceChangeRequest } from "../../../services/mealPreferenceRequests";
import { useColors } from "../../../hooks/useColors";
import type { MealTexture } from "../../../types";

const TEXTURES: MealTexture[] = ["pureed", "mixed", "pieces", "normal"];
const DIETARY_TYPES = ["halal", "kosher", "vegetarian", "vegan", "gluten_free"];

/** Meal-preference-change request form (feature 013e, US3) — reached from the Menu tab's
 * per-child "Voorkeur aanpassen" entry point, so childId always arrives via route params
 * rather than a second child-picker (the parent already chose the child on the prior screen). */
export default function RequestPreferenceChangeScreen() {
  const { t } = useTranslation();
  const colors = useColors();
  const router = useRouter();
  const { childId } = useLocalSearchParams<{ childId: string }>();

  const [texture, setTexture] = useState<MealTexture | null>(null);
  const [dietaryType, setDietaryType] = useState<string[]>([]);
  const [notes, setNotes] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  const toggleDietaryType = (value: string) => {
    setDietaryType((current) => (current.includes(value) ? current.filter((v) => v !== value) : [...current, value]));
  };

  const canSubmit = !!childId && (texture !== null || dietaryType.length > 0) && !submitting;

  const handleSubmit = async () => {
    if (!childId) return;
    setError("");
    setSubmitting(true);
    try {
      await submitMealPreferenceChangeRequest(childId, texture, dietaryType.length > 0 ? dietaryType : null, notes.trim() || null);
      router.back();
    } catch (e) {
      const errorKey = (e as Error).message;
      setError(t(errorKey, { defaultValue: t("mealPreferenceRequests.submitFailed") }));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <ScrollView className="flex-1 bg-background dark:bg-background-dark" contentContainerStyle={{ padding: 16 }}>
      <Text className="text-text dark:text-text-dark text-xl font-bold mb-4">{t("mealPreferenceRequests.formTitle")}</Text>

      {!!error && <Text className="text-danger dark:text-danger-dark text-sm mb-4">{error}</Text>}

      <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium mb-1">{t("mealPreferenceRequests.textureLabel")}</Text>
      <View className="flex-row flex-wrap mb-4" style={{ gap: 8 }}>
        {TEXTURES.map((value) => (
          <TouchableOpacity
            key={value}
            testID={`texture-${value}`}
            onPress={() => setTexture(texture === value ? null : value)}
            className={`rounded-lg px-4 ${texture === value ? "bg-primary-soft dark:bg-primary-soft-dark" : "bg-surface-soft dark:bg-surface-soft-dark"}`}
            style={{ minHeight: 48, justifyContent: "center" }}
          >
            <Text className="text-text dark:text-text-dark text-sm">{t(`mealPreferenceRequests.texture.${value}`)}</Text>
          </TouchableOpacity>
        ))}
      </View>

      <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium mb-1">{t("mealPreferenceRequests.dietaryTypeLabel")}</Text>
      <View className="flex-row flex-wrap mb-4" style={{ gap: 8 }}>
        {DIETARY_TYPES.map((value) => (
          <TouchableOpacity
            key={value}
            testID={`dietary-${value}`}
            onPress={() => toggleDietaryType(value)}
            className={`rounded-lg px-4 ${dietaryType.includes(value) ? "bg-primary-soft dark:bg-primary-soft-dark" : "bg-surface-soft dark:bg-surface-soft-dark"}`}
            style={{ minHeight: 48, justifyContent: "center" }}
          >
            <Text className="text-text dark:text-text-dark text-sm">{t(`mealPreferenceRequests.dietaryType.${value}`)}</Text>
          </TouchableOpacity>
        ))}
      </View>

      <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium mb-1">{t("mealPreferenceRequests.notesLabel")}</Text>
      <TextInput
        testID="notes-input"
        value={notes}
        onChangeText={setNotes}
        multiline
        className="bg-surface-soft dark:bg-surface-soft-dark text-text dark:text-text-dark rounded-lg px-4 py-3 mb-6"
        style={{ minHeight: 96, textAlignVertical: "top" }}
        placeholderTextColor={colors.textSoft}
      />

      <TouchableOpacity
        testID="submit-button"
        disabled={!canSubmit}
        onPress={handleSubmit}
        className="bg-primary rounded-lg items-center justify-center"
        style={{ minHeight: 48, opacity: canSubmit ? 1 : 0.5 }}
      >
        <Text className="text-white text-sm font-semibold">{t("mealPreferenceRequests.submit")}</Text>
      </TouchableOpacity>
    </ScrollView>
  );
}
