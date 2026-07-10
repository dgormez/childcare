"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "./ui/dialog";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import type { AbsenceReason, GroupResponse, StaffScheduleResponse } from "../lib/types";

interface ScheduleEntryDialogProps {
  open: boolean;
  entry: StaffScheduleResponse | null;
  groups: GroupResponse[];
  onOpenChange: (open: boolean) => void;
  onSubmit: (values: { groupId: string | null; startTime: string; endTime: string }) => Promise<void>;
  onDelete: () => Promise<void>;
  onMarkAbsence: (isAbsent: boolean, reason: AbsenceReason | null) => Promise<void>;
  saving: boolean;
}

const DEFAULT_START = "08:00";
const DEFAULT_END = "16:00";

/** Create/edit form for a single rota cell — location is implicit (the page's currently
 * selected location, per the week-grid's per-location scope). Editing also exposes
 * delete and absence-marking, since both act on the same existing entry (spec.md US1/US3). */
export function ScheduleEntryDialog({ open, entry, groups, onOpenChange, onSubmit, onDelete, onMarkAbsence, saving }: ScheduleEntryDialogProps) {
  const t = useTranslations("scheduling");
  const [groupId, setGroupId] = useState("");
  const [startTime, setStartTime] = useState(DEFAULT_START);
  const [endTime, setEndTime] = useState(DEFAULT_END);
  const [absenceReason, setAbsenceReason] = useState<AbsenceReason>("sick");

  useEffect(() => {
    if (!open) return;
    setGroupId(entry?.groupId ?? "");
    setStartTime(entry?.startTime.slice(0, 5) ?? DEFAULT_START);
    setEndTime(entry?.endTime.slice(0, 5) ?? DEFAULT_END);
    setAbsenceReason((entry?.absenceReason as AbsenceReason) ?? "sick");
  }, [open, entry]);

  const invalid = startTime >= endTime;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{entry ? t("editShift") : t("addShift")}</DialogTitle>
          <DialogDescription>{entry ? t("editShift") : t("addShift")}</DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("groupLabel")}
            <select
              value={groupId}
              onChange={(e) => setGroupId(e.target.value)}
              className="mt-2 h-10 w-full rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
            >
              <option value="">{t("unassignedGroup")}</option>
              {groups.map((group) => (
                <option key={group.id} value={group.id}>{group.name}</option>
              ))}
            </select>
          </label>
          <div className="grid grid-cols-2 gap-3">
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("startTimeLabel")}
              <Input className="mt-2" type="time" value={startTime} onChange={(e) => setStartTime(e.target.value)} invalid={invalid} />
            </label>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("endTimeLabel")}
              <Input className="mt-2" type="time" value={endTime} onChange={(e) => setEndTime(e.target.value)} invalid={invalid} />
            </label>
          </div>

          {entry && (
            <div className="rounded-lg bg-surface-soft p-3 dark:bg-surface-soft-dark">
              {entry.isAbsent ? (
                <div className="flex items-center justify-between gap-3">
                  <span className="text-sm text-text dark:text-text-dark">
                    {t("absentBadge")} — {t(`absenceReason.${entry.absenceReason}`)}
                  </span>
                  <Button variant="secondary" size="sm" disabled={saving} onClick={() => onMarkAbsence(false, null)}>
                    {t("unmarkAbsent")}
                  </Button>
                </div>
              ) : (
                <div className="flex items-center justify-between gap-3">
                  <select
                    value={absenceReason}
                    onChange={(e) => setAbsenceReason(e.target.value as AbsenceReason)}
                    className="h-9 rounded-lg bg-surface px-2 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-dark dark:text-text-dark"
                  >
                    <option value="sick">{t("absenceReason.sick")}</option>
                    <option value="leave">{t("absenceReason.leave")}</option>
                    <option value="holiday">{t("absenceReason.holiday")}</option>
                  </select>
                  <Button variant="secondary" size="sm" disabled={saving} onClick={() => onMarkAbsence(true, absenceReason)}>
                    {t("markAbsent")}
                  </Button>
                </div>
              )}
            </div>
          )}
        </div>
        <DialogFooter>
          {entry && (
            <Button variant="destructive" disabled={saving} onClick={onDelete}>
              {t("delete")}
            </Button>
          )}
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={saving}>{t("cancel")}</Button>
          <Button
            onClick={() => onSubmit({ groupId: groupId || null, startTime, endTime })}
            disabled={saving || invalid}
          >
            {t("save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
