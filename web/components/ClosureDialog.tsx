"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "./ui/dialog";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import type { ClosureDayResponse, ClosureType } from "../lib/types";

interface ClosureDialogProps {
  open: boolean;
  closure: ClosureDayResponse | null;
  defaultDate: string;
  onOpenChange: (open: boolean) => void;
  onSubmit: (values: { date: string; endDate: string | null; label: string; closureType: ClosureType }) => Promise<void>;
  saving: boolean;
}

/** Create/edit form for closure days. Notify-parents is decided later, at publish time (see the
 * Publish confirmation dialog on the closures page) — not here, so a director can draft an
 * entire season of closure days up front without committing to a notification decision for each
 * one individually. Creating (not editing) supports an optional end date so a multi-week block
 * (e.g. summer closure) doesn't need one dialog submission per day. */
export function ClosureDialog({ open, closure, defaultDate, onOpenChange, onSubmit, saving }: ClosureDialogProps) {
  const t = useTranslations("closures");
  const [date, setDate] = useState(defaultDate);
  const [endDate, setEndDate] = useState("");
  const [label, setLabel] = useState("");
  const [closureType, setClosureType] = useState<ClosureType>("holiday");

  useEffect(() => {
    if (!open) return;
    setDate(closure?.date ?? defaultDate);
    setEndDate("");
    setLabel(closure?.label ?? "");
    setClosureType(closure?.closureType ?? "holiday");
  }, [open, closure, defaultDate]);

  const endDateInvalid = !!endDate && endDate < date;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{closure ? t("editTitle") : t("addTitle")}</DialogTitle>
          <DialogDescription>{closure ? t("dialogDescription") : t("dialogDescriptionRange")}</DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          {!closure && (
            <div className="grid grid-cols-2 gap-4">
              <label className="block text-sm font-medium text-text dark:text-text-dark">
                {t("dateLabel")}
                <Input className="mt-2" type="date" value={date} onChange={(e) => setDate(e.target.value)} />
              </label>
              <label className="block text-sm font-medium text-text dark:text-text-dark">
                {t("endDateLabel")}
                <Input
                  className="mt-2"
                  type="date"
                  invalid={endDateInvalid}
                  min={date}
                  value={endDate}
                  onChange={(e) => setEndDate(e.target.value)}
                />
                {endDateInvalid && <p className="mt-1 text-xs text-danger dark:text-danger-dark">{t("endDateInvalid")}</p>}
              </label>
            </div>
          )}
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("labelLabel")}
            <Input className="mt-2" value={label} onChange={(e) => setLabel(e.target.value)} />
          </label>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("typeLabel")}
            <select
              value={closureType}
              onChange={(e) => setClosureType(e.target.value as ClosureType)}
              className="mt-2 h-10 w-full rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
            >
              <option value="holiday">{t("type.holiday")}</option>
              <option value="training">{t("type.training")}</option>
              <option value="extraordinary">{t("type.extraordinary")}</option>
            </select>
          </label>
        </div>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={saving}>{t("dismiss")}</Button>
          <Button
            onClick={() => onSubmit({ date, endDate: endDate || null, label, closureType })}
            disabled={saving || !label.trim() || endDateInvalid}
          >
            {t("save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
