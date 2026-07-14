"use client";
import { useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import { Input } from "../ui/input";
import { Badge } from "../ui/badge";
import type { MealPreferenceChangeRequestResponse } from "../../lib/types";

interface MealPreferenceRequestQueueProps {
  requests: MealPreferenceChangeRequestResponse[];
  onApprove: (request: MealPreferenceChangeRequestResponse) => Promise<void>;
  onReject: (request: MealPreferenceChangeRequestResponse, reason: string | null) => Promise<void>;
}

/** Director review queue for meal-preference-change requests (feature 013e, US4) — pairs each
 * pending request with the child's active health records (FR-013) so the director can decide
 * with full context, without leaving this screen. */
export function MealPreferenceRequestQueue({ requests, onApprove, onReject }: MealPreferenceRequestQueueProps) {
  const t = useTranslations("mealPreferenceRequests");
  const [rejectTarget, setRejectTarget] = useState<MealPreferenceChangeRequestResponse | null>(null);
  const [reason, setReason] = useState("");
  const [saving, setSaving] = useState(false);

  if (requests.length === 0) return null;

  const handleApprove = async (request: MealPreferenceChangeRequestResponse) => {
    setSaving(true);
    try {
      await onApprove(request);
    } finally {
      setSaving(false);
    }
  };

  const confirmReject = async () => {
    if (!rejectTarget) return;
    setSaving(true);
    try {
      await onReject(rejectTarget, reason.trim() || null);
      setRejectTarget(null);
      setReason("");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-4">
      <h2 className="text-lg font-semibold text-text dark:text-text-dark">{t("queueTitle")}</h2>
      <div className="space-y-3">
        {requests.map((request) => (
          <div key={request.id} className="rounded-xl bg-surface-soft dark:bg-surface-soft-dark" style={{ padding: 16 }}>
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="font-medium text-text dark:text-text-dark">{request.childName}</p>
                <p className="text-sm text-text-soft dark:text-text-soft-dark">{t("requestedBy", { name: request.requestedByName })}</p>
                <div className="mt-2 flex flex-wrap gap-2">
                  {request.newTexture && <Badge variant="neutral">{t(`texture.${request.newTexture}`)}</Badge>}
                  {request.newDietaryType?.map((tag) => (
                    <Badge key={tag} variant="neutral">{t(`dietaryType.${tag}`)}</Badge>
                  ))}
                </div>
                {request.notes && <p className="mt-2 text-sm text-text dark:text-text-dark">{request.notes}</p>}
                {request.activeHealthRecords.length > 0 && (
                  <div className="mt-3">
                    <p className="text-xs font-semibold uppercase tracking-wide text-text-soft dark:text-text-soft-dark">
                      {t("activeHealthRecords")}
                    </p>
                    <ul className="mt-1 space-y-1">
                      {request.activeHealthRecords.map((record) => (
                        <li key={record.id} className="text-sm text-text dark:text-text-dark">{record.title}</li>
                      ))}
                    </ul>
                  </div>
                )}
              </div>
              <div className="flex shrink-0 gap-2">
                <Button variant="ghost" size="sm" disabled={saving} onClick={() => handleApprove(request)}>
                  {t("approve")}
                </Button>
                <Button variant="destructive" size="sm" disabled={saving} onClick={() => setRejectTarget(request)}>
                  {t("reject")}
                </Button>
              </div>
            </div>
          </div>
        ))}
      </div>

      <Dialog open={rejectTarget !== null} onOpenChange={(open) => !open && setRejectTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("rejectTitle")}</DialogTitle>
          </DialogHeader>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("rejectReasonLabel")}
            <Input className="mt-2" value={reason} onChange={(e) => setReason(e.target.value)} />
          </label>
          <DialogFooter>
            <Button variant="secondary" onClick={() => setRejectTarget(null)} disabled={saving}>
              {t("cancel")}
            </Button>
            <Button variant="destructive" onClick={confirmReject} disabled={saving}>
              {t("confirmReject")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
