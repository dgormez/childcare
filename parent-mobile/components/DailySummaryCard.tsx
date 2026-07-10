import React from "react";
import { View, Text } from "react-native";
import { useTranslation } from "react-i18next";
import { Moon, Milk, Droplets, Smile, Thermometer, Pill, Activity as ActivityIcon } from "lucide-react-native";
import { useColors } from "../hooks/useColors";
import type { DailySummaryResponse, ParentChildResponse } from "../types";

interface Props {
  child:   ParentChildResponse;
  summary: DailySummaryResponse | null;
}

interface RowProps {
  icon:  React.ReactNode;
  label: string;
}

function SummaryRow({ icon, label }: RowProps) {
  return (
    <View className="flex-row items-center" style={{ minHeight: 56, paddingVertical: 8 }}>
      <View style={{ width: 32, alignItems: "center" }}>{icon}</View>
      <Text className="text-text dark:text-text-dark text-base ml-2 flex-1">{label}</Text>
    </View>
  );
}

export function DailySummaryCard({ child, summary }: Props) {
  const { t } = useTranslation();
  const colors = useColors();

  const hasAnyData = !!summary && (
    summary.napsCount > 0 ||
    summary.bottlesCount > 0 ||
    summary.diaperChangesCount > 0 ||
    !!summary.latestMood ||
    summary.latestTemperatureCelsius !== null ||
    summary.medicationAdministered ||
    summary.activities.length > 0
  );

  return (
    <View className="bg-surface dark:bg-surface-dark rounded-xl p-4 mb-4" testID={`daily-summary-card-${child.id}`}>
      <View className="flex-row items-center mb-2">
        <View
          className="bg-primary-soft dark:bg-primary-soft-dark items-center justify-center"
          style={{ width: 40, height: 40, borderRadius: 20 }}
        >
          <Text className="text-primary-hover dark:text-primary-hover-dark font-bold">
            {child.firstName.charAt(0).toUpperCase()}
          </Text>
        </View>
        <Text className="text-text dark:text-text-dark text-lg font-bold ml-3">
          {child.firstName} {child.lastName}
        </Text>
      </View>

      {!hasAnyData && (
        <View className="items-center" style={{ paddingVertical: 24 }}>
          <ActivityIcon color={colors.textSoft} size={24} strokeWidth={2} />
          <Text className="text-text-soft dark:text-text-soft-dark text-sm mt-2">{t("home.noUpdatesYet")}</Text>
        </View>
      )}

      {hasAnyData && summary && (
        <View>
          {summary.napsCount > 0 && (
            <SummaryRow
              icon={<Moon color={colors.text} size={20} strokeWidth={2} />}
              label={`${t("home.naps")}: ${summary.napsCount}`}
            />
          )}
          {summary.bottlesCount > 0 && (
            <SummaryRow
              icon={<Milk color={colors.text} size={20} strokeWidth={2} />}
              label={`${t("home.bottles")}: ${summary.bottlesCount}`}
            />
          )}
          {summary.diaperChangesCount > 0 && (
            <SummaryRow
              icon={<Droplets color={colors.text} size={20} strokeWidth={2} />}
              label={`${t("home.diaperChanges")}: ${summary.diaperChangesCount}`}
            />
          )}
          {!!summary.latestMood && (
            <SummaryRow
              icon={<Smile color={colors.text} size={20} strokeWidth={2} />}
              label={`${t("home.mood")}: ${summary.latestMood}`}
            />
          )}
          {summary.latestTemperatureCelsius !== null && (
            <SummaryRow
              icon={<Thermometer color={colors.danger} size={20} strokeWidth={2} />}
              label={`${t("home.temperature")}: ${summary.latestTemperatureCelsius}°C`}
            />
          )}
          {summary.medicationAdministered && (
            <SummaryRow
              icon={<Pill color={colors.text} size={20} strokeWidth={2} />}
              label={t("home.medicationGiven")}
            />
          )}
          {summary.activities.length > 0 && (
            <View style={{ paddingVertical: 8 }}>
              <View className="flex-row items-center mb-1">
                <View style={{ width: 32, alignItems: "center" }}>
                  <ActivityIcon color={colors.text} size={20} strokeWidth={2} />
                </View>
                <Text className="text-text dark:text-text-dark text-base ml-2 font-medium">{t("home.activities")}</Text>
              </View>
              {summary.activities.map((activity, i) => (
                <Text key={i} className="text-text-soft dark:text-text-soft-dark text-sm" style={{ marginLeft: 40, paddingVertical: 4 }}>
                  • {activity}
                </Text>
              ))}
            </View>
          )}
        </View>
      )}
    </View>
  );
}
