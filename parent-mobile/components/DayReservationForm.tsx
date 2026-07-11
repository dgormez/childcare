import React, { useEffect, useState } from "react";
import { View, Text, TextInput, TouchableOpacity, ScrollView, ActivityIndicator } from "react-native";
import { useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import { apiClient } from "../services/apiClient";
import { submitDayReservation } from "../services/dayReservations";
import { useColors } from "../hooks/useColors";
import { DateField } from "./DateField";
import type { DayReservationType, ParentChildResponse } from "../types";

interface DayReservationFormProps {
  type: DayReservationType;
  titleKey: string;
}

/** Shared form for all three entry points (FR-018) — a single component parameterized by
 * `type` rather than triplicating the date-picker + reason-field form (plan.md's Structure
 * Decision). */
export function DayReservationForm({ type, titleKey }: DayReservationFormProps) {
  const { t } = useTranslation();
  const colors = useColors();
  const router = useRouter();

  const [children, setChildren] = useState<ParentChildResponse[]>([]);
  const [childId, setChildId] = useState<string | null>(null);
  const [requestedDate, setRequestedDate] = useState("");
  const [exchangeForDate, setExchangeForDate] = useState("");
  const [reason, setReason] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    apiClient.GET("/api/parent/children").then((result) => {
      if (result.response.ok) {
        const fetched = result.data as unknown as ParentChildResponse[];
        setChildren(fetched);
        if (fetched.length > 0) setChildId((current) => current ?? fetched[0].id);
      }
    }).catch(() => {});
  }, []);

  const canSubmit = !!childId && requestedDate.length > 0 && (type !== "exchange" || exchangeForDate.length > 0) && !submitting;

  const handleSubmit = async () => {
    if (!childId) return;
    setError("");
    setSubmitting(true);
    try {
      await submitDayReservation(childId, type, requestedDate, type === "exchange" ? exchangeForDate : null, reason.trim() || null);
      router.replace("/(app)/requests");
    } catch (e) {
      const errorKey = (e as Error).message;
      setError(t(errorKey, { defaultValue: t("dayReservations.submitFailed") }));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <ScrollView className="flex-1 bg-background dark:bg-background-dark" contentContainerStyle={{ padding: 16 }}>
      <Text className="text-text dark:text-text-dark text-xl font-bold mb-4">{t(titleKey)}</Text>

      {!!error && <Text className="text-danger dark:text-danger-dark text-sm mb-4">{error}</Text>}

      <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium mb-1">{t("dayReservations.childLabel")}</Text>
      <View className="mb-4">
        {children.map((child) => (
          <TouchableOpacity
            key={child.id}
            onPress={() => setChildId(child.id)}
            className={`rounded-lg px-4 mb-2 ${childId === child.id ? "bg-primary-soft dark:bg-primary-soft-dark" : "bg-surface-soft dark:bg-surface-soft-dark"}`}
            style={{ minHeight: 48, justifyContent: "center" }}
          >
            <Text className="text-text dark:text-text-dark">{child.firstName} {child.lastName}</Text>
          </TouchableOpacity>
        ))}
      </View>

      {type === "exchange" && (
        <DateField label={t("dayReservations.exchangeForDateLabel")} value={exchangeForDate} onChange={setExchangeForDate} />
      )}

      <DateField label={t("dayReservations.dateLabel")} value={requestedDate} onChange={setRequestedDate} />

      <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium mb-1">{t("dayReservations.reasonLabel")}</Text>
      <TextInput
        value={reason}
        onChangeText={setReason}
        placeholder={t("dayReservations.reasonPlaceholder")}
        placeholderTextColor={colors.placeholder}
        multiline
        className="bg-surface-soft dark:bg-surface-soft-dark text-text dark:text-text-dark rounded-lg px-4 py-4 mb-6"
        style={{ minHeight: 96, textAlignVertical: "top" }}
      />

      <TouchableOpacity
        onPress={handleSubmit}
        disabled={!canSubmit}
        className={`rounded-lg py-4 items-center ${canSubmit ? "bg-primary dark:bg-primary-dark" : "bg-border dark:bg-border-dark"}`}
        style={{ minHeight: 48 }}
      >
        {submitting ? <ActivityIndicator color="#fff" /> : <Text className="text-white text-lg font-bold">{t("dayReservations.submit")}</Text>}
      </TouchableOpacity>
    </ScrollView>
  );
}
