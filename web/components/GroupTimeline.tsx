"use client";
import { useTranslations } from "next-intl";
import { Trees, Palette, Music, BookOpen, PartyPopper, Ellipsis } from "lucide-react";
import { Button } from "./ui/button";
import type { GroupActivityResponse, GroupActivityType, GroupTimelineEntryResponse } from "../lib/types";

interface GroupTimelineProps {
  entries: GroupTimelineEntryResponse[];
  onDeleteActivity: (activity: GroupActivityResponse) => void;
}

const TYPE_ICONS: Record<GroupActivityType, typeof Trees> = {
  outdoor: Trees,
  creative: Palette,
  music: Music,
  story: BookOpen,
  celebration: PartyPopper,
  other: Ellipsis,
};

function formatTime(value: string): string {
  return new Date(value).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

/**
 * Merged group/date timeline (feature 009b, contracts/group-activities-api.md). A plain row
 * list, not a <table> — a group activity's shape (title/description/photos) doesn't share
 * columns with a child event, unlike AttendanceTable.tsx's uniform rows. The delete action is an
 * always-visible inline button (007a's "avoid hidden actions" precedent), not a hidden menu —
 * Button already carries a visible focus ring (focus-visible:ring-2), so no extra work is
 * needed for keyboard reachability here.
 */
export function GroupTimeline({ entries, onDeleteActivity }: GroupTimelineProps) {
  const t = useTranslations("groups");

  return (
    <ul className="space-y-2">
      {entries.map((entry) => {
        if (entry.kind === "group_activity" && entry.groupActivity) {
          const activity = entry.groupActivity;
          const Icon = TYPE_ICONS[activity.activityType];
          return (
            <li
              key={`activity-${activity.id}`}
              className="flex items-start justify-between gap-4 rounded-xl bg-surface-soft p-4 dark:bg-surface-soft-dark"
            >
              <div className="flex items-start gap-3">
                <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary-soft dark:bg-primary-soft-dark">
                  <Icon className="h-4 w-4 text-primary-hover dark:text-primary-hover-dark" strokeWidth={2} />
                </div>
                <div>
                  <p className="font-medium text-text dark:text-text-dark">{activity.title}</p>
                  {!!activity.description && (
                    <p className="mt-1 text-sm text-text-soft dark:text-text-soft-dark">{activity.description}</p>
                  )}
                  <p className="mt-1 text-xs text-text-soft dark:text-text-soft-dark" style={{ fontVariantNumeric: "tabular-nums" }}>
                    {formatTime(activity.occurredAt)}
                  </p>
                  {activity.photos.length > 0 && (
                    <div className="mt-2 flex gap-2">
                      {activity.photos.map((photo) => (
                        // eslint-disable-next-line @next/next/no-img-element
                        <img
                          key={photo.id}
                          src={photo.thumbnailDownloadUrl ?? undefined}
                          alt={activity.title}
                          className="h-12 w-12 rounded-lg object-cover"
                        />
                      ))}
                    </div>
                  )}
                </div>
              </div>
              <Button variant="destructive" size="sm" onClick={() => onDeleteActivity(activity)}>
                {t("actionDelete")}
              </Button>
            </li>
          );
        }

        const event = entry.childEvent;
        if (!event) return null;
        return (
          <li key={`event-${event.id}`} className="flex items-center justify-between rounded-xl bg-surface-soft p-4 dark:bg-surface-soft-dark">
            <p className="font-medium text-text dark:text-text-dark">{t(`eventTypes.${event.eventType}`)}</p>
            <p className="text-xs text-text-soft dark:text-text-soft-dark" style={{ fontVariantNumeric: "tabular-nums" }}>
              {formatTime(event.occurredAt)}
            </p>
          </li>
        );
      })}
    </ul>
  );
}
