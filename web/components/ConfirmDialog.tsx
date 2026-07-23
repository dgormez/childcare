"use client";
import type { ReactNode } from "react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "./ui/dialog";
import { Button } from "./ui/button";

interface ConfirmDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description: string;
  confirmLabel: string;
  cancelLabel: string;
  onConfirm: () => void;
  confirmDestructive?: boolean;
  confirming?: boolean;
  /** Optional extra content between the description and the action buttons (e.g. a checkbox
   * gathering one more decision as part of confirming), rendered by callers that need it. */
  children?: ReactNode;
}

/** Shared confirmation modal for every destructive/state-changing row action in this feature
 * (PIN reset, deactivate/reactivate, device revoke — spec FR-010/FR-014). Motion: none beyond
 * the dialog primitive's own opacity transition, under 250ms per design-system.md. */
export function ConfirmDialog({
  open,
  onOpenChange,
  title,
  description,
  confirmLabel,
  cancelLabel,
  onConfirm,
  confirmDestructive = false,
  confirming = false,
  children,
}: ConfirmDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        {children}
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={confirming}>
            {cancelLabel}
          </Button>
          <Button
            variant={confirmDestructive ? "destructive" : "primary"}
            onClick={onConfirm}
            disabled={confirming}
          >
            {confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
