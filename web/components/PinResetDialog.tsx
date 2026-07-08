"use client";
import { useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "./ui/dialog";
import { Button } from "./ui/button";
import { Input } from "./ui/input";

interface PinResetDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  staffName: string;
  onSubmit: (pin: string) => Promise<{ ok: true } | { ok: false; errorKey: string }>;
}

const PIN_PATTERN = /^\d{4}$/;

/** spec FR-009: director sets/resets a caregiver's 4-digit PIN (feature 008a's
 * PUT /api/staff/{id}/pin). Distinct from ConfirmDialog since this step needs an input, not
 * just a confirm/cancel choice. */
export function PinResetDialog({ open, onOpenChange, staffName, onSubmit }: PinResetDialogProps) {
  const t = useTranslations("staff");
  const [pin, setPin] = useState("");
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const close = (nextOpen: boolean) => {
    if (!nextOpen) {
      setPin("");
      setError("");
    }
    onOpenChange(nextOpen);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!PIN_PATTERN.test(pin)) {
      setError(t("pinDialogInvalidFormat"));
      return;
    }
    setError("");
    setSubmitting(true);
    const result = await onSubmit(pin);
    setSubmitting(false);
    if (result.ok) {
      close(false);
    } else if (result.errorKey === "errors.pin.not_unique_at_location") {
      setError(t("pinDialogNotUnique"));
    } else {
      setError(t("pinDialogGenericError"));
    }
  };

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent>
        <form onSubmit={handleSubmit}>
          <DialogHeader>
            <DialogTitle>{t("pinDialogTitle", { name: staffName })}</DialogTitle>
            <DialogDescription>{t("pinDialogDescription")}</DialogDescription>
          </DialogHeader>
          <Input
            autoFocus
            inputMode="numeric"
            maxLength={4}
            value={pin}
            onChange={(e) => setPin(e.target.value.replace(/\D/g, ""))}
            invalid={Boolean(error)}
            aria-label={t("pinDialogInputLabel")}
            placeholder="0000"
          />
          {error && (
            <p className="mt-2 text-sm text-danger dark:text-danger-dark" role="alert">
              {error}
            </p>
          )}
          <DialogFooter>
            <Button type="button" variant="secondary" onClick={() => close(false)} disabled={submitting}>
              {t("pinDialogCancel")}
            </Button>
            <Button type="submit" disabled={submitting}>
              {t("pinDialogConfirm")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
