"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import { Input } from "../ui/input";
import type { StaffTimeEntryFunction, StaffTimeEntryResponse } from "../../lib/types";
import { apiClient } from "../../lib/apiClient";

const FUNCTIONS: StaffTimeEntryFunction[] = ["kinderbegeleider", "logistiek", "verantwoordelijke"];

function toLocalInputValue(iso: string | null): string {
  if (!iso) return "";
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/**
 * Director correction of one time entry (spec.md FR-006/FR-007/FR-008/FR-009, User Story 2):
 * clock-out fill-in, function correction, an overlap warning banner (never a block), and the
 * unlock/re-lock override for entries past the 7-day lock.
 */
export function TimeEntryCorrectionDialog({
  entry,
  onOpenChange,
  onSaved,
}: {
  entry: StaffTimeEntryResponse | null;
  onOpenChange: (open: boolean) => void;
  onSaved: () => void;
}) {
  const t = useTranslations("staff.timeEntries");
  const [clockedOutAt, setClockedOutAt] = useState("");
  const [staffFunction, setStaffFunction] = useState<StaffTimeEntryFunction>("kinderbegeleider");
  const [notes, setNotes] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [overlapWarning, setOverlapWarning] = useState(false);

  useEffect(() => {
    if (!entry) return;
    setClockedOutAt(toLocalInputValue(entry.clockedOutAt));
    setStaffFunction(entry.function);
    setNotes(entry.notes ?? "");
    setError(null);
    setOverlapWarning(false);
  }, [entry]);

  if (!entry) return null;

  async function save() {
    setSaving(true);
    setError(null);
    const result = await apiClient.PATCH("/api/staff-time-entries/{id}", {
      params: { path: { id: entry!.id } },
      body: {
        clockedOutAt: clockedOutAt ? new Date(clockedOutAt).toISOString() : null,
        function: staffFunction,
        groupId: null,
        notes: notes || null,
      },
    });
    setSaving(false);
    if (!result.response.ok) {
      setError(t("saveError"));
      return;
    }
    const body = result.data as unknown as { overlapWarning: boolean };
    if (body.overlapWarning) {
      setOverlapWarning(true);
      return;
    }
    onSaved();
  }

  async function unlock() {
    setSaving(true);
    await apiClient.POST("/api/staff-time-entries/{id}/unlock", { params: { path: { id: entry!.id } } });
    setSaving(false);
    onSaved();
  }

  async function relock() {
    setSaving(true);
    await apiClient.POST("/api/staff-time-entries/{id}/relock", { params: { path: { id: entry!.id } } });
    setSaving(false);
    onSaved();
  }

  return (
    <Dialog open={!!entry} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("correctionTitle")}</DialogTitle>
        </DialogHeader>

        {error && <p className="text-sm text-danger dark:text-danger-dark">{error}</p>}
        {overlapWarning && <p className="text-sm text-warning">{t("overlapWarning")}</p>}

        {entry.isLocked ? (
          <div className="space-y-4">
            <p className="text-sm text-text-soft dark:text-text-soft-dark">{t("lockedMessage")}</p>
            <Button onClick={unlock} disabled={saving}>
              {t("unlock")}
            </Button>
          </div>
        ) : (
          <div className="space-y-4">
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("clockedOutLabel")}
              <Input type="datetime-local" value={clockedOutAt} onChange={(e) => setClockedOutAt(e.target.value)} />
            </label>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("functionLabel")}
              <select
                value={staffFunction}
                onChange={(e) => setStaffFunction(e.target.value as StaffTimeEntryFunction)}
                className="mt-1 block w-full rounded-lg border-0 bg-surface-soft px-3 py-2 text-sm text-text dark:bg-surface-soft-dark dark:text-text-dark"
              >
                {FUNCTIONS.map((fn) => (
                  <option key={fn} value={fn}>
                    {t(`functions.${fn}`)}
                  </option>
                ))}
              </select>
            </label>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("notesLabel")}
              <Input value={notes} onChange={(e) => setNotes(e.target.value)} />
            </label>
            {entry.unlockedAt && (
              <Button variant="secondary" onClick={relock} disabled={saving}>
                {t("relock")}
              </Button>
            )}
          </div>
        )}

        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            {t("cancel")}
          </Button>
          {!entry.isLocked && (
            <Button onClick={save} disabled={saving}>
              {t("save")}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
