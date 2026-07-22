import React from "react";
import { View, Text } from "react-native";
import { useTranslation } from "react-i18next";
import dayjs from "dayjs";
import type { StaffScheduleResponse } from "../types";

interface ScheduleDayCardProps {
  date: string; // yyyy-MM-dd
  entries: StaffScheduleResponse[];
  locationNamesById: Map<string, string>;
  groupNamesById: Map<string, string>;
  // Location ids closed on this specific date (FR-004) — per-location, not per-day, since a
  // split day (User Story 2 Scenario 2) can have one closed and one open location.
  closedLocationIds: Set<string>;
  isNonContractedDay: boolean;
}

const STATUS_LABEL_KEY: Record<StaffScheduleResponse["status"], string | null> = {
  scheduled: null,
  confirmed: null,
  absent: "schedule.status.absent",
  covered: "schedule.status.covered",
};

/**
 * FR-003/FR-004: one calendar day's row — location/group/time per entry (split-day shows both,
 * User Story 2 Scenario 2), a closure day shows "KDV gesloten" instead of an empty/unscheduled
 * day, and a non-contracted day is visually de-emphasized the same way the director-web grid
 * greys it (spec.md UX Requirements — this is a read-only mirror of that same rule).
 */
export function ScheduleDayCard({
  date,
  entries,
  locationNamesById,
  groupNamesById,
  closedLocationIds,
  isNonContractedDay,
}: ScheduleDayCardProps) {
  const { t } = useTranslation();
  const deEmphasized = isNonContractedDay && entries.length === 0;

  return (
    <View
      className="bg-surface dark:bg-surface-dark rounded-xl px-4 py-3 mb-3"
      style={{ opacity: deEmphasized ? 0.55 : 1 }}
      testID={`schedule-day-${date}`}
    >
      <Text className="text-text dark:text-text-dark text-sm font-semibold mb-2">
        {dayjs(date).format("dddd D MMM")}
      </Text>

      {entries.length === 0 && (
        <Text className="text-text-soft dark:text-text-soft-dark text-sm" testID={isNonContractedDay ? undefined : "no-shifts-label"}>
          {isNonContractedDay ? t("schedule.nonContractedDay") : t("schedule.noShifts")}
        </Text>
      )}

      {entries.map((entry) => {
        const closed = closedLocationIds.has(entry.locationId);
        const statusKey = STATUS_LABEL_KEY[entry.status];
        return (
          <View key={entry.id} className="mb-2 last:mb-0" testID={`schedule-entry-${entry.id}`}>
            <Text className="text-text dark:text-text-dark text-base font-medium">
              {locationNamesById.get(entry.locationId) ?? t("schedule.unknownLocation")}
              {entry.groupId ? ` · ${groupNamesById.get(entry.groupId) ?? ""}` : ""}
            </Text>
            <Text className="text-text-soft dark:text-text-soft-dark text-sm">
              {entry.startTime.slice(0, 5)}–{entry.endTime.slice(0, 5)}
            </Text>
            {statusKey && (
              <Text className={`text-sm font-medium ${entry.status === "absent" ? "text-danger dark:text-danger-dark" : "text-warning dark:text-warning-dark"}`}>
                {t(statusKey)}
              </Text>
            )}
            {closed && (
              <Text className="text-text-soft dark:text-text-soft-dark text-xs mt-1" testID="closure-label">
                {t("schedule.closed")}
              </Text>
            )}
          </View>
        );
      })}
    </View>
  );
}
