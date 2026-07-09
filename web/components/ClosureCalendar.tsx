"use client";
import { AlertTriangle, BriefcaseBusiness, CalendarDays, GraduationCap } from "lucide-react";
import { useTranslations } from "next-intl";
import { cn } from "../lib/cn";
import type { ClosureDayResponse, ClosureType } from "../lib/types";

interface ClosureCalendarProps {
  year: number;
  closures: ClosureDayResponse[];
  onSelect: (closure: ClosureDayResponse) => void;
}

const months = Array.from({ length: 12 }, (_, i) => i);

function iconFor(type: ClosureType) {
  if (type === "training") return GraduationCap;
  if (type === "extraordinary") return AlertTriangle;
  return CalendarDays;
}

function daysInMonth(year: number, month: number): number {
  return new Date(year, month + 1, 0).getDate();
}

function isoDate(year: number, month: number, day: number): string {
  return `${year}-${String(month + 1).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
}

export function ClosureCalendar({ year, closures, onSelect }: ClosureCalendarProps) {
  const t = useTranslations("closures");
  const byDate = new Map(closures.map((closure) => [closure.date, closure]));

  return (
    <div className="grid grid-cols-3 gap-4">
      {months.map((month) => (
        <section key={month} aria-label={t("monthLabel", { month: month + 1 })}>
          <h2 className="mb-2 text-sm font-semibold text-text dark:text-text-dark">
            {t(`months.${month}`)}
          </h2>
          <div className="grid grid-cols-7 gap-1 text-center text-xs">
            {Array.from({ length: daysInMonth(year, month) }, (_, index) => {
              const day = index + 1;
              const date = isoDate(year, month, day);
              const closure = byDate.get(date);
              const Icon = closure ? iconFor(closure.closureType) : BriefcaseBusiness;
              return closure ? (
                <button
                  key={date}
                  type="button"
                  onClick={() => onSelect(closure)}
                  className={cn(
                    "flex h-10 items-center justify-center rounded-lg border text-xs font-medium focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary",
                    closure.status === "cancelled"
                      ? "border-border bg-surface-soft text-text-soft dark:border-border-dark dark:bg-surface-soft-dark dark:text-text-soft-dark"
                      : "border-primary bg-primary-soft text-primary-hover dark:border-primary-dark dark:bg-primary-soft-dark dark:text-primary-hover-dark",
                  )}
                  aria-label={t("dayClosedLabel", { day, label: closure.label, type: t(`type.${closure.closureType}`) })}
                >
                  <span className="flex items-center gap-1">
                    <Icon className="h-3 w-3" strokeWidth={2} />
                    {day}
                  </span>
                </button>
              ) : (
                <div key={date} className="flex h-10 items-center justify-center rounded-lg text-text-soft dark:text-text-soft-dark">
                  {day}
                </div>
              );
            })}
          </div>
        </section>
      ))}
    </div>
  );
}
