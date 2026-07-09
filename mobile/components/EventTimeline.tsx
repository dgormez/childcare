import React from "react";
import { View, Text, TouchableOpacity } from "react-native";
import { useTranslation } from "react-i18next";
import type { ChildEventResponse } from "../types";

export type EventSyncStatus = "synced" | "pending" | "needs_review";

interface Props {
  events: ChildEventResponse[];
  /** Keyed by event id — omitted/absent means "synced" (the common case for server-fetched
   * events). Populated by the caller from the offline queue for locally-created/queued rows. */
  syncStatusByEventId?: Record<string, EventSyncStatus>;
  onEdit: (event: ChildEventResponse) => void;
  onDelete: (event: ChildEventResponse) => void;
}

function isToday(isoDate: string): boolean {
  const d = new Date(isoDate);
  const now = new Date();
  return d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth() && d.getDate() === now.getDate();
}

function formatTime(isoDate: string): string {
  return new Date(isoDate).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

function Badge({ tone, label }: { tone: "info" | "warning" | "danger"; label: string }) {
  const toneClasses: Record<typeof tone, string> = {
    info: "bg-info dark:bg-info-dark",
    warning: "bg-warning dark:bg-warning-dark",
    danger: "bg-danger-bg dark:bg-danger-bg-dark",
  };
  const textClasses: Record<typeof tone, string> = {
    info: "text-white",
    warning: "text-warning-fg", // fixed dark text on the amber fill, same value both themes
    danger: "text-danger dark:text-danger-dark",
  };
  return (
    <View className={`self-start rounded-full px-2 py-0.5 mt-1 ${toneClasses[tone]}`}>
      <Text className={`text-xs font-medium ${textClasses[tone]}`}>{label}</Text>
    </View>
  );
}

/**
 * Chronological per-child event list. Edit/delete affordances only render for same-day events —
 * this mobile app is always device-token authenticated once paired (feature 008a), so there is
 * no "director session" here to exempt from that window; a director's any-day correction
 * capability (FR-007) is reachable via the API but has no UI in this app (spec.md Assumptions).
 */
export function EventTimeline({ events, syncStatusByEventId = {}, onEdit, onDelete }: Props) {
  const { t } = useTranslation();

  if (events.length === 0) {
    return (
      <View style={{ paddingVertical: 32, alignItems: "center" }}>
        <Text className="text-text-soft dark:text-text-soft-dark">{t("childEvents.empty")}</Text>
      </View>
    );
  }

  return (
    <View>
      {events.map((event) => {
        const editable = isToday(event.occurredAt);
        const syncStatus = syncStatusByEventId[event.id] ?? "synced";
        const inProgress = event.eventType === "sleep" && !event.endedAt;
        // FR-004: a `custom` event shows its caregiver-supplied label as the headline (in place
        // of the generic type name every other type uses), with `text` as secondary detail.
        const isCustom = event.eventType === "custom";
        const customLabel = isCustom && typeof event.payload.label === "string" ? event.payload.label : null;
        const customText = isCustom && typeof event.payload.text === "string" ? event.payload.text : null;

        return (
          <View
            key={event.id}
            className="flex-row items-start justify-between bg-surface-soft dark:bg-surface-soft-dark rounded-xl p-3 mb-2"
          >
            <View style={{ flex: 1 }}>
              <Text className="text-text dark:text-text-dark font-semibold">
                {customLabel ?? t(`childEvents.types.${event.eventType}`)}
              </Text>
              {customText && (
                <Text className="text-text-soft dark:text-text-soft-dark text-sm mt-1">{customText}</Text>
              )}
              <Text className="text-text-soft dark:text-text-soft-dark text-xs" style={{ fontVariant: ["tabular-nums"] }}>
                {formatTime(event.occurredAt)}
              </Text>
              {inProgress && <Badge tone="info" label={t("childEvents.inProgress")} />}
              {syncStatus === "pending" && <Badge tone="warning" label={t("childEvents.pendingSync")} />}
              {syncStatus === "needs_review" && <Badge tone="danger" label={t("childEvents.needsReview")} />}
            </View>

            {editable && (
              <View className="flex-row" style={{ gap: 4 }}>
                <TouchableOpacity
                  onPress={() => onEdit(event)}
                  style={{ minHeight: 48, minWidth: 48 }}
                  className="items-center justify-center active:opacity-60"
                >
                  <Text className="text-primary-hover dark:text-primary-hover-dark font-medium text-sm">{t("childEvents.edit")}</Text>
                </TouchableOpacity>
                <TouchableOpacity
                  onPress={() => onDelete(event)}
                  style={{ minHeight: 48, minWidth: 48 }}
                  className="items-center justify-center active:opacity-60"
                >
                  <Text className="text-danger dark:text-danger-dark font-medium text-sm">{t("childEvents.delete")}</Text>
                </TouchableOpacity>
              </View>
            )}
          </View>
        );
      })}
    </View>
  );
}
