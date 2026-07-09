"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "./ui/dialog";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import type { AttendanceRecordResponse, AttendanceStatus } from "../lib/types";

interface AttendanceCorrectionDialogProps {
  record: AttendanceRecordResponse | null;
  childName: string;
  onOpenChange: (open: boolean) => void;
  onSubmit: (changes: {
    status?: AttendanceStatus;
    checkInAt?: string | null;
    checkOutAt?: string | null;
    absenceJustified?: boolean;
    absenceReason?: string | null;
  }) => Promise<{ ok: true } | { ok: false; errorKey: string }>;
}

// Datetime-local inputs need "yyyy-MM-ddTHH:mm" (no seconds/timezone) — ISO strings from the API
// carry both, so this strips down to what <input type="datetime-local"> expects.
function toLocalInputValue(iso: string | null): string {
  if (!iso) return "";
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** FR-011/FR-011a: a director can correct any attendance record regardless of age — sets
 * check-in/check-out times, status, or absence justification. */
export function AttendanceCorrectionDialog({ record, childName, onOpenChange, onSubmit }: AttendanceCorrectionDialogProps) {
  const t = useTranslations("attendance");
  const [status, setStatus] = useState<"present" | "absent">("present");
  const [checkInAt, setCheckInAt] = useState(() => toLocalInputValue(record?.checkInAt ?? null));
  const [checkOutAt, setCheckOutAt] = useState(() => toLocalInputValue(record?.checkOutAt ?? null));
  const [absenceJustified, setAbsenceJustified] = useState("true");
  const [absenceReason, setAbsenceReason] = useState("");
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!record) return;
    setStatus(record.status === "absent" ? "absent" : "present");
    setCheckInAt(toLocalInputValue(record.checkInAt));
    setCheckOutAt(toLocalInputValue(record.checkOutAt));
    setAbsenceJustified(record.absenceJustified === false ? "false" : "true");
    setAbsenceReason(record.absenceReason ?? "");
  }, [record]);

  if (!record) return null;

  const close = (nextOpen: boolean) => {
    if (!nextOpen) setError("");
    onOpenChange(nextOpen);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setSubmitting(true);
    const result =
      status === "present"
        ? await onSubmit({
            status,
            checkInAt: checkInAt ? new Date(checkInAt).toISOString() : undefined,
            checkOutAt: checkOutAt ? new Date(checkOutAt).toISOString() : null,
          })
        : await onSubmit({
            status,
            absenceJustified: absenceJustified === "true",
            absenceReason: absenceReason.trim(),
          });
    setSubmitting(false);
    if (result.ok) close(false);
    else setError(t(errorMessageKey(result.errorKey)));
  };

  return (
    <Dialog open={Boolean(record)} onOpenChange={close}>
      <DialogContent>
        <form onSubmit={handleSubmit}>
          <DialogHeader>
            <DialogTitle>{t("correctionDialogTitle", { name: childName })}</DialogTitle>
            <DialogDescription>{t("correctionDialogDescription")}</DialogDescription>
          </DialogHeader>

          <div className="space-y-3">
            <label className="block">
              <span className="mb-1 block text-sm text-text-soft dark:text-text-soft-dark">{t("correctionStatusLabel")}</span>
              <select
                value={status}
                onChange={(e) => setStatus(e.target.value as "present" | "absent")}
                className="flex h-10 w-full rounded-lg bg-surface-soft px-3 py-2 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
              >
                <option value="present">{t("status.present")}</option>
                <option value="absent">{t("status.absent")}</option>
              </select>
            </label>
            {status === "present" ? (
              <>
                <label className="block">
                  <span className="mb-1 block text-sm text-text-soft dark:text-text-soft-dark">{t("columnCheckIn")}</span>
                  <Input type="datetime-local" value={checkInAt} onChange={(e) => setCheckInAt(e.target.value)} />
                </label>
                <label className="block">
                  <span className="mb-1 block text-sm text-text-soft dark:text-text-soft-dark">{t("columnCheckOut")}</span>
                  <Input type="datetime-local" value={checkOutAt} onChange={(e) => setCheckOutAt(e.target.value)} />
                </label>
              </>
            ) : (
              <>
                <label className="block">
                  <span className="mb-1 block text-sm text-text-soft dark:text-text-soft-dark">{t("correctionAbsenceJustifiedLabel")}</span>
                  <select
                    value={absenceJustified}
                    onChange={(e) => setAbsenceJustified(e.target.value)}
                    className="flex h-10 w-full rounded-lg bg-surface-soft px-3 py-2 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
                  >
                    <option value="true">{t("justified")}</option>
                    <option value="false">{t("unjustified")}</option>
                  </select>
                </label>
                <label className="block">
                  <span className="mb-1 block text-sm text-text-soft dark:text-text-soft-dark">{t("correctionAbsenceReasonLabel")}</span>
                  <Input value={absenceReason} onChange={(e) => setAbsenceReason(e.target.value)} />
                </label>
              </>
            )}
          </div>

          {error && (
            <p className="mt-2 text-sm text-danger dark:text-danger-dark" role="alert">
              {error}
            </p>
          )}

          <DialogFooter>
            <Button type="button" variant="secondary" onClick={() => close(false)} disabled={submitting}>
              {t("correctionDialogCancel")}
            </Button>
            <Button type="submit" disabled={submitting}>
              {t("correctionDialogSave")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function errorMessageKey(errorKey: string): string {
  if (errorKey === "errors.attendance.closure_status_immutable") return "correctionErrorClosureImmutable";
  if (errorKey === "errors.validation") return "correctionErrorValidation";
  return "correctionErrorGeneric";
}
