import React, { useCallback, useEffect, useState } from "react";
import { View, Text, TouchableOpacity } from "react-native";
import { useTranslation } from "react-i18next";
import { Clock } from "lucide-react-native";
import { clockIn, clockOut, type StaffTimeEntry } from "../services/timeEntries";
import { apiClient } from "../services/apiClient";
import { useIsOffline } from "../hooks/useIsOffline";

interface ClockInOutCardProps {
  eligibleLocationIds: string[];
  timeEntryFunctions: string[];
  locationNamesById: Map<string, string>;
}

type Step = "idle" | "pickLocation" | "pickFunction" | "submitting";

/**
 * FR-001/FR-002/FR-005: one-tap clock in/out, the single highest-frequency action on this
 * surface (platform-rules.md) — mounted at the top of the schedule screen (research.md R10).
 * Location/function pickers only appear when genuinely ambiguous (more than one eligible
 * location / configured function — spec.md Assumptions, mirrors FR-005's picker rule).
 */
export function ClockInOutCard({ eligibleLocationIds, timeEntryFunctions, locationNamesById }: ClockInOutCardProps) {
  const { t } = useTranslation();
  const isOffline = useIsOffline();

  const [openEntry, setOpenEntry] = useState<StaffTimeEntry | null | undefined>(undefined);
  const [step, setStep] = useState<Step>("idle");
  const [pendingLocationId, setPendingLocationId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadCurrent = useCallback(async () => {
    const result = await apiClient.GET("/api/staff-time-entries/me/current");
    if (result.response.ok) {
      setOpenEntry((result.data as unknown as StaffTimeEntry | null) ?? null);
    }
  }, []);

  useEffect(() => {
    loadCurrent();
  }, [loadCurrent]);

  async function submitClockIn(locationId: string, staffFunction: string | null) {
    setStep("submitting");
    const result = await clockIn(locationId, null, staffFunction);
    setStep("idle");
    setPendingLocationId(null);
    if (result.succeeded) {
      setOpenEntry(result.entry);
    } else {
      setError(t("timeEntries.clockInError"));
    }
  }

  // Proceeds from an already-known location — either the caller's single eligible one, or one
  // just picked from the location step — deciding next whether a function is ambiguous too.
  async function proceedFromLocation(locationId: string) {
    if (timeEntryFunctions.length > 1) {
      setPendingLocationId(locationId);
      setStep("pickFunction");
      return;
    }
    await submitClockIn(locationId, null);
  }

  async function handleClockInPress() {
    setError(null);
    if (eligibleLocationIds.length === 0) return;

    if (eligibleLocationIds.length > 1) {
      setStep("pickLocation");
      return;
    }

    await proceedFromLocation(eligibleLocationIds[0]);
  }

  async function handleClockOut() {
    setError(null);
    setStep("submitting");
    const result = await clockOut();
    setStep("idle");
    if (result.succeeded) {
      setOpenEntry(null);
    } else {
      setError(t("timeEntries.clockOutError"));
    }
  }

  if (openEntry === undefined || eligibleLocationIds.length === 0) return null;

  const disabled = isOffline || step === "submitting";

  return (
    <View className="bg-surface dark:bg-surface-dark rounded-xl px-4 py-4 mb-4">
      {isOffline && (
        <Text className="text-danger dark:text-danger-dark text-sm text-center mb-3">{t("timeEntries.needsConnection")}</Text>
      )}
      {error && <Text className="text-danger dark:text-danger-dark text-sm text-center mb-3">{error}</Text>}

      {step === "pickLocation" && (
        <View>
          <Text className="text-text dark:text-text-dark text-base font-medium mb-3">{t("timeEntries.pickLocation")}</Text>
          {eligibleLocationIds.map((locationId) => (
            <TouchableOpacity
              key={locationId}
              onPress={() => proceedFromLocation(locationId)}
              className="border border-border dark:border-border-dark rounded-lg py-3 px-4 mb-2"
              style={{ minHeight: 48, justifyContent: "center" }}
            >
              <Text className="text-text dark:text-text-dark text-base">{locationNamesById.get(locationId) ?? locationId}</Text>
            </TouchableOpacity>
          ))}
        </View>
      )}

      {step === "pickFunction" && (
        <View>
          <Text className="text-text dark:text-text-dark text-base font-medium mb-3">{t("timeEntries.pickFunction")}</Text>
          {timeEntryFunctions.map((fn) => (
            <TouchableOpacity
              key={fn}
              onPress={() => submitClockIn(pendingLocationId!, fn)}
              className="border border-border dark:border-border-dark rounded-lg py-3 px-4 mb-2"
              style={{ minHeight: 48, justifyContent: "center" }}
            >
              <Text className="text-text dark:text-text-dark text-base">{t(`timeEntries.functions.${fn}`)}</Text>
            </TouchableOpacity>
          ))}
        </View>
      )}

      {(step === "idle" || step === "submitting") && (
        <TouchableOpacity
          onPress={openEntry ? handleClockOut : handleClockInPress}
          disabled={disabled}
          className={
            openEntry
              ? "bg-danger dark:bg-danger-dark rounded-lg py-4 items-center flex-row justify-center gap-2"
              : "bg-primary dark:bg-primary-dark rounded-lg py-4 items-center flex-row justify-center gap-2"
          }
          style={{ minHeight: 64, opacity: disabled ? 0.5 : 1 }}
          testID="clock-in-out-cta"
        >
          <Clock color="white" size={20} strokeWidth={2} />
          <Text className="text-white text-base font-bold">
            {openEntry ? t("timeEntries.clockOutAction") : t("timeEntries.clockInAction")}
          </Text>
        </TouchableOpacity>
      )}
    </View>
  );
}
