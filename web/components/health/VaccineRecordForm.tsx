"use client";
import { useEffect, useId, useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import { Input } from "../ui/input";
import { Textarea } from "../ui/textarea";
import { VaccineNameCombobox } from "./VaccineNameCombobox";
import type { CustomVaccineEntryResponse, VaccineRecordResponse, VaccineTypeResponse } from "../../lib/types";

export interface VaccineRecordFormValues {
  vaccineName: string;
  vaccineTypeId: string | null;
  doseNumber: number | null;
  administeredOn: string;
  nextDueDate: string | null;
  administeredBy: string | null;
  notes: string | null;
}

interface VaccineRecordFormProps {
  open: boolean;
  record: VaccineRecordResponse | null;
  vaccineTypes: VaccineTypeResponse[];
  customVaccineEntries: CustomVaccineEntryResponse[];
  onOpenChange: (open: boolean) => void;
  onSubmit: (values: VaccineRecordFormValues) => Promise<void>;
  saving: boolean;
  error?: string | null;
}

const today = () => new Date().toISOString().slice(0, 10);

/** Director create/edit form for one vaccine record (spec.md FR-001/FR-002, US1). Vaccine-name
 * field is a combobox (spec.md FR-013, US1/US2) — typing never clears a prior catalog/custom
 * reference (FR-004), only an explicit selection replaces it. */
export function VaccineRecordForm({ open, record, vaccineTypes, customVaccineEntries, onOpenChange, onSubmit, saving, error }: VaccineRecordFormProps) {
  const t = useTranslations("children.health.vaccines.form");
  const vaccineNameId = useId();
  const [vaccineName, setVaccineName] = useState("");
  const [vaccineTypeId, setVaccineTypeId] = useState<string | null>(null);
  const [doseNumber, setDoseNumber] = useState("");
  const [administeredOn, setAdministeredOn] = useState(today());
  const [nextDueDate, setNextDueDate] = useState("");
  const [administeredBy, setAdministeredBy] = useState("");
  const [notes, setNotes] = useState("");

  useEffect(() => {
    if (!open) return;
    setVaccineName(record?.vaccineName ?? "");
    setVaccineTypeId(record?.vaccineTypeId ?? null);
    setDoseNumber(record?.doseNumber ? String(record.doseNumber) : "");
    setAdministeredOn(record?.administeredOn ?? today());
    setNextDueDate(record?.nextDueDate ?? "");
    setAdministeredBy(record?.administeredBy ?? "");
    setNotes(record?.notes ?? "");
  }, [open, record]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{record ? t("editTitle") : t("addTitle")}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div>
            <label htmlFor={vaccineNameId} className="block text-sm font-medium text-text dark:text-text-dark">
              {t("vaccineNameLabel")}
            </label>
            <VaccineNameCombobox
              id={vaccineNameId}
              value={vaccineName}
              vaccineTypes={vaccineTypes}
              customEntries={customVaccineEntries}
              onChange={(text) => setVaccineName(text)}
              onSelect={({ name, vaccineTypeId: id }) => {
                setVaccineName(name);
                setVaccineTypeId(id);
              }}
            />
          </div>
          <div className="flex gap-4">
            <label className="block flex-1 text-sm font-medium text-text dark:text-text-dark">
              {t("doseNumberLabel")}
              <Input
                className="mt-2 tabular-nums"
                type="number"
                min={1}
                value={doseNumber}
                onChange={(e) => setDoseNumber(e.target.value)}
              />
            </label>
            <label className="block flex-1 text-sm font-medium text-text dark:text-text-dark">
              {t("administeredOnLabel")}
              <Input className="mt-2" type="date" value={administeredOn} onChange={(e) => setAdministeredOn(e.target.value)} />
            </label>
          </div>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("nextDueDateLabel")}
            <Input className="mt-2" type="date" value={nextDueDate} onChange={(e) => setNextDueDate(e.target.value)} />
          </label>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("administeredByLabel")}
            <Input className="mt-2" value={administeredBy} onChange={(e) => setAdministeredBy(e.target.value)} />
          </label>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("notesLabel")}
            <Textarea className="mt-2" value={notes} onChange={(e) => setNotes(e.target.value)} />
          </label>
          {error && <p className="text-sm text-danger dark:text-danger-dark">{error}</p>}
        </div>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={saving}>{t("cancel")}</Button>
          <Button
            disabled={saving || !vaccineName.trim() || !administeredOn}
            onClick={() =>
              onSubmit({
                vaccineName: vaccineName.trim(),
                vaccineTypeId,
                doseNumber: doseNumber ? Number(doseNumber) : null,
                administeredOn,
                nextDueDate: nextDueDate || null,
                administeredBy: administeredBy.trim() || null,
                notes: notes.trim() || null,
              })
            }
          >
            {t("save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
