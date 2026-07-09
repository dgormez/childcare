import React from "react";
import { View, Text } from "react-native";
import { useTranslation } from "react-i18next";
import { CheckCircle2, AlertTriangle, AlertOctagon } from "lucide-react-native";
import { useColors } from "../hooks/useColors";
import type { BkrRatioResponse } from "../types";

interface Props {
  bkr: BkrRatioResponse;
}

/**
 * FR-007/FR-008: live BKR (begeleider-kind-ratio) status — a colour-coded pill, always paired
 * with a text label and icon (never colour alone, per design-system.md's accessibility rule).
 * Display/warning only (FR-009) — never blocks check-in.
 */
export function BkrIndicator({ bkr }: Props) {
  const { t } = useTranslation();
  const colors = useColors();

  const config = {
    green: { bg: "bg-success-bg dark:bg-success-bg-dark", fg: "text-success dark:text-success-dark", Icon: CheckCircle2, color: colors.success },
    amber: { bg: "bg-warning dark:bg-warning-dark", fg: "text-warning-fg", Icon: AlertTriangle, color: colors.warningFg },
    red: { bg: "bg-danger-bg dark:bg-danger-bg-dark", fg: "text-danger dark:text-danger-dark", Icon: AlertOctagon, color: colors.danger },
  }[bkr.status];

  const label = bkr.isNapTime ? t("attendance.bkr.labelNapTime") : t("attendance.bkr.label");

  return (
    <View
      accessibilityLabel={t(`attendance.bkr.status.${bkr.status}`)}
      className={`flex-row items-center rounded-full px-3 py-2 ${config.bg}`}
      style={{ alignSelf: "flex-start" }}
    >
      <config.Icon size={16} strokeWidth={2} color={config.color} />
      <Text className={`text-xs font-semibold ml-1 ${config.fg}`}>
        {label}: {bkr.presentCount}/{bkr.threshold}
      </Text>
    </View>
  );
}
