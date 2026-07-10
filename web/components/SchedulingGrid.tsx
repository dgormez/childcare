"use client";
import { Plus } from "lucide-react";
import { useTranslations } from "next-intl";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "./ui/table";
import { Badge } from "./ui/badge";
import type { StaffResponse, StaffScheduleResponse } from "../lib/types";

interface SchedulingGridProps {
  weekDates: string[]; // 7 dates, Monday first, yyyy-MM-dd
  staff: StaffResponse[];
  entries: StaffScheduleResponse[];
  groupsById: Map<string, string>;
  projectedOnDutyByDate: Map<string, number>;
  onAddShift: (staffId: string, date: string) => void;
  onSelectShift: (entry: StaffScheduleResponse) => void;
}

function groupLabel(groupId: string | null, groupsById: Map<string, string>, unassignedLabel: string): string {
  if (!groupId) return unassignedLabel;
  return groupsById.get(groupId) ?? unassignedLabel;
}

/**
 * Week x staff rota grid — director-web density per platform-rules.md/design-system.md
 * (reuses the shared Table primitives rather than a bespoke grid component, per
 * design-system.md's "shared components reused rather than reimplemented" rule). Every cell
 * action is a real <button>, keyboard-reachable with a visible focus ring — no hover-only
 * affordances (spec.md UX Requirements Accessibility).
 */
export function SchedulingGrid({
  weekDates,
  staff,
  entries,
  groupsById,
  projectedOnDutyByDate,
  onAddShift,
  onSelectShift,
}: SchedulingGridProps) {
  const t = useTranslations("scheduling");

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t("groupLabel")}</TableHead>
          {weekDates.map((date) => (
            <TableHead key={date}>
              <div className="flex flex-col gap-1">
                <span>{new Date(`${date}T00:00:00`).toLocaleDateString(undefined, { weekday: "short", day: "numeric", month: "short" })}</span>
                <span className="text-xs font-normal normal-case text-text-soft dark:text-text-soft-dark">
                  {t("projectedOnDuty", { count: projectedOnDutyByDate.get(date) ?? 0 })}
                </span>
              </div>
            </TableHead>
          ))}
        </TableRow>
      </TableHeader>
      <TableBody>
        {staff.map((member) => (
          <TableRow key={member.id}>
            <TableCell className="font-medium">
              {member.firstName} {member.lastName}
            </TableCell>
            {weekDates.map((date) => {
              const dayEntries = entries.filter((e) => e.staffProfileId === member.id && e.date === date);
              return (
                <TableCell key={date} className="align-top">
                  <div className="flex flex-col gap-1">
                    {dayEntries.map((entry) => (
                      <button
                        key={entry.id}
                        type="button"
                        onClick={() => onSelectShift(entry)}
                        className="flex flex-col items-start gap-1 rounded-lg bg-primary-soft px-2 py-1 text-left text-xs text-primary-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-primary-soft-dark dark:text-primary-hover-dark"
                      >
                        <span className="font-medium">
                          {entry.startTime.slice(0, 5)}–{entry.endTime.slice(0, 5)}
                        </span>
                        <span className="text-text-soft dark:text-text-soft-dark">
                          {groupLabel(entry.groupId, groupsById, t("unassignedGroup"))}
                        </span>
                        {entry.isAbsent && (
                          <Badge variant="danger" className="mt-1">
                            {t("absentBadge")}
                          </Badge>
                        )}
                      </button>
                    ))}
                    <button
                      type="button"
                      onClick={() => onAddShift(member.id, date)}
                      aria-label={t("addShift")}
                      className="flex h-8 items-center justify-center gap-1 rounded-lg text-text-soft transition hover:bg-surface-soft focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:text-text-soft-dark dark:hover:bg-surface-soft-dark"
                    >
                      <Plus className="h-4 w-4" strokeWidth={2} />
                    </button>
                  </div>
                </TableCell>
              );
            })}
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
