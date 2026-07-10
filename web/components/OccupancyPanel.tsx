"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { CalendarRange } from "lucide-react";
import { apiClient } from "../lib/apiClient";
import { Input } from "./ui/input";
import { EmptyState } from "./EmptyState";
import type { OccupancyDayResponse } from "../lib/types";

interface OccupancyPanelProps {
  locationId: string;
  defaultDate: string;
}

// Deliberately avoids Date.toISOString() here, which converts through UTC and shifts the date
// backward by a day in any positive-UTC-offset timezone (feature 012 precedent) — builds the
// string from local date components instead.
function addDays(date: string, days: number): string {
  const d = new Date(`${date}T00:00:00`);
  d.setDate(d.getDate() + days);
  const year = d.getFullYear();
  const month = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

export function OccupancyPanel({ locationId, defaultDate }: OccupancyPanelProps) {
  const t = useTranslations("waitingList");
  const [from, setFrom] = useState(defaultDate);
  const [to, setTo] = useState(addDays(defaultDate, 13));
  const [days, setDays] = useState<OccupancyDayResponse[]>([]);

  useEffect(() => {
    setFrom(defaultDate);
    setTo(addDays(defaultDate, 13));
  }, [defaultDate]);

  const load = useCallback(async () => {
    if (!locationId || !from || !to || to < from) return;
    const result = await (apiClient.GET as any)("/api/waiting-list/occupancy", {
      params: { query: { locationId, from, to } },
    });
    if (result.response.ok) setDays((result.data ?? []) as OccupancyDayResponse[]);
  }, [locationId, from, to]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div className="rounded-xl border border-border bg-surface p-4 dark:border-border-dark dark:bg-surface-dark">
      <h2 className="mb-3 text-sm font-semibold text-text dark:text-text-dark">{t("occupancy")}</h2>
      <div className="mb-4 flex items-center gap-3">
        <label className="text-xs text-text-soft dark:text-text-soft-dark">
          {t("occupancyFrom")}
          <Input className="mt-1 h-9" type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        </label>
        <label className="text-xs text-text-soft dark:text-text-soft-dark">
          {t("occupancyTo")}
          <Input className="mt-1 h-9" type="date" value={to} onChange={(e) => setTo(e.target.value)} />
        </label>
      </div>
      {days.length === 0 ? (
        <EmptyState icon={CalendarRange} message={t("emptyState")} />
      ) : (
        <ul className="space-y-1">
          {days.map((day) => (
            <li key={day.date} className="flex items-center justify-between rounded-lg px-2 py-1 text-sm">
              <span className="text-text dark:text-text-dark">{new Date(`${day.date}T00:00:00`).toLocaleDateString()}</span>
              <span
                className={
                  day.closed
                    ? "text-text-soft dark:text-text-soft-dark"
                    : "text-text dark:text-text-dark"
                }
                style={{ fontVariantNumeric: "tabular-nums" }}
              >
                {day.closed ? t("occupancyClosed") : t("occupancyFreeCapacity", { count: day.freeCapacity ?? 0 })}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
