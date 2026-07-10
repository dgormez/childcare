"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "./ui/dialog";
import { Button } from "./ui/button";
import { apiClient } from "../lib/apiClient";
import type { ChildResponse, WaitingListEntryResponse } from "../lib/types";

interface EnrollChildLinkDialogProps {
  open: boolean;
  entry: WaitingListEntryResponse | null;
  onOpenChange: (open: boolean) => void;
  onLinkExisting: (childId: string) => Promise<void>;
  onCreateNew: () => Promise<void>;
  saving: boolean;
}

export function EnrollChildLinkDialog({ open, entry, onOpenChange, onLinkExisting, onCreateNew, saving }: EnrollChildLinkDialogProps) {
  const t = useTranslations("waitingList");
  const [mode, setMode] = useState<"existing" | "new">("new");
  const [children, setChildren] = useState<ChildResponse[]>([]);
  const [childId, setChildId] = useState("");

  useEffect(() => {
    if (!open) return;
    setMode("new");
    setChildId("");
    (apiClient.GET as any)("/api/children").then((result: { response: Response; data?: ChildResponse[] }) => {
      if (!result.response.ok || !result.data) return;
      const active = result.data.filter((c) => !c.deactivatedAt);
      setChildren(active);
      if (active.length > 0) setChildId(active[0].id);
    });
  }, [open]);

  if (!entry) return null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("linkChildTitle")}</DialogTitle>
          <DialogDescription>{t("linkChildDescription")}</DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          <div className="flex gap-2">
            <Button variant={mode === "existing" ? "primary" : "secondary"} size="sm" onClick={() => setMode("existing")}>
              {t("linkExisting")}
            </Button>
            <Button variant={mode === "new" ? "primary" : "secondary"} size="sm" onClick={() => setMode("new")}>
              {t("createNew")}
            </Button>
          </div>
          {mode === "existing" ? (
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("childSelectLabel")}
              <select
                value={childId}
                onChange={(e) => setChildId(e.target.value)}
                className="mt-2 h-10 w-full rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
              >
                {children.map((child) => (
                  <option key={child.id} value={child.id}>
                    {child.firstName} {child.lastName}
                  </option>
                ))}
              </select>
            </label>
          ) : (
            <div className="rounded-lg bg-surface-soft p-3 text-sm text-text dark:bg-surface-soft-dark dark:text-text-dark">
              {entry.childFirstName} {entry.childLastName} — {new Date(`${entry.dateOfBirth}T00:00:00`).toLocaleDateString()}
            </div>
          )}
        </div>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={saving}>
            {t("cancel")}
          </Button>
          <Button
            onClick={() => (mode === "existing" ? onLinkExisting(childId) : onCreateNew())}
            disabled={saving || (mode === "existing" && !childId)}
          >
            {t("confirmLink")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
