"use client";
import { useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import { Input } from "../ui/input";

export interface InvitationFormValues {
  email: string;
  organisationNameNote: string;
  locale: "nl" | "fr" | "en";
}

interface InvitationFormDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: (values: InvitationFormValues) => Promise<void>;
  saving: boolean;
}

const EMPTY_FORM: InvitationFormValues = { email: "", organisationNameNote: "", locale: "nl" };

// FR-001: create-only — an invitation is never edited in place (resend/revoke supersede it
// instead), unlike VaccineTypeFormDialog's dual create/edit shape.
export function InvitationFormDialog({ open, onOpenChange, onSubmit, saving }: InvitationFormDialogProps) {
  const t = useTranslations("platformAdmin.invitations");
  const [values, setValues] = useState<InvitationFormValues>(EMPTY_FORM);

  const canSave = values.email.trim().length > 0;

  async function handleSubmit() {
    await onSubmit(values);
    setValues(EMPTY_FORM);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("addTitle")}</DialogTitle>
          <DialogDescription>{t("dialogDescription")}</DialogDescription>
        </DialogHeader>
        <div className="grid grid-cols-1 gap-4">
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("emailLabel")}
            <Input
              type="email"
              className="mt-2"
              value={values.email}
              onChange={(e) => setValues((v) => ({ ...v, email: e.target.value }))}
            />
          </label>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("organisationNameNoteLabel")}
            <Input
              className="mt-2"
              value={values.organisationNameNote}
              onChange={(e) => setValues((v) => ({ ...v, organisationNameNote: e.target.value }))}
            />
          </label>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("localeLabel")}
            <select
              value={values.locale}
              onChange={(e) => setValues((v) => ({ ...v, locale: e.target.value as InvitationFormValues["locale"] }))}
              className="mt-2 h-10 w-full rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
            >
              <option value="nl">Nederlands</option>
              <option value="fr">Français</option>
              <option value="en">English</option>
            </select>
          </label>
        </div>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={saving}>
            {t("cancel")}
          </Button>
          <Button onClick={handleSubmit} disabled={saving || !canSave}>
            {t("send")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
