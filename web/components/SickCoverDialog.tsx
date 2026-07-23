"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "./ui/dialog";
import { Button } from "./ui/button";
import { EmptyState } from "./EmptyState";
import { apiClient } from "../lib/apiClient";
import type { SickCoverCandidateResponse, StaffScheduleResponse } from "../lib/types";
import { UserRoundX } from "lucide-react";

interface SickCoverDialogProps {
  open: boolean;
  entry: StaffScheduleResponse | null;
  onOpenChange: (open: boolean) => void;
  onAssigned: () => Promise<void>;
}

/** FR-006/FR-007: eligible-cover candidate picker for a same-day absence, opened from the
 * urgent sick-cover banner. */
export function SickCoverDialog({ open, entry, onOpenChange, onAssigned }: SickCoverDialogProps) {
  const t = useTranslations("scheduling");
  const [candidates, setCandidates] = useState<SickCoverCandidateResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [assigning, setAssigning] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!open || !entry) return;
    setLoading(true);
    setError("");
    apiClient
      .GET("/api/staff-schedules/{date}/sick-cover-candidates", {
        params: { path: { date: entry.date }, query: { excludeStaffProfileId: entry.staffProfileId } },
      })
      .then((result) => {
        setLoading(false);
        if (!result.response.ok || !result.data) {
          setError(t("genericError"));
          return;
        }
        setCandidates(result.data as unknown as SickCoverCandidateResponse[]);
      });
  }, [open, entry, t]);

  async function assign(staffProfileId: string) {
    if (!entry) return;
    setAssigning(true);
    const result = await apiClient.POST("/api/staff-schedules/{id}/assign-cover", {
      params: { path: { id: entry.id } },
      body: { coverStaffProfileId: staffProfileId },
    });
    setAssigning(false);
    if (!result.response.ok) {
      setError(t("genericError"));
      return;
    }
    await onAssigned();
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("sickCoverDialog.title")}</DialogTitle>
          <DialogDescription>{t("sickCoverDialog.description")}</DialogDescription>
        </DialogHeader>
        <div className="space-y-2">
          {loading && <div className="h-24 rounded-lg bg-surface-soft dark:bg-surface-soft-dark" />}
          {error && <p className="text-sm text-danger dark:text-danger-dark">{error}</p>}
          {!loading && !error && candidates.length === 0 && (
            <EmptyState icon={UserRoundX} message={t("sickCoverDialog.noCandidates")} />
          )}
          {!loading &&
            candidates.map((candidate) => (
              <div
                key={candidate.staffProfileId}
                className="flex items-center justify-between gap-3 rounded-lg bg-surface-soft p-3 dark:bg-surface-soft-dark"
              >
                <div>
                  <p className="text-sm font-medium text-text dark:text-text-dark">{candidate.name}</p>
                  {candidate.qualificationLevel && (
                    <p className="text-xs text-text-soft dark:text-text-soft-dark">{candidate.qualificationLevel}</p>
                  )}
                </div>
                <Button size="sm" disabled={assigning} onClick={() => assign(candidate.staffProfileId)}>
                  {t("sickCoverDialog.assign")}
                </Button>
              </div>
            ))}
        </div>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={assigning}>
            {t("cancel")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
