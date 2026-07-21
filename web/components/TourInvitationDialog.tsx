"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "./ui/dialog";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import { Badge } from "./ui/badge";
import type { WaitingListEntryResponse } from "../lib/types";

interface TourInvitationDialogProps {
  open: boolean;
  entry: WaitingListEntryResponse | null;
  onOpenChange: (open: boolean) => void;
  onSendInvitation: (proposedAt: string) => Promise<void>;
  onRecordOutcome: (outcome: string) => Promise<void>;
  saving: boolean;
}

// Feature 023 FR-015/FR-017 — a director sends a proposed date/time (accept/decline link goes
// out by email) and, independently, records what actually happened. Two separate actions in one
// dialog since they operate on the same entry but have no ordering dependency on each other.
export function TourInvitationDialog({ open, entry, onOpenChange, onSendInvitation, onRecordOutcome, saving }: TourInvitationDialogProps) {
  const t = useTranslations("waitingList.tourInvitation");
  const [proposedAt, setProposedAt] = useState("");
  const [outcome, setOutcome] = useState("");

  useEffect(() => {
    if (!open) return;
    setProposedAt("");
    setOutcome(entry?.tourOutcome ?? "");
  }, [open, entry]);

  if (!entry) return null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("title")}</DialogTitle>
        </DialogHeader>
        <div className="space-y-6">
          <div className="flex items-center gap-2">
            <span className="text-sm text-text-soft dark:text-text-soft-dark">{t("statusLabel")}</span>
            <Badge variant={entry.tourInvitationStatus === "accepted" ? "success" : "neutral"}>
              {t(`status.${entry.tourInvitationStatus}`)}
            </Badge>
          </div>

          <div className="space-y-2">
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("proposedAtLabel")}
              <Input type="datetime-local" className="mt-2" value={proposedAt} onChange={(e) => setProposedAt(e.target.value)} />
            </label>
            <Button size="sm" onClick={() => onSendInvitation(proposedAt)} disabled={saving || !proposedAt}>
              {t("sendAction")}
            </Button>
          </div>

          <div className="space-y-2">
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("outcomeLabel")}
              <Input className="mt-2" value={outcome} onChange={(e) => setOutcome(e.target.value)} />
            </label>
            <Button size="sm" variant="secondary" onClick={() => onRecordOutcome(outcome)} disabled={saving || !outcome.trim()}>
              {t("saveOutcomeAction")}
            </Button>
          </div>
        </div>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={saving}>
            {t("close")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
