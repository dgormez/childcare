"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "./ui/dialog";
import { Button } from "./ui/button";
import { cn } from "../lib/cn";
import type { DayReservationResponse } from "../lib/types";

interface ApproveDayReservationDialogProps {
  open: boolean;
  reservation: DayReservationResponse | null;
  onOpenChange: (open: boolean) => void;
  onConfirm: (absenceJustified: boolean | null) => void;
  saving?: boolean;
}

/** FR-008: absence approval requires the director to set the justified flag before confirming;
 * extra/exchange approvals need no extra input beyond the confirm action itself. */
export function ApproveDayReservationDialog({ open, reservation, onOpenChange, onConfirm, saving = false }: ApproveDayReservationDialogProps) {
  const t = useTranslations("dayReservations");
  const [justified, setJustified] = useState<boolean>(true);

  useEffect(() => {
    if (open) setJustified(true);
  }, [open]);

  if (!reservation) return null;
  const isAbsence = reservation.type === "absence";

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("approveTitle")}</DialogTitle>
        </DialogHeader>
        {isAbsence && (
          <div>
            <p className="text-sm font-medium text-text dark:text-text-dark">{t("justifiedLabel")}</p>
            <div className="mt-2 inline-flex overflow-hidden rounded-lg border border-border dark:border-border-dark">
              <button
                type="button"
                onClick={() => setJustified(true)}
                className={cn(
                  "px-4 py-2 text-sm font-medium transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary",
                  justified ? "bg-primary text-white" : "text-text hover:bg-surface-soft dark:text-text-dark dark:hover:bg-surface-soft-dark",
                )}
              >
                {t("justifiedYes")}
              </button>
              <button
                type="button"
                onClick={() => setJustified(false)}
                className={cn(
                  "px-4 py-2 text-sm font-medium transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary",
                  !justified ? "bg-primary text-white" : "text-text hover:bg-surface-soft dark:text-text-dark dark:hover:bg-surface-soft-dark",
                )}
              >
                {t("justifiedNo")}
              </button>
            </div>
          </div>
        )}
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={saving}>
            {t("cancel")}
          </Button>
          <Button onClick={() => onConfirm(isAbsence ? justified : null)} disabled={saving}>
            {t("confirmApprove")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
