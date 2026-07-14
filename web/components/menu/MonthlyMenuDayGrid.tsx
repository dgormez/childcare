"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Save, Send, Undo2, CheckCircle2, FileEdit } from "lucide-react";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../ui/table";
import { Input } from "../ui/input";
import { Button } from "../ui/button";
import { Badge } from "../ui/badge";
import type { MonthlyMenuResponse } from "../../lib/types";

interface DayFields {
  soup: string;
  mainCourse: string;
  dessert: string;
  notes: string;
}

export interface MonthlyMenuDaySave {
  date: string;
  soup: string | null;
  mainCourse: string | null;
  dessert: string | null;
  notes: string | null;
}

interface MonthlyMenuDayGridProps {
  year: number;
  month: number; // 1-12
  menu: MonthlyMenuResponse;
  saving: boolean;
  onSave: (days: MonthlyMenuDaySave[]) => Promise<void>;
  onPublish: () => Promise<void>;
  onUnpublish: () => Promise<void>;
}

function daysInMonth(year: number, month: number): string[] {
  const count = new Date(year, month, 0).getDate();
  return Array.from({ length: count }, (_, i) => {
    const day = String(i + 1).padStart(2, "0");
    const monthStr = String(month).padStart(2, "0");
    return `${year}-${monthStr}-${day}`;
  });
}

function toFieldMap(menu: MonthlyMenuResponse, dates: string[]): Map<string, DayFields> {
  const byDate = new Map(menu.days.map((d) => [d.date, d]));
  return new Map(
    dates.map((date) => {
      const entry = byDate.get(date);
      return [
        date,
        {
          soup: entry?.soup ?? "",
          mainCourse: entry?.mainCourse ?? "",
          dessert: entry?.dessert ?? "",
          notes: entry?.notes ?? "",
        },
      ];
    }),
  );
}

/** Director day-by-day authoring grid for a month's menu (feature 013e, US1). Save persists a
 * draft without changing publish state; Publish/Un-publish are distinct labeled+iconed actions
 * (never a single toggle) so a director can't mis-tap between them (design-system.md icon-pairing
 * convention, FR-003/FR-004). */
export function MonthlyMenuDayGrid({ year, month, menu, saving, onSave, onPublish, onUnpublish }: MonthlyMenuDayGridProps) {
  const t = useTranslations("menu");
  const dates = daysInMonth(year, month);
  const [fields, setFields] = useState<Map<string, DayFields>>(() => toFieldMap(menu, dates));

  useEffect(() => {
    setFields(toFieldMap(menu, dates));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [menu, year, month]);

  const updateField = (date: string, key: keyof DayFields, value: string) => {
    setFields((current) => {
      const next = new Map(current);
      next.set(date, { ...(next.get(date) ?? { soup: "", mainCourse: "", dessert: "", notes: "" }), [key]: value });
      return next;
    });
  };

  const handleSave = () => {
    const days: MonthlyMenuDaySave[] = dates.map((date) => {
      const f = fields.get(date)!;
      return {
        date,
        soup: f.soup.trim() || null,
        mainCourse: f.mainCourse.trim() || null,
        dessert: f.dessert.trim() || null,
        notes: f.notes.trim() || null,
      };
    });
    return onSave(days);
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-4">
        <div className="flex items-center gap-2">
          {menu.isPublished ? (
            <Badge variant="success" className="inline-flex items-center gap-1">
              <CheckCircle2 className="h-3 w-3" strokeWidth={2} />
              {t("statusPublished")}
            </Badge>
          ) : (
            <Badge variant="neutral" className="inline-flex items-center gap-1">
              <FileEdit className="h-3 w-3" strokeWidth={2} />
              {t("statusDraft")}
            </Badge>
          )}
        </div>
        <div className="flex items-center gap-3">
          <Button variant="secondary" disabled={saving} onClick={handleSave}>
            <Save className="h-4 w-4" strokeWidth={2} />
            {t("saveDraft")}
          </Button>
          {menu.isPublished ? (
            <Button variant="secondary" disabled={saving} onClick={onUnpublish}>
              <Undo2 className="h-4 w-4" strokeWidth={2} />
              {t("unpublish")}
            </Button>
          ) : (
            <Button disabled={saving} onClick={onPublish}>
              <Send className="h-4 w-4" strokeWidth={2} />
              {t("publish")}
            </Button>
          )}
        </div>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t("columnDate")}</TableHead>
            <TableHead>{t("columnSoup")}</TableHead>
            <TableHead>{t("columnMainCourse")}</TableHead>
            <TableHead>{t("columnDessert")}</TableHead>
            <TableHead>{t("columnNotes")}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {dates.map((date) => {
            const f = fields.get(date)!;
            const label = new Date(`${date}T00:00:00`).toLocaleDateString(undefined, { weekday: "short", day: "numeric" });
            return (
              <TableRow key={date}>
                <TableCell className="whitespace-nowrap font-medium">{label}</TableCell>
                <TableCell>
                  <Input aria-label={t("columnSoup")} value={f.soup} onChange={(e) => updateField(date, "soup", e.target.value)} />
                </TableCell>
                <TableCell>
                  <Input aria-label={t("columnMainCourse")} value={f.mainCourse} onChange={(e) => updateField(date, "mainCourse", e.target.value)} />
                </TableCell>
                <TableCell>
                  <Input aria-label={t("columnDessert")} value={f.dessert} onChange={(e) => updateField(date, "dessert", e.target.value)} />
                </TableCell>
                <TableCell>
                  <Input aria-label={t("columnNotes")} value={f.notes} onChange={(e) => updateField(date, "notes", e.target.value)} />
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </div>
  );
}
