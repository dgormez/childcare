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
        <Text style={{ color: colors.textSoft }}>{t("groupView.empty")}</Text>
      </View>
    );
  }

  return (
    <ScrollView style={{ flex: 1, backgroundColor: colors.background }} contentContainerStyle={{ padding: 20 }}>
      <Text className="text-text dark:text-text-dark text-2xl font-bold mb-1">
        {child.firstName} {child.lastName}
      </Text>
      <Text style={{ color: colors.textSoft, marginBottom: 20 }}>{t("child.medicalQuickAccess")}</Text>

      {!!child.allergiesDescription && (
        <View className="bg-danger-bg dark:bg-danger-bg-dark rounded-xl p-4 mb-3">
          <Text className="text-danger dark:text-danger-dark font-semibold mb-1">{t("child.allergyAlert")}</Text>
          <Text className="text-danger dark:text-danger-dark">{child.allergiesDescription}</Text>
          {!!child.allergySeverity && (
            <Text className="text-danger dark:text-danger-dark mt-1 opacity-80">{child.allergySeverity}</Text>
          )}
        </View>
      )}

      {!!child.medicalConditions && (
        <View className="bg-surface dark:bg-surface-dark rounded-xl p-4 mb-3">
          <Text className="text-text dark:text-text-dark font-semibold mb-1">
            {t("child.medicalConditions")}
          </Text>
          <Text className="text-text-soft dark:text-text-soft-dark">{child.medicalConditions}</Text>
        </View>
      )}

      {!!child.dietaryRestrictions && (
        <View className="bg-surface dark:bg-surface-dark rounded-xl p-4 mb-3">
          <Text className="text-text dark:text-text-dark font-semibold mb-1">
            {t("child.dietaryRestrictions")}
          </Text>
          <Text className="text-text-soft dark:text-text-soft-dark">{child.dietaryRestrictions}</Text>
        </View>
      )}
    </ScrollView>
  );
}
