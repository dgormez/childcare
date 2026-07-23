import React, { useState } from "react";
import { View, Text, TextInput, TouchableOpacity, ScrollView, KeyboardAvoidingView, ActivityIndicator } from "react-native";
import { useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import { submitLeaveRequest } from "../../../services/leaveRequests";
import { DateField } from "../../../components/DateField";
import { useColors } from "../../../hooks/useColors";
import { useIsOffline } from "../../../hooks/useIsOffline";
import type { StaffLeaveRequestType } from "../../../types";

function toDateString(d: Date): string {
  const year = d.getFullYear();
  const month = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

const TYPES: StaffLeaveRequestType[] = ["annual", "sick", "other"];

/** FR-009: a short form since planned leave is lower-urgency than sick reporting (spec.md Main
 * flow) — type, date range, optional note. Requires connectivity, same as report-sick. */
export default function NewLeaveRequestScreen() {
  const { t } = useTranslation();
  const colors = useColors();
  const router = useRouter();
  const isOffline = useIsOffline();

  const today = toDateString(new Date());
  const [type, setType] = useState<StaffLeaveRequestType>("annual");
  const [dateFrom, setDateFrom] = useState(today);
  const [dateTo, setDateTo] = useState(today);
  const [notes, setNotes] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  const canSubmit = dateFrom.length > 0 && dateTo.length > 0 && dateTo >= dateFrom && !isOffline && !submitting;

  async function submit() {
    setError("");
    setSubmitting(true);
    const result = await submitLeaveRequest(type, dateFrom, dateTo, notes.trim() || null);
    setSubmitting(false);
    if (!result.succeeded) {
      setError(t("leaveRequests.submitFailed"));
      return;
    }
    router.back();
  }

  return (
    <KeyboardAvoidingView behavior="padding" className="flex-1 bg-background dark:bg-background-dark">
      <ScrollView contentContainerStyle={{ padding: 16 }} keyboardShouldPersistTaps="handled">
        <Text className="text-text dark:text-text-dark text-sm font-medium mb-2">{t("leaveRequests.typeLabel")}</Text>
        <View className="flex-row gap-2 mb-4">
          {TYPES.map((option) => (
            <TouchableOpacity
              key={option}
              onPress={() => setType(option)}
              className={`flex-1 rounded-lg py-3 items-center ${type === option ? "bg-primary dark:bg-primary-dark" : "bg-surface-soft dark:bg-surface-soft-dark"}`}
              style={{ minHeight: 48, justifyContent: "center" }}
              testID={`leave-type-${option}`}
            >
              <Text className={type === option ? "text-white text-sm font-semibold" : "text-text dark:text-text-dark text-sm"}>
                {t(`leaveRequests.type.${option}`)}
              </Text>
            </TouchableOpacity>
          ))}
        </View>

        <DateField label={t("leaveRequests.dateFromLabel")} value={dateFrom} onChange={setDateFrom} />
        <DateField label={t("leaveRequests.dateToLabel")} value={dateTo} onChange={setDateTo} />

        <Text className="text-text-soft dark:text-text-soft-dark text-sm font-medium mb-1">{t("leaveRequests.notesLabel")}</Text>
        <TextInput
          value={notes}
          onChangeText={setNotes}
          placeholder={t("leaveRequests.notesPlaceholder")}
          placeholderTextColor={colors.placeholder}
          multiline
          numberOfLines={3}
          className="bg-surface-soft dark:bg-surface-soft-dark text-text dark:text-text-dark rounded-lg px-4 py-3 mb-4"
          style={{ minHeight: 88, textAlignVertical: "top" }}
        />

        {isOffline && <Text className="text-danger dark:text-danger-dark text-sm mb-4">{t("leaveRequests.needsConnection")}</Text>}
        {!!error && <Text className="text-danger dark:text-danger-dark text-sm mb-4">{error}</Text>}

        <TouchableOpacity
          onPress={submit}
          disabled={!canSubmit}
          className={`rounded-lg py-4 items-center ${canSubmit ? "bg-primary dark:bg-primary-dark" : "bg-border dark:bg-border-dark"}`}
          style={{ minHeight: 48 }}
          testID="submit-leave-request"
        >
          {submitting ? <ActivityIndicator color="#fff" /> : <Text className="text-white text-base font-bold">{t("leaveRequests.submit")}</Text>}
        </TouchableOpacity>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}
