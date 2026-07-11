import React from "react";
import { View, Text, Image, ScrollView } from "react-native";
import { useTranslation } from "react-i18next";
import { Trees, Palette, Music, BookOpen, PartyPopper, Ellipsis, Clock } from "lucide-react-native";
import { useColors } from "../hooks/useColors";
import type { GroupActivityResponse, GroupActivityType, GroupTimelineEntryResponse } from "../types";

const TYPE_ICONS: Record<GroupActivityType, typeof Trees> = {
  outdoor: Trees,
  creative: Palette,
  music: Music,
  story: BookOpen,
  celebration: PartyPopper,
  other: Ellipsis,
};

interface Props {
  entries: GroupTimelineEntryResponse[];
  /** Local-only pending-photo count for an activity still uploading (photoUploadQueue.ts) — a
   * server-fetched activity has none, since by the time it's fetched its photos are already
   * whatever the server has. */
  getPendingPhotoCount?: (activityId: string) => number;
}

function formatTime(isoDate: string): string {
  return new Date(isoDate).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

// design-system.md: every badge pairs its semantic color with an icon, never color alone —
// warning's fixed pairing is a clock.
function PendingBadge({ label }: { label: string }) {
  const colors = useColors();
  return (
    <View className="self-start flex-row items-center bg-warning dark:bg-warning-dark rounded-full px-2 py-0.5 mt-1" style={{ gap: 4 }}>
      <Clock size={12} strokeWidth={2} color={colors.warningFg} />
      <Text className="text-xs font-medium text-warning-fg">{label}</Text>
    </View>
  );
}

function GroupActivityRow({ activity, pendingPhotoCount }: { activity: GroupActivityResponse; pendingPhotoCount: number }) {
  const { t } = useTranslation();
  const colors = useColors();
  const Icon = TYPE_ICONS[activity.activityType];

  return (
    <View className="bg-surface-soft dark:bg-surface-soft-dark rounded-xl p-3 mb-2">
      <View className="flex-row items-start" style={{ gap: 8 }}>
        <View style={{ width: 32, height: 32 }} className="items-center justify-center rounded-full bg-primary-soft dark:bg-primary-soft-dark">
          <Icon size={16} strokeWidth={2} color={colors.primary} />
        </View>
        <View style={{ flex: 1 }}>
          <Text className="text-text dark:text-text-dark font-semibold">{activity.title}</Text>
          {!!activity.description && (
            <Text className="text-text-soft dark:text-text-soft-dark text-sm mt-1">{activity.description}</Text>
          )}
          <Text className="text-text-soft dark:text-text-soft-dark text-xs mt-1" style={{ fontVariant: ["tabular-nums"] }}>
            {formatTime(activity.occurredAt)}
          </Text>
          {pendingPhotoCount > 0 && <PendingBadge label={t("groupActivities.photosUploading")} />}
        </View>
      </View>

      {activity.photos.length > 0 && (
        <ScrollView horizontal showsHorizontalScrollIndicator={false} style={{ marginTop: 8 }}>
          <View className="flex-row" style={{ gap: 8 }}>
            {activity.photos.map((photo) => (
              <Image
                key={photo.id}
                source={{ uri: photo.thumbnailDownloadUrl ?? undefined }}
                style={{ width: 56, height: 56, borderRadius: 8, backgroundColor: colors.border }}
              />
            ))}
          </View>
        </ScrollView>
      )}
    </View>
  );
}

function ChildEventRow({ event }: { event: NonNullable<GroupTimelineEntryResponse["childEvent"]> }) {
  const { t } = useTranslation();
  const isCustom = event.eventType === "custom";
  const customLabel = isCustom && typeof event.payload.label === "string" ? event.payload.label : null;

  return (
    <View className="flex-row items-start bg-surface-soft dark:bg-surface-soft-dark rounded-xl p-3 mb-2" style={{ gap: 8 }}>
      <View style={{ flex: 1 }}>
        <Text className="text-text dark:text-text-dark font-semibold">
          {customLabel ?? t(`childEvents.types.${event.eventType}`)}
        </Text>
        <Text className="text-text-soft dark:text-text-soft-dark text-xs mt-1" style={{ fontVariant: ["tabular-nums"] }}>
          {formatTime(event.occurredAt)}
        </Text>
      </View>
    </View>
  );
}

/**
 * Merged group/date timeline (feature 009b, contracts/group-activities-api.md) — interleaves
 * ChildEvent and GroupActivity entries chronologically (server-ordered; this component just
 * renders in the order given). Read-only: no edit/delete affordances here, unlike per-child
 * EventTimeline.tsx — this view has no editing capability by design (spec.md FR-014, director
 * deletion only, reachable from web).
 */
export function GroupTimeline({ entries, getPendingPhotoCount }: Props) {
  const { t } = useTranslation();

  if (entries.length === 0) {
    return (
      <View style={{ paddingVertical: 32, alignItems: "center" }}>
        <Text className="text-text-soft dark:text-text-soft-dark">{t("groupActivities.timelineEmpty")}</Text>
      </View>
    );
  }

  return (
    <View>
      {entries.map((entry) =>
        entry.kind === "group_activity" && entry.groupActivity ? (
          <GroupActivityRow
            key={`activity-${entry.groupActivity.id}`}
            activity={entry.groupActivity}
            pendingPhotoCount={getPendingPhotoCount?.(entry.groupActivity.id) ?? 0}
          />
        ) : entry.childEvent ? (
          <ChildEventRow key={`event-${entry.childEvent.id}`} event={entry.childEvent} />
        ) : null,
      )}
    </View>
  );
}
