import React from "react";
import { View, Text, ScrollView } from "react-native";
import { useLocalSearchParams } from "expo-router";
import { useTranslation } from "react-i18next";
import { getCached } from "../../../services/readCache";
import { useColors } from "../../../hooks/useColors";
import type { ChildResponse } from "../../../types";
import { CHILDREN_CACHE_KEY } from "../index";

export default function ChildDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { t } = useTranslation();
  const colors = useColors();

  const children = getCached<ChildResponse[]>(CHILDREN_CACHE_KEY) ?? [];
  const child = children.find((c) => c.id === id);

  if (!child) {
    return (
      <View style={{ flex: 1, alignItems: "center", justifyContent: "center", backgroundColor: colors.background }}>
        <Text style={{ color: colors.secondaryText }}>{t("groupView.empty")}</Text>
      </View>
    );
  }

  return (
    <ScrollView style={{ flex: 1, backgroundColor: colors.background }} contentContainerStyle={{ padding: 20 }}>
      <Text className="text-gray-900 dark:text-white text-2xl font-bold mb-1">
        {child.firstName} {child.lastName}
      </Text>
      <Text style={{ color: colors.secondaryText, marginBottom: 20 }}>{t("child.medicalQuickAccess")}</Text>

      {!!child.allergiesDescription && (
        <View className="bg-red-50 dark:bg-red-900/30 rounded-2xl p-4 mb-3">
          <Text className="text-red-700 dark:text-red-300 font-semibold mb-1">{t("child.allergyAlert")}</Text>
          <Text className="text-red-700 dark:text-red-300">{child.allergiesDescription}</Text>
          {!!child.allergySeverity && (
            <Text className="text-red-600 dark:text-red-400 mt-1">{child.allergySeverity}</Text>
          )}
        </View>
      )}

      {!!child.medicalConditions && (
        <View className="bg-white dark:bg-gray-800 rounded-2xl p-4 mb-3">
          <Text className="text-gray-900 dark:text-white font-semibold mb-1">
            {t("child.medicalConditions", { defaultValue: "Medical conditions" })}
          </Text>
          <Text className="text-gray-700 dark:text-gray-300">{child.medicalConditions}</Text>
        </View>
      )}

      {!!child.dietaryRestrictions && (
        <View className="bg-white dark:bg-gray-800 rounded-2xl p-4 mb-3">
          <Text className="text-gray-900 dark:text-white font-semibold mb-1">
            {t("child.dietaryRestrictions", { defaultValue: "Dietary restrictions" })}
          </Text>
          <Text className="text-gray-700 dark:text-gray-300">{child.dietaryRestrictions}</Text>
        </View>
      )}
    </ScrollView>
  );
}
