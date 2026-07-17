import React from "react";
import { View, Text } from "react-native";
import { useTranslation } from "react-i18next";
import { CheckCircle2, Sparkles, Circle, Sprout } from "lucide-react-native";
import { useColors } from "../../hooks/useColors";
import type { MilestoneObservationStatus } from "../../types";

export interface MilestoneTimelineEntry {
  observationId: string;
  milestoneDescription: string;
  status: MilestoneObservationStatus;
  observedAt: string;
  createdAt: string;
  /** Recorded while offline and not yet synced — mirrors EventTimeline's "pending" sync status. */
  pending?: boolean;
}

interface Props {
  entries: MilestoneTimelineEntry[];
}

// Never color alone (design-system.md's Status Indicators section) — each status gets a
// distinct icon as well as a distinct color, so the difference survives even at a glance.
function StatusIcon({ status, colors }: { status: MilestoneObservationStatus; colors: ReturnType<typeof useColors> }) {
  if (status === "achieved") return <CheckCircle2 size={16} strokeWidth={2} color={colors.success} />;
  if (status === "emerging") return <Sparkles size={16} strokeWidth={2} color={colors.textSoft} />;
  return <Circle size={16} strokeWidth={2} color={colors.textSoft} />;
}

/**
 * Recent milestone observations for this child, most recent first — read-only (observations are
 * append-only, research.md R3), so there is no edit/delete affordance here, unlike EventTimeline.
 */
export function MilestoneTimeline({ entries }: Props) {
  const { t } = useTranslation();
  const colors = useColors();

  if (entries.length === 0) {
    return (
      <View style={{ paddingVertical: 32, alignItems: "center" }}>
        <Sprout color={colors.textSoft} size={24} strokeWidth={2} />
        <Text className="text-text-soft dark:text-text-soft-dark mt-2">{t("milestones.empty")}</Text>
      </View>
    );
  }

  return (
    <View>
      {entries.map((entry) => (
        <View key={entry.observationId} className="bg-surface-soft dark:bg-surface-soft-dark rounded-xl p-3 mb-2">
          <Text className="text-text dark:text-text-dark font-semibold">{entry.milestoneDescription}</Text>
          <View className="flex-row items-center mt-1" style={{ gap: 4 }}>
            <StatusIcon status={entry.status} colors={colors} />
            <Text className="text-text-soft dark:text-text-soft-dark text-sm">{t(`milestones.status.${entry.status}`)}</Text>
          </View>
          <Text className="text-text-soft dark:text-text-soft-dark text-xs mt-1" style={{ fontVariant: ["tabular-nums"] }}>
            {entry.observedAt}
          </Text>
          {entry.pending && (
            <View className="self-start rounded-full px-2 py-0.5 mt-2 bg-warning dark:bg-warning-dark">
              <Text className="text-xs font-medium text-warning-fg">{t("milestones.pendingSync")}</Text>
            </View>
          )}
        </View>
      ))}
    </View>
  );
}
