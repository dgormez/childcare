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
  // Feature 027 (FR-002): dates within the visible week that are a published KDV closure —
  // greys the entire column for every staff member, non-selectable for new assignments.
  closureDates: Set<string>;
  onAddShift: (staffId: string, date: string) => void;
  onSelectShift: (entry: StaffScheduleResponse) => void;
}

function groupLabel(groupId: string | null, groupsById: Map<string, string>, unassignedLabel: string): string {
  if (!groupId) return unassignedLabel;
  return groupsById.get(groupId) ?? unassignedLabel;
}

const WEEKDAY_NAMES = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

// Feature 027 (FR-002): a day is outside a staff member's contracted days when the member has a
// non-empty ContractedDays list that doesn't include that date's weekday — an empty list means
// "no restriction" (data-model.md's safe default for pre-existing profiles).
function isNonContractedDay(member: StaffResponse, date: string): boolean {
  if (member.contractedDays.length === 0) return false;
  const weekday = WEEKDAY_NAMES[new Date(`${date}T00:00:00`).getDay()];
  return !member.contractedDays.includes(weekday);
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
  closureDates,
  onAddShift,
  onSelectShift,
}: SchedulingGridProps) {
  const t = useTranslations("scheduling");

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t("groupLabel")}</TableHead>
          {weekDates.map((date) => {
            const closed = closureDates.has(date);
            return (
              <TableHead
                key={date}
                data-testid={`grid-column-header-${date}`}
                className={closed ? "bg-surface-soft text-text-soft dark:bg-surface-soft-dark dark:text-text-soft-dark" : undefined}
              >
                <div className="flex flex-col gap-1">
                  <span>{new Date(`${date}T00:00:00`).toLocaleDateString(undefined, { weekday: "short", day: "numeric", month: "short" })}</span>
                  {closed ? (
                    <span className="text-xs font-normal normal-case">{t("closureDay")}</span>
                  ) : (
                    <span className="text-xs font-normal normal-case text-text-soft dark:text-text-soft-dark">
                      {t("projectedOnDuty", { count: projectedOnDutyByDate.get(date) ?? 0 })}
                    </span>
                  )}
                </div>
              </TableHead>
            );
          })}
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
              const closed = closureDates.has(date);
              const nonContracted = isNonContractedDay(member, date);
              // FR-002: greyed out and non-selectable for new assignments — a closure day or a
              // day outside the staff member's contracted days.
              const cellDisabled = closed || nonContracted;
              return (
                <TableCell
                  key={date}
                  data-testid={`grid-cell-${member.id}-${date}`}
                  className={`align-top ${cellDisabled ? "bg-surface-soft dark:bg-surface-soft-dark" : ""}`}
                >
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
                        {entry.status === "absent" && (
                          <Badge variant="danger" className="mt-1">
                            {t("absentBadge")}
                          </Badge>
                        )}
                        {entry.status === "covered" && (
                          <Badge variant="warning" className="mt-1">
                            {t("coveredBadge")}
                          </Badge>
                        )}
                      </button>
                    ))}
                    {cellDisabled ? (
                      <span className="flex h-8 items-center justify-center text-xs text-text-soft dark:text-text-soft-dark">
                        {closed ? t("closureDay") : t("nonContractedDay")}
                      </span>
                    ) : (
                      <button
                        type="button"
                        onClick={() => onAddShift(member.id, date)}
                        aria-label={t("addShift")}
                        className="flex h-8 items-center justify-center gap-1 rounded-lg text-text-soft transition hover:bg-surface-soft focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:text-text-soft-dark dark:hover:bg-surface-soft-dark"
                      >
                        <Plus className="h-4 w-4" strokeWidth={2} />
                      </button>
                    )}
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
