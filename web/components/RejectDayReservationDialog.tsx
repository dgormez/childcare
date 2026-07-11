"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "./ui/dialog";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import type { DayReservationResponse } from "../lib/types";

interface RejectDayReservationDialogProps {
  open: boolean;
  reservation: DayReservationResponse | null;
  onOpenChange: (open: boolean) => void;
  onConfirm: (directorNotes: string | null) => void;
  saving?: boolean;
}

/** FR-009/FR-013: rejection accepts an optional note, included verbatim in the parent's push
 * notification. */
export function RejectDayReservationDialog({ open, reservation, onOpenChange, onConfirm, saving = false }: RejectDayReservationDialogProps) {
  const t = useTranslations("dayReservations");
  const [notes, setNotes] = useState("");

  useEffect(() => {
    if (open) setNotes("");
  }, [open]);

  if (!reservation) return null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("rejectTitle")}</DialogTitle>
        </DialogHeader>
        <label className="block text-sm font-medium text-text dark:text-text-dark">
          {t("directorNotesLabel")}
          <Input className="mt-2" value={notes} onChange={(e) => setNotes(e.target.value)} />
        </label>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={saving}>
            {t("cancel")}
          </Button>
          <Button variant="destructive" onClick={() => onConfirm(notes.trim() || null)} disabled={saving}>
            {t("confirmReject")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
