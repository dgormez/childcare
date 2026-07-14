"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import { Input } from "../ui/input";
import type { PlatformAdminVaccineTypeResponse, VaccineCategory } from "../../lib/types";

export interface VaccineTypeFormValues {
  name: string;
  category: VaccineCategory | "";
}

interface VaccineTypeFormDialogProps {
  open: boolean;
  entry: PlatformAdminVaccineTypeResponse | null;
  onOpenChange: (open: boolean) => void;
  onSubmit: (values: VaccineTypeFormValues) => Promise<void>;
  saving: boolean;
}

const EMPTY_FORM: VaccineTypeFormValues = { name: "", category: "" };

export function VaccineTypeFormDialog({ open, entry, onOpenChange, onSubmit, saving }: VaccineTypeFormDialogProps) {
  const t = useTranslations("vaccineTypes");
  const [values, setValues] = useState<VaccineTypeFormValues>(EMPTY_FORM);

  useEffect(() => {
    if (!open) return;
    setValues(entry ? { name: entry.name, category: entry.category ?? "" } : EMPTY_FORM);
  }, [open, entry]);

  const canSave = values.name.trim().length > 0;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{entry ? t("editTitle") : t("addTitle")}</DialogTitle>
          <DialogDescription>{t("dialogDescription")}</DialogDescription>
        </DialogHeader>
        <div className="grid grid-cols-1 gap-4">
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("nameLabel")}
            <Input
              className="mt-2"
              value={values.name}
              onChange={(e) => setValues((v) => ({ ...v, name: e.target.value }))}
            />
          </label>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("categoryLabel")}
            <select
              value={values.category}
              onChange={(e) => setValues((v) => ({ ...v, category: e.target.value as VaccineCategory | "" }))}
              className="mt-2 h-10 w-full rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
            >
              <option value="">{t("categoryNone")}</option>
              <option value="basisvaccinatieschema">{t("category.basisvaccinatieschema")}</option>
              <option value="aanbevolen_niet_gratis">{t("category.aanbevolen_niet_gratis")}</option>
            </select>
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
