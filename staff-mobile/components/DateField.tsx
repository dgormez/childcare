import React, { useMemo } from "react";
import { View, Text, TextInput, TouchableOpacity, ScrollView } from "react-native";
import { useTranslation } from "react-i18next";
import { useColors } from "../hooks/useColors";

interface DateFieldProps {
  label: string;
  value: string;
  onChange: (value: string) => void;
}

function toDateString(d: Date): string {
  const year = d.getFullYear();
  const month = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

/** Quick-pick chips for the next 7 days plus a manual YYYY-MM-DD fallback for dates further out
 * — mirrors parent-mobile/components/DateField.tsx exactly (research.md R7's copied-pattern
 * convention). No native date-picker dependency — pure-JS, same reasoning as the parent-mobile
 * original (no simulator/screenshot tooling in this repo to verify a new native module boots). */
export function DateField({ label, value, onChange }: DateFieldProps) {
  const { t } = useTranslation();
  const colors = useColors();

  const quickDates = useMemo(() => {
    const today = new Date();
    return Array.from({ length: 7 }, (_, i) => {
      const d = new Date(today);
      d.setDate(d.getDate() + i);
      return { value: toDateString(d), date: d, index: i };
    });
  }, []);

  return (
    <View className="mb-4">
      <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium mb-1">{label}</Text>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} className="mb-2">
        {quickDates.map((qd) => {
          const selected = value === qd.value;
          const dayLabel = qd.index === 0 ? t("leaveRequests.todayLabel") : qd.index === 1 ? t("leaveRequests.tomorrowLabel") : qd.date.toLocaleDateString(undefined, { weekday: "short", day: "numeric" });
          return (
            <TouchableOpacity
              key={qd.value}
              onPress={() => onChange(qd.value)}
              className={`rounded-lg px-4 mr-2 ${selected ? "bg-primary dark:bg-primary-dark" : "bg-surface-soft dark:bg-surface-soft-dark"}`}
              style={{ minHeight: 48, justifyContent: "center" }}
            >
              <Text className={selected ? "text-white font-semibold" : "text-text dark:text-text-dark"}>{dayLabel}</Text>
            </TouchableOpacity>
          );
        })}
      </ScrollView>
      <TextInput
        value={value}
        onChangeText={onChange}
        placeholder={t("leaveRequests.chooseDate")}
        placeholderTextColor={colors.placeholder}
        className="bg-surface-soft dark:bg-surface-soft-dark text-text dark:text-text-dark rounded-lg px-4"
        style={{ minHeight: 48 }}
      />
    </View>
  );
}
