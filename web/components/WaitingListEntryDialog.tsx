"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "./ui/dialog";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import type { WaitingListEntryResponse } from "../lib/types";

export interface WaitingListEntryFormValues {
  childFirstName: string;
  childLastName: string;
  dateOfBirth: string;
  contactName: string;
  contactEmail: string;
  contactPhone: string;
  requestedStartDate: string;
  notes: string;
}

interface WaitingListEntryDialogProps {
  open: boolean;
  entry: WaitingListEntryResponse | null;
  onOpenChange: (open: boolean) => void;
  onSubmit: (values: WaitingListEntryFormValues) => Promise<void>;
  saving: boolean;
}

const EMPTY_FORM: WaitingListEntryFormValues = {
  childFirstName: "",
  childLastName: "",
  dateOfBirth: "",
  contactName: "",
  contactEmail: "",
  contactPhone: "",
  requestedStartDate: "",
  notes: "",
};

export function WaitingListEntryDialog({ open, entry, onOpenChange, onSubmit, saving }: WaitingListEntryDialogProps) {
  const t = useTranslations("waitingList");
  const [values, setValues] = useState<WaitingListEntryFormValues>(EMPTY_FORM);

  useEffect(() => {
    if (!open) return;
    setValues(
      entry
        ? {
            childFirstName: entry.childFirstName,
            childLastName: entry.childLastName,
            dateOfBirth: entry.dateOfBirth,
            contactName: entry.contactName,
            contactEmail: entry.contactEmail ?? "",
            contactPhone: entry.contactPhone ?? "",
            requestedStartDate: entry.requestedStartDate ?? "",
            notes: entry.notes ?? "",
          }
        : EMPTY_FORM,
    );
  }, [open, entry]);

  function field(key: keyof WaitingListEntryFormValues) {
    return {
      value: values[key],
      onChange: (e: React.ChangeEvent<HTMLInputElement>) => setValues((v) => ({ ...v, [key]: e.target.value })),
    };
  }

  const canSave =
    values.childFirstName.trim() && values.childLastName.trim() && values.dateOfBirth && values.contactName.trim();

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{entry ? t("editTitle") : t("addTitle")}</DialogTitle>
          <DialogDescription>{t("dialogDescription")}</DialogDescription>
        </DialogHeader>
        <div className="grid grid-cols-2 gap-4">
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("childFirstNameLabel")}
            <Input className="mt-2" {...field("childFirstName")} />
          </label>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("childLastNameLabel")}
            <Input className="mt-2" {...field("childLastName")} />
          </label>
          <label className="col-span-2 block text-sm font-medium text-text dark:text-text-dark">
            {t("dateOfBirthLabel")}
            <Input className="mt-2" type="date" {...field("dateOfBirth")} />
          </label>
          <label className="col-span-2 block text-sm font-medium text-text dark:text-text-dark">
            {t("contactNameLabel")}
            <Input className="mt-2" {...field("contactName")} />
          </label>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("contactEmailLabel")}
            <Input className="mt-2" type="email" {...field("contactEmail")} />
          </label>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("contactPhoneLabel")}
            <Input className="mt-2" {...field("contactPhone")} />
          </label>
          <label className="col-span-2 block text-sm font-medium text-text dark:text-text-dark">
            {t("requestedStartDateLabel")}
            <Input className="mt-2" type="date" {...field("requestedStartDate")} />
          </label>
          <label className="col-span-2 block text-sm font-medium text-text dark:text-text-dark">
            {t("notesLabel")}
            <Input className="mt-2" {...field("notes")} />
          </label>
        </div>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={saving}>
            {t("cancel")}
          </Button>
          <Button onClick={() => onSubmit(values)} disabled={saving || !canSave}>
            {t("save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
