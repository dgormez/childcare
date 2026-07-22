import React from "react";
import { View } from "react-native";
import { ScheduleDayCard } from "./ScheduleDayCard";
import type { StaffScheduleResponse } from "../types";

interface ScheduleWeekListProps {
  weekDates: string[]; // 7 dates, Monday first, yyyy-MM-dd
  entriesByDate: Map<string, StaffScheduleResponse[]>;
  locationNamesById: Map<string, string>;
  groupNamesById: Map<string, string>;
  closedLocationIdsByDate: Map<string, Set<string>>;
  contractedDays: string[]; // e.g. ["Monday", "Tuesday"] — empty = no restriction
}

const WEEKDAY_NAMES = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

function weekdayNameOf(date: string): string {
  return WEEKDAY_NAMES[new Date(`${date}T00:00:00`).getDay()];
}

/** FR-003: week view — one ScheduleDayCard per date, Monday first. */
export function ScheduleWeekList({
  weekDates,
  entriesByDate,
  locationNamesById,
  groupNamesById,
  closedLocationIdsByDate,
  contractedDays,
}: ScheduleWeekListProps) {
  return (
    <View>
      {weekDates.map((date) => (
        <ScheduleDayCard
          key={date}
          date={date}
          entries={entriesByDate.get(date) ?? []}
          locationNamesById={locationNamesById}
          groupNamesById={groupNamesById}
          closedLocationIds={closedLocationIdsByDate.get(date) ?? new Set()}
          isNonContractedDay={contractedDays.length > 0 && !contractedDays.includes(weekdayNameOf(date))}
        />
      ))}
    </View>
  );
}
