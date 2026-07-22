import React, { useState } from "react";
import { View, Text, TouchableOpacity, ActivityIndicator } from "react-native";
import { useRouter } from "expo-router";
import { useTranslation } from "react-i18next";
import { ThermometerSun } from "lucide-react-native";
import { reportSick } from "../../services/schedule";
import { useColors } from "../../hooks/useColors";
import { useIsOffline } from "../../hooks/useIsOffline";

/**
 * FR-005/SC-002: "Ik ben ziek" — one tap plus a confirmation step, not a form (spec.md Main
 * flow). The confirmation step is a deliberate exception to the tablet's "one tap" speed
 * principle, precisely because of this action's urgency and irreversibility-by-a-normal-user
 * (spec.md Accessibility — a mis-tap shouldn't silently report a healthy staff member sick).
 * Requires connectivity (spec.md Offline behavior) — no offline queue.
 */
export default function ReportSickScreen() {
  const { t } = useTranslation();
  const colors = useColors();
  const router = useRouter();
  const isOffline = useIsOffline();

  const [confirming, setConfirming] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState<"success" | "error" | null>(null);

  async function confirm() {
    setSubmitting(true);
    const outcome = await reportSick();
    setSubmitting(false);
    setResult(outcome.succeeded ? "success" : "error");
  }

  if (result === "success") {
    return (
      <View style={{ flex: 1, backgroundColor: colors.background, alignItems: "center", justifyContent: "center", padding: 24 }}>
        <ThermometerSun color={colors.danger} size={40} strokeWidth={2} />
        <Text className="text-text dark:text-text-dark text-lg font-semibold text-center mt-4">
          {t("reportSick.success")}
        </Text>
        <TouchableOpacity
          onPress={() => router.replace("/(app)")}
          className="bg-primary dark:bg-primary-dark rounded-lg py-4 px-6 items-center mt-6"
          style={{ minHeight: 48 }}
        >
          <Text className="text-white text-base font-bold">{t("common.ok")}</Text>
        </TouchableOpacity>
      </View>
    );
  }

  return (
    <View style={{ flex: 1, backgroundColor: colors.background, alignItems: "center", justifyContent: "center", padding: 24 }}>
      <ThermometerSun color={colors.danger} size={40} strokeWidth={2} />
      <Text className="text-text dark:text-text-dark text-xl font-bold text-center mt-4 mb-2">
        {t("reportSick.title")}
      </Text>
      <Text className="text-text-soft dark:text-text-soft-dark text-sm text-center mb-6">
        {t("reportSick.description")}
      </Text>

      {isOffline && (
        <Text className="text-danger dark:text-danger-dark text-sm text-center mb-4">{t("reportSick.needsConnection")}</Text>
      )}
      {result === "error" && (
        <Text className="text-danger dark:text-danger-dark text-sm text-center mb-4">{t("reportSick.genericError")}</Text>
      )}

      {!confirming ? (
        <TouchableOpacity
          onPress={() => setConfirming(true)}
          disabled={isOffline}
          className="bg-danger dark:bg-danger-dark rounded-lg py-4 px-8 items-center"
          style={{ minHeight: 48, opacity: isOffline ? 0.5 : 1 }}
          testID="report-sick-cta"
        >
          <Text className="text-white text-base font-bold">{t("reportSick.action")}</Text>
        </TouchableOpacity>
      ) : (
        <View className="w-full">
          <Text className="text-text dark:text-text-dark text-base font-medium text-center mb-4">
            {t("reportSick.confirmPrompt")}
          </Text>
          <View className="flex-row gap-3">
            <TouchableOpacity
              onPress={() => setConfirming(false)}
              disabled={submitting}
              className="flex-1 border border-border dark:border-border-dark rounded-lg py-4 items-center"
              style={{ minHeight: 48 }}
            >
              <Text className="text-text dark:text-text-dark text-base font-medium">{t("common.cancel")}</Text>
            </TouchableOpacity>
            <TouchableOpacity
              onPress={confirm}
              disabled={submitting}
              className="flex-1 bg-danger dark:bg-danger-dark rounded-lg py-4 items-center"
              style={{ minHeight: 48 }}
              testID="report-sick-confirm"
            >
              {submitting ? <ActivityIndicator color="#fff" /> : <Text className="text-white text-base font-bold">{t("reportSick.confirm")}</Text>}
            </TouchableOpacity>
          </View>
        </View>
      )}
    </View>
  );
}
