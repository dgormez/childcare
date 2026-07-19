import React, { useEffect, useState } from "react";
import { View, Text, TextInput, TouchableOpacity, ScrollView, ActivityIndicator, Switch } from "react-native";
import { useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import { apiClient } from "../services/apiClient";
import { submitDayReservation, submitBulkDayReservation } from "../services/dayReservations";
import { getReservationAvailability } from "../services/locations";
import { useColors } from "../hooks/useColors";
import { DateField } from "./DateField";
import type { BulkDayReservationResultItem, DayReservationType, ParentChildResponse } from "../types";

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
  // FR-006/research.md R6: the authoritative per-child check, once a child is selected — the
  // home screen's hiding is only a heuristic across all children, this is the real gate. The
  // server still re-enforces at submission regardless (FR-007) — this is a UX nicety only.
  const [disabledForChild, setDisabledForChild] = useState(false);
  // Feature 030 US1 — only offered once 2+ active children are linked (spec.md FR-001).
  const [applyToAllChildren, setApplyToAllChildren] = useState(false);
  const [bulkResults, setBulkResults] = useState<BulkDayReservationResultItem[] | null>(null);

  useEffect(() => {
    apiClient.GET("/api/parent/children").then((result) => {
      if (result.response.ok) {
        const fetched = result.data as unknown as ParentChildResponse[];
        setChildren(fetched);
        if (fetched.length > 0) setChildId((current) => current ?? fetched[0].id);
      }
    }).catch(() => {});
  }, []);

  useEffect(() => {
    if (!childId) {
      setDisabledForChild(false);
      return;
    }
    let cancelled = false;
    getReservationAvailability(childId).then((availability) => {
      if (cancelled || !availability) return;
      setDisabledForChild(availability[type] === "disabled");
    });
    return () => {
      cancelled = true;
    };
  }, [childId, type]);

  const selectedChild = children.find((child) => child.id === childId) ?? null;

  const canSubmit =
    (applyToAllChildren || (!!childId && !disabledForChild)) &&
    requestedDate.length > 0 &&
    (type !== "exchange" || exchangeForDate.length > 0) &&
    !submitting;

  const handleSubmit = async () => {
    if (!applyToAllChildren && !childId) return;
    setError("");
    setBulkResults(null);
    setSubmitting(true);
    try {
      if (applyToAllChildren) {
        const bulk = await submitBulkDayReservation(
          children.map((child) => child.id), type, requestedDate, type === "exchange" ? exchangeForDate : null, reason.trim() || null);
        if (bulk.results.every((result) => result.succeeded)) {
          router.replace("/(app)/requests");
        } else {
          setBulkResults(bulk.results);
        }
      } else {
        await submitDayReservation(childId!, type, requestedDate, type === "exchange" ? exchangeForDate : null, reason.trim() || null);
        router.replace("/(app)/requests");
      }
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

      {disabledForChild && selectedChild && !applyToAllChildren && (
        <Text className="text-danger dark:text-danger-dark text-sm mb-4">
          {t("dayReservations.notAvailableForChild", { childName: selectedChild.firstName })}
        </Text>
      )}

      {children.length > 1 && (
        <TouchableOpacity
          onPress={() => setApplyToAllChildren((current) => !current)}
          className="flex-row items-center justify-between bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-4 mb-4"
          style={{ minHeight: 48 }}
        >
          <Text className="text-text dark:text-text-dark flex-1 mr-3">{t("dayReservations.applyToAllChildren")}</Text>
          <Switch value={applyToAllChildren} onValueChange={setApplyToAllChildren} />
        </TouchableOpacity>
      )}

      {!applyToAllChildren && (
        <>
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
        </>
      )}

      {bulkResults && (
        <View className="mb-4">
          <Text className="text-text dark:text-text-dark text-sm font-medium mb-2">
            {t("dayReservations.bulkPartialResult", {
              succeededCount: bulkResults.filter((r) => r.succeeded).length,
              totalCount: bulkResults.length,
            })}
          </Text>
          {bulkResults.map((result) => (
            <View key={result.childId} className="flex-row items-center justify-between rounded-lg bg-surface-soft dark:bg-surface-soft-dark px-4 mb-2" style={{ minHeight: 48 }}>
              <Text className="text-text dark:text-text-dark flex-1 mr-3">{result.childName}</Text>
              <Text className={result.succeeded ? "text-success dark:text-success-dark text-sm" : "text-danger dark:text-danger-dark text-sm"}>
                {result.succeeded ? t("dayReservations.status.pending") : t(result.errorKey!, { defaultValue: t("dayReservations.submitFailed") })}
              </Text>
            </View>
          ))}
        </View>
      )}

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
