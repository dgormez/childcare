import React, { useCallback, useEffect, useState } from "react";
import { View, Text, SectionList, ActivityIndicator, Switch } from "react-native";
import { useFocusEffect } from "expo-router";
import { useTranslation } from "react-i18next";
import { AlertTriangle, AlertCircle, Circle, Pill, UtensilsCrossed } from "lucide-react-native";
import { getMealList } from "../../services/mealList";
import { useColors } from "../../hooks/useColors";
import type { MealListChildEntry, MealListGroupEntry, AllergySeverityWireValue } from "../../types";

const SEVERITY_ICON: Record<AllergySeverityWireValue, typeof AlertTriangle> = {
  severe: AlertTriangle,
  mild_moderate: AlertCircle,
  none: Circle,
};

function AllergySeverityBadge({ severity }: { severity: AllergySeverityWireValue }) {
  const { t } = useTranslation();
  const colors = useColors();
  const Icon = SEVERITY_ICON[severity];
  const color = severity === "severe" ? colors.danger : severity === "mild_moderate" ? colors.warningFg : colors.textSoft;
  const bg = severity === "severe" ? colors.dangerBg : severity === "mild_moderate" ? colors.warning : colors.surfaceSoft;

  return (
    <View className="flex-row items-center rounded-full px-2 py-1" style={{ backgroundColor: bg, gap: 4 }}>
      <Icon size={14} strokeWidth={2} color={color} />
      <Text style={{ color, fontSize: 12, fontWeight: "500" }}>{t(`mealList.allergySeverity.${severity}`)}</Text>
    </View>
  );
}

function ChildRow({ child }: { child: MealListChildEntry }) {
  const { t } = useTranslation();
  const colors = useColors();

  return (
    <View className="flex-row items-center justify-between rounded-xl bg-surface dark:bg-surface-dark px-4 mb-2" style={{ minHeight: 48, paddingVertical: 12 }}>
      <View style={{ flex: 1 }}>
        <Text className="text-text dark:text-text-dark font-medium">{child.firstName} {child.lastName}</Text>
        <Text className="text-text-soft dark:text-text-soft-dark" style={{ fontSize: 13 }}>
          {child.hasPreference
            ? `${t(`mealList.texture.${child.texture}`)} · ${t(`mealList.portionSize.${child.portionSize}`)}${
                child.dietaryType.length > 0 ? ` · ${child.dietaryType.map((d) => t(`mealList.dietaryType.${d}`)).join(", ")}` : ""
              }`
            : t("mealList.noPreference")}
        </Text>
      </View>
      <View className="flex-row items-center" style={{ gap: 8 }}>
        {child.hasStandingMedication && <Pill size={18} strokeWidth={2} color={colors.textSoft} />}
        <AllergySeverityBadge severity={child.allergySeverity} />
      </View>
    </View>
  );
}

export default function MealListScreen() {
  const { t } = useTranslation();
  const colors = useColors();

  const [groups, setGroups] = useState<MealListGroupEntry[] | null>(null);
  const [expected, setExpected] = useState<MealListChildEntry[]>([]);
  const [includeExpected, setIncludeExpected] = useState(false);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async (withExpected: boolean) => {
    setLoading(true);
    const result = await getMealList(withExpected);
    if (result.status === "loaded") {
      setGroups(result.mealList.groups);
      setExpected(result.mealList.expected?.children ?? []);
    }
    setLoading(false);
  }, []);

  useEffect(() => {
    load(includeExpected);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [includeExpected]);

  useFocusEffect(useCallback(() => { load(includeExpected); /* eslint-disable-line react-hooks/exhaustive-deps */ }, []));

  if (loading && !groups) {
    return (
      <View style={{ flex: 1, alignItems: "center", justifyContent: "center", backgroundColor: colors.background }}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  const sections = [
    ...(groups ?? []).map((g) => ({ title: g.groupName, data: g.children })),
    ...(includeExpected && expected.length > 0 ? [{ title: t("mealList.expectedSectionTitle"), data: expected }] : []),
  ];

  return (
    <View style={{ flex: 1, backgroundColor: colors.background }}>
      <View className="flex-row items-center justify-between px-4 py-3">
        <Text className="text-text dark:text-text-dark" style={{ fontWeight: "600" }}>{t("mealList.includeExpected")}</Text>
        <Switch value={includeExpected} onValueChange={setIncludeExpected} />
      </View>

      {sections.every((s) => s.data.length === 0) ? (
        <View style={{ flex: 1, alignItems: "center", justifyContent: "center", padding: 32 }}>
          <UtensilsCrossed size={24} strokeWidth={2} color={colors.textSoft} />
          <Text className="text-text-soft dark:text-text-soft-dark" style={{ marginTop: 12, textAlign: "center" }}>
            {t("mealList.emptyState")}
          </Text>
        </View>
      ) : (
        <SectionList
          testID="meal-list"
          sections={sections}
          keyExtractor={(child) => child.childId}
          contentContainerStyle={{ padding: 16 }}
          renderSectionHeader={({ section }) => (
            <Text className="text-text-soft dark:text-text-soft-dark" style={{ fontSize: 12, fontWeight: "600", textTransform: "uppercase", marginBottom: 8, marginTop: 12 }}>
              {section.title}
            </Text>
          )}
          renderItem={({ item }) => <ChildRow child={item} />}
        />
      )}
    </View>
  );
}
