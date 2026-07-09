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
  onSubmit: (values: { date: string; label: string; closureType: ClosureType; notifyParents: boolean }) => Promise<void>;
  saving: boolean;
}

export function ClosureDialog({ open, closure, defaultDate, onOpenChange, onSubmit, saving }: ClosureDialogProps) {
  const t = useTranslations("closures");
  const [date, setDate] = useState(defaultDate);
  const [label, setLabel] = useState("");
  const [closureType, setClosureType] = useState<ClosureType>("holiday");
  const [notifyParents, setNotifyParents] = useState(true);

  useEffect(() => {
    if (!open) return;
    setDate(closure?.date ?? defaultDate);
    setLabel(closure?.label ?? "");
    setClosureType(closure?.closureType ?? "holiday");
    setNotifyParents(closure?.notifyParents ?? true);
  }, [open, closure, defaultDate]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{closure ? t("editTitle") : t("addTitle")}</DialogTitle>
          <DialogDescription>{t("dialogDescription")}</DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          {!closure && (
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("dateLabel")}
              <Input className="mt-2" type="date" value={date} onChange={(e) => setDate(e.target.value)} />
            </label>
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
          <label className="flex items-center gap-2 text-sm font-medium text-text dark:text-text-dark">
            <input
              type="checkbox"
              checked={notifyParents}
              onChange={(e) => setNotifyParents(e.target.checked)}
              className="h-4 w-4 rounded border-border text-primary focus-visible:ring-2 focus-visible:ring-primary"
            />
            {t("notifyParents")}
          </label>
        </div>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={saving}>{t("dismiss")}</Button>
          <Button onClick={() => onSubmit({ date, label, closureType, notifyParents })} disabled={saving || !label.trim()}>
            {t("save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
